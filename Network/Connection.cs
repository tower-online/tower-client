using Godot;
using Google.FlatBuffers;
using System;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Tower.Network.Packet;
using Tower.System;

namespace Tower.Network;

public partial class Connection : Node
{
    public event Action<PlayerSpawn> PlayerSpawnEvent;
    
    [Signal]
    public delegate void SEntityMovementsEventHandler(
        int[] entityIds, Godot.Vector2[] targetDirections, Godot.Vector2[] targetPositions);

    [Signal]
    public delegate void SEntitySpawnsEventHandler(
        int[] entityIds, int[] entityTypes, Godot.Vector2[] positions, float[] rotations);

    [Signal]
    public delegate void SClientJoinRequestEventHandler(ClientJoinResponseEventArgs args);

    private TcpClient _client;
    private NetworkStream _stream;
    private readonly BufferBlock<ByteBuffer> _sendBufferBlock = new();
    private bool _isConnecting = false;

    public bool IsClientConnected => _client?.Connected ?? false;

    public async Task<bool> ConnectAsync(string host, int port)
    {
        if (_isConnecting) return false;
        _isConnecting = true;
        
        GD.Print($"[{nameof(Connection)}] Connecting to {host}:{port}...");
        try
        {
            _client = new TcpClient();
            await _client.ConnectAsync(host, port);
            _stream = _client.GetStream();
            _client.NoDelay = true;
        }
        catch (ArgumentOutOfRangeException)
        {
            GD.PrintErr($"[{nameof(Connection)}] port out of range: {port}");
            return false;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[{nameof(Connection)}] Error connecting: {ex}");
            return false;
        }
        
        // Receiving loop
        _ = Task.Run(async () =>
        {
            while (_client.Connected)
            {
                var buffer = await ReceivePacketAsync();
                HandlePacket(buffer);
            }
        });

        // Sending loop
        _ = Task.Run(async () =>
        {
            while (_client.Connected)
            {
                var buffer = await _sendBufferBlock.ReceiveAsync();
                try
                {
                    await _stream!.WriteAsync(buffer.ToSizedArray());
                }
                catch (Exception)
                {
                    Disconnect();
                }
            }
        });

        _isConnecting = false;
        return true;
    }

    public void Disconnect()
    {
        if (!_client.Connected) return;

        GD.Print($"[{nameof(Connection)}] Disconnecting...");
        _stream?.Close();
        _client?.Close();
        
        _stream?.Dispose();
        _stream?.Dispose();
    }

    public override void _ExitTree()
    {
        Disconnect();
        base._ExitTree();
    }

    private async Task<ByteBuffer> ReceivePacketAsync()
    {
        if (!_client.Connected) return new ByteBuffer(0);

        try
        {
            var headerBuffer = new byte[FlatBufferConstants.SizePrefixLength];
            await _stream!.ReadExactlyAsync(headerBuffer, 0, headerBuffer.Length);

            var bodyBuffer = new byte[ByteBufferUtil.GetSizePrefix(new ByteBuffer(headerBuffer))];
            await _stream.ReadExactlyAsync(bodyBuffer, 0, bodyBuffer.Length);

            return new ByteBuffer(bodyBuffer);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception)
        {
            // GD.PrintErr($"[{nameof(Connection)}] Error reading: {ex}");
            Disconnect();
        }

        return new ByteBuffer(0);
    }

    public void SendPacket(ByteBuffer buffer)
    {
        if (!_client.Connected) return;

        _sendBufferBlock.Post(buffer);
    }

    private void HandlePacket(ByteBuffer buffer)
    {
        // if (!PacketBaseVerify.Verify(new Verifier(buffer), 0))
        // {
        //     GD.PrintErr($"[{nameof(Connection)}] Invalid packet base");
        //     Disconnect();
        //     return;
        // }

        var packetBase = PacketBase.GetRootAsPacketBase(buffer);
        switch (packetBase.PacketBaseType)
        {
            case PacketType.EntityMovements:
                HandleEntityMovements(packetBase.PacketBase_AsEntityMovements());
                break;

            case PacketType.EntitySpawns:
                HandleEntitySpawns(packetBase.PacketBase_AsEntitySpawns());
                break;

            case PacketType.EntityDespawn:
                HandleEntityDespawn(packetBase.PacketBase_AsEntityDespawn());
                break;

            case PacketType.PlayerSpawn:
                PlayerSpawnEvent?.Invoke(packetBase.PacketBase_AsPlayerSpawn());
                break;
            
            case PacketType.ClientJoinResponse:
                CallDeferred(GodotObject.MethodName.EmitSignal, SignalName.SClientJoinRequest,
                    new ClientJoinResponseEventArgs(packetBase.PacketBase_AsClientJoinResponse()));
                break;

            case PacketType.HeartBeat:
                HandleHeartBeat();
                break;
        }
    }

    #region Client Packet Handlers

    private void HandleHeartBeat()
    {
        var builder = new FlatBufferBuilder(64);

        HeartBeat.StartHeartBeat(builder);
        var heartBeat = HeartBeat.EndHeartBeat(builder);
        var packetBase = PacketBase.CreatePacketBase(builder, PacketType.HeartBeat, heartBeat.Value);
        builder.FinishSizePrefixed(packetBase.Value);

        SendPacket(builder.DataBuffer);
    }

    #endregion

    #region Entity Packet Handlers

    private void HandleEntityMovements(EntityMovements movements)
    {
        var length = movements.MovementsLength;
        var entityIds = new int[length];
        var targetDirections = new Godot.Vector2[length];
        var targetPositions = new Godot.Vector2[length];

        for (var i = 0; i < length; i++)
        {
            var movementBase = movements.Movements(i);
            if (!movementBase.HasValue)
            {
                GD.PrintErr($"[{nameof(Connection)}] [{nameof(HandleEntityMovements)}] Invalid array");
                return;
            }

            var movement = movementBase.Value;
            var targetDirection = movement.TargetDirection;
            var targetPosition = movement.TargetPosition;

            entityIds[i] = (int)movement.EntityId;
            targetDirections[i] = new Godot.Vector2(targetDirection.X, targetDirection.Y);
            targetPositions[i] = new Godot.Vector2(targetPosition.X, targetPosition.Y);
        }

        CallDeferred(GodotObject.MethodName.EmitSignal, SignalName.SEntityMovements,
            entityIds, targetDirections, targetPositions);
    }

    private void HandleEntitySpawns(EntitySpawns spawns)
    {
        var length = spawns.SpawnsLength;
        var entityIds = new int[length];
        var entityTypes = new int[length];
        var positions = new Godot.Vector2[length];
        var rotations = new float[length];

        for (var i = 0; i < length; i++)
        {
            var spawnBase = spawns.Spawns(i);
            if (!spawnBase.HasValue)
            {
                GD.PrintErr($"[{nameof(Connection)}] [{nameof(HandleEntitySpawns)}] Invalid array");
                return;
            }

            var spawn = spawnBase.Value;
            var position = spawn.Position;

            entityIds[i] = (int)spawn.EntityId;
            entityTypes[i] = (int)spawn.EntityType;
            positions[i] = new Godot.Vector2(position.X, position.Y);
            rotations[i] = spawn.Rotation;
        }

        CallDeferred(GodotObject.MethodName.EmitSignal, SignalName.SEntitySpawns,
            entityIds, entityTypes, positions, rotations);
    }

    private void HandleEntityDespawn(EntityDespawn despawn)
    {
    }

    #endregion

    #region Player Packet Handlers

    private void HandlePlayerSpawn(PlayerSpawn spawn)
    {
        // var position = new Godot.Vector2();
        // if (spawn.Position.HasValue)
        // {
        //     var pos = spawn.Position.Value;
        //     position.X = pos.X;
        //     position.Y = pos.Y;
        // }
        //
        // CallDeferred(GodotObject.MethodName.EmitSignal, SignalName.SPlayerSpawn, (int)spawn.EntityId, (int)spawn.EntityType, position, spawn.Rotation);
    }

    #endregion

    #region Player Action Handlers

    public void HandlePlayerMovement(Godot.Vector2 targetDirection)
    {
        var builder = new FlatBufferBuilder(128);
        PlayerMovement.StartPlayerMovement(builder);
        PlayerMovement.AddTargetDirection(builder,
            Packet.Vector2.CreateVector2(builder, targetDirection.X, targetDirection.Y));
        var movement = PlayerMovement.EndPlayerMovement(builder);
        var packetBase = PacketBase.CreatePacketBase(builder, PacketType.PlayerMovement, movement.Value);
        builder.FinishSizePrefixed(packetBase.Value);

        SendPacket(builder.DataBuffer);
    }

    #endregion
}

public partial class ClientJoinResponseEventArgs(ClientJoinResponse response) : GodotObject
{
    public ClientJoinResponse Response { get; } = response;
}