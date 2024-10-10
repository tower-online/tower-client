using System.Timers;
using Google.FlatBuffers;
using Microsoft.Extensions.Logging;
using Tower.Network;
using tower.network.packet;

namespace Tower.Dummy;

public class DummyClient
{
    private readonly string _username;
    private readonly Connection _connection;
    private readonly ILogger _logger;

    private string? _authToken;
    private DummyPlayer? _player;

    private static readonly Random Rand = new();
    private DateTime _stayZoneUntil;

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
        _stayZoneUntil = DateTime.Now + TimeSpan.FromSeconds(Rand.Next(5, 15));
        var updateTask = Task.Run(async () =>
        {
            while (_connection.IsConnected)
            {
                await Task.Delay(100, cancellationToken);
                Update();
            }
        }, cancellationToken);

        await Task.WhenAll(connectionTask, updateTask);
    }

    public void Stop()
    {
        _logger.LogInformation("Stopping...");
        _connection.Disconnect();
    }

    private void Update()
    {
        if (_player is null) return;
        
        _player.Update();
        
        // Send movement
        {
            FlatBufferBuilder builder = new(128);
            PlayerMovement.StartPlayerMovement(builder);
            PlayerMovement.AddX(builder, _player.TargetDirection.X);
            PlayerMovement.AddZ(builder, _player.TargetDirection.Z);
            var movementOffset = PlayerMovement.EndPlayerMovement(builder);
            var packetBaseOffset = PacketBase.CreatePacketBase(builder, PacketType.PlayerMovement, movementOffset.Value);
            PacketBase.FinishSizePrefixedPacketBaseBuffer(builder, packetBaseOffset);

            _connection.SendPacket(builder.DataBuffer);
        }
        
        // Move to another zone
        if (Settings.ZoneMovementDisabled) return;
        if (DateTime.Now < _stayZoneUntil) return;
        _stayZoneUntil = DateTime.Now + TimeSpan.FromSeconds(Rand.Next(5, 15));
        
        {
            FlatBufferBuilder builder = new(128);
            PlayerEnterZoneRequest.StartPlayerEnterZoneRequest(builder);
            PlayerEnterZoneRequest.AddLocation(builder,
                WorldLocation.CreateWorldLocation(builder, 0, (uint)Rand.Next(1, 10)));
            var request = PlayerEnterZoneRequest.EndPlayerEnterZoneRequest(builder);
            var packetBase = PacketBase.CreatePacketBase(builder, PacketType.PlayerEnterZoneRequest, request.Value);
            PacketBase.FinishSizePrefixedPacketBaseBuffer(builder, packetBase);
            
            _connection.SendPacket(builder.DataBuffer);
        }
    }

    private void OnClientJoinResponse(ClientJoinResponse response)
    {
        _logger.LogInformation("Joined main server");
        
        var spawn = response.Spawn.Value;
        var playerData = spawn.Data.Value;
        var location = response.CurrentLocation.Value;

        _player = new DummyPlayer(spawn.EntityId, playerData.Name);

        var builder = new FlatBufferBuilder(128);
        PlayerEnterZoneRequest.StartPlayerEnterZoneRequest(builder);
        PlayerEnterZoneRequest.AddLocation(builder,
            WorldLocation.CreateWorldLocation(builder, location.Floor, location.ZoneId));
        var request = PlayerEnterZoneRequest.EndPlayerEnterZoneRequest(builder);
        var packetBase = PacketBase.CreatePacketBase(builder, PacketType.PlayerEnterZoneRequest, request.Value);
        builder.FinishSizePrefixed(packetBase.Value);
        _connection.SendPacket(builder.DataBuffer);
    }
}