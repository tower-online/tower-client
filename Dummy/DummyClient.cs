using System.Threading.Tasks.Dataflow;
using Google.FlatBuffers;
using Microsoft.Extensions.Logging;
using Tower.Dummy.Behavior;
using Tower.Network;
using tower.network.packet;

namespace Tower.Dummy;

public class DummyClient
{
    private readonly string _username;
    private readonly Connection _connection;
    private readonly Environment _environment = new();
    private readonly ILogger _logger;

    private string? _authToken;
    private DummyPlayer? _player;
    
    // Behaviors
    private readonly BehaviorTree _behavior;
    private DateTime _stayZoneUntil;
    private bool hasParty = false;

    private BehaviorTree CreateBehaviorTree()
    {
        var root = new SequenceNode();
        var tree = new BehaviorTree(root);

        var movementFireCondition = new FireConditionNode(() =>
        {
            if (Settings.MovementDisabled || _player is null) return false;
            if (DateTime.Now < _player.LastTransition + _player.TransitionStay) return false;
            _player.LastTransition = DateTime.Now;
            _player.TransitionStay = TimeSpan.FromSeconds(Random.Shared.Next(3, 10));
            return true;

        });
        root.AddChild(movementFireCondition);

        var movementSelector = new RandomSelectorNode();
        movementFireCondition.AddChild(movementSelector);

        var movementIdle = new ExecutionNode(() =>
        {
            if (_player is null) return Node.TraverseResult.Failure;

            _player.TargetDirection = new System.Numerics.Vector3(0, 0, 0);
            HandleMovement();

            return Node.TraverseResult.Success;
        });
        movementSelector.AddChild(movementIdle);

        var movementMoving = new ExecutionNode(() =>
        {
            if (_player is null) return Node.TraverseResult.Failure;

            _player.TargetDirection = new System.Numerics.Vector3(
                (float)Random.Shared.NextDouble() * 2.0f - 1.0f,
                0,
                (float)Random.Shared.NextDouble() * 2.0f - 1.0f);
            HandleMovement();

            return Node.TraverseResult.Success;
        });
        movementSelector.AddChild(movementMoving);

        var zoneMoveFireCondition = new FireConditionNode(() => !Settings.ZoneMovementDisabled && DateTime.Now > _stayZoneUntil);
        root.AddChild(zoneMoveFireCondition);

        var zoneMove = new ExecutionNode(() =>
        {
            HandleZoneMove((uint)Random.Shared.Next(1, 10));

            _stayZoneUntil = DateTime.Now + TimeSpan.FromSeconds(Random.Shared.Next(5, 15));
            return Node.TraverseResult.Success;
        });
        zoneMoveFireCondition.AddChild(zoneMove);

        return tree;
    }

    public DummyClient(string username, ILoggerFactory loggerFactory)
    {
        _username = username;
        _logger = loggerFactory.CreateLogger(_username);
        _connection = new Connection(_logger);

        _connection.Disconnected += Stop;

        _connection.ClientJoinResponseEvent += OnClientJoinResponse;
        _connection.PlayerEnterZoneResponseEvent += response =>
        {
            if (response.Result) return;
            _logger.LogWarning("PlayerEnterZone failed");
        };

        _connection.PlayerSpawnEvent += _environment.OnPlayerSpawn;
        _connection.PlayerSpawnsEvent += _environment.OnPlayerSpawns;
        _connection.EntityDespawnEvent += _environment.OnEntityDespawn;

        _behavior = CreateBehaviorTree();
    }

    public async Task Run(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Running...");

        _authToken = await Authentication.RequestAuthTokenAsync(Settings.RemoteHost, Settings.RemoteAuthPort, _username,
            Authentication.Platform.Test, _logger);
        if (_authToken is null)
        {
            Stop();
            return;
        }

        var characters = await Authentication.RequestCharactersAsync(Settings.RemoteHost, Settings.RemoteAuthPort,
            _username, _authToken, Authentication.Platform.Test, _logger);
        if (characters is null || characters.Count == 0)
        {
            Stop();
            return;
        }

        _logger.LogInformation("Character: {}", characters[0]);

        if (!await _connection.ConnectAsync(Settings.RemoteHost, Settings.RemoteMainPort))
        {
            Stop();
            return;
        }

        var connectionTask = _connection.Run(cancellationToken);

        // Send ClientJoinRequest with acquired token
        var characterName = characters[0];
        var builder = new FlatBufferBuilder(512);
        var request =
            ClientJoinRequest.CreateClientJoinRequest(builder,
                builder.CreateString(_username),
                builder.CreateString(characterName),
                builder.CreateString(_authToken));
        var packetBase = PacketBase.CreatePacketBase(builder, PacketType.ClientJoinRequest, request.Value);
        builder.FinishSizePrefixed(packetBase.Value);
        _connection.SendPacket(builder.DataBuffer);

        // Start update loop
        _stayZoneUntil = DateTime.Now + TimeSpan.FromSeconds(Random.Shared.Next(5, 15));
        var updateTask = Task.Run(async () =>
        {
            while (_connection.IsConnected)
            {
                try
                {
                    await Task.Delay(100, cancellationToken);
                }
                catch (TaskCanceledException)
                {
                    break;
                }

                Update();
            }
        }, cancellationToken);

        // Start packet handling loop
        var packetHandlingTask = Task.Run(async () =>
        {
            while (_connection.IsConnected)
            {
                try
                {
                    var buffer = await _connection.ReceiveBufferBlock.ReceiveAsync(cancellationToken);
                    _connection.HandlePacket(buffer);
                }
                catch (Exception)
                {
                    break;
                }
            }
        }, cancellationToken);

        var pingTask = _connection.StartPingAsync(cancellationToken);

        await Task.WhenAll(connectionTask, updateTask, packetHandlingTask, pingTask);
    }

    public void Stop()
    {
        _logger.LogInformation("Stopping...");
        _connection.Disconnect();
    }

    private void Update()
    {
        _behavior.Run();
    }

    private void OnClientJoinResponse(ClientJoinResponse response)
    {
        _logger.LogInformation("Joined main server");

        var spawn = response.Spawn.Value;
        var playerData = spawn.Data.Value;
        var location = response.CurrentLocation.Value;

        _player = new DummyPlayer(spawn.EntityId, spawn.ClientId, playerData.Name);

        var builder = new FlatBufferBuilder(128);
        PlayerEnterZoneRequest.StartPlayerEnterZoneRequest(builder);
        PlayerEnterZoneRequest.AddLocation(builder,
            WorldLocation.CreateWorldLocation(builder, location.Floor, location.ZoneId));
        var request = PlayerEnterZoneRequest.EndPlayerEnterZoneRequest(builder);
        var packetBase = PacketBase.CreatePacketBase(builder, PacketType.PlayerEnterZoneRequest, request.Value);
        builder.FinishSizePrefixed(packetBase.Value);
        _connection.SendPacket(builder.DataBuffer);
    }

    private void HandleMovement()
    {
        FlatBufferBuilder builder = new(128);
        PlayerMovement.StartPlayerMovement(builder);
        PlayerMovement.AddX(builder, _player.TargetDirection.X);
        PlayerMovement.AddZ(builder, _player.TargetDirection.Z);
        var movementOffset = PlayerMovement.EndPlayerMovement(builder);
        var packetBaseOffset =
            PacketBase.CreatePacketBase(builder, PacketType.PlayerMovement, movementOffset.Value);
        PacketBase.FinishSizePrefixedPacketBaseBuffer(builder, packetBaseOffset);

        _connection.SendPacket(builder.DataBuffer);
    }

    private void HandleZoneMove(uint newZoneId)
    {
        FlatBufferBuilder builder = new(128);
        PlayerEnterZoneRequest.StartPlayerEnterZoneRequest(builder);
        PlayerEnterZoneRequest.AddLocation(builder,
            WorldLocation.CreateWorldLocation(builder, 0, newZoneId));
        var request = PlayerEnterZoneRequest.EndPlayerEnterZoneRequest(builder);
        var packetBase = PacketBase.CreatePacketBase(builder, PacketType.PlayerEnterZoneRequest, request.Value);
        PacketBase.FinishSizePrefixedPacketBaseBuffer(builder, packetBase);

        _connection.SendPacket(builder.DataBuffer);
    }
}