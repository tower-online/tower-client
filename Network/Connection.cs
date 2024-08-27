using Godot;
using Google.FlatBuffers;
using System;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Tower.Network.Packet;

namespace Tower.Network;

public partial class Connection : Node
{
    [Signal]
    public delegate void SEntityMovementsEventHandler(
        int[] entityIds, Godot.Vector2[] targetDirections, Godot.Vector2[] targetPositions);

    [Signal]
    public delegate void SEntitySpawnsEventHandler(
        int[] entityIds, int[] entityTypes, Godot.Vector2[] positions, float[] rotations);

    [Signal]
    public delegate void SPlayerSpawnEventHandler(
        int entityId, int entityType, Godot.Vector2 position, float rotation);

    private readonly TcpClient _client = new TcpClient();
    private NetworkStream _stream;
    private readonly BufferBlock<ByteBuffer> _sendBufferBlock = new();

    private void Run()
    {
        _ = Task.Run(async () =>
        {
            const string username = "tester_00001";

            var token = await Auth.RequestToken(username);
            if (token == default) return;

            if (!await ConnectAsync("localhost", 30000)) return;

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
                        await _stream.WriteAsync(buffer.ToSizedArray());
                    }
                    catch (Exception)
                    {
                        Disconnect();
                    }
                }
            });

            // Send ClientJoinRequest with acquired token
            var builder = new FlatBufferBuilder(512);
            var request =
                ClientJoinRequest.CreateClientJoinRequest(builder,
                    ClientPlatform.TEST, builder.CreateString(username), builder.CreateString(token));
            var packetBase = PacketBase.CreatePacketBase(builder, PacketType.ClientJoinRequest, request.Value);
            builder.FinishSizePrefixed(packetBase.Value);

            SendPacket(builder.DataBuffer);
        });
    }

    public async Task<bool> ConnectAsync(string host, int port)
    {
        GD.Print($"[{nameof(Connection)}] Connecting to {host}:{port}...");
        try
        {
            await _client.ConnectAsync(host, port);
            _stream = _client.GetStream();
            _client.NoDelay = true;

            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            GD.PrintErr($"[{nameof(Connection)}] port out of range: {port}");
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[{nameof(Connection)}] Error connecting: {ex}");
        }

        return false;
    }

    public void Disconnect()
    {
        if (!_client.Connected) return;

        GD.Print($"[{nameof(Connection)}] Disconnecting...");
        _stream?.Close();
        _client?.Close();
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
            await _stream.ReadExactlyAsync(headerBuffer, 0, headerBuffer.Length);

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

    private void SendPacket(ByteBuffer buffer)
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
                HandlePlayerSpawn(packetBase.PacketBase_AsPlayerSpawn());
                break;

            case PacketType.ClientJoinResponse:
                HandleClientJoinResponse(packetBase.PacketBase_AsClientJoinResponse());
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

    private void HandleClientJoinResponse(ClientJoinResponse response)
    {
        if (response.Result != ClientJoinResult.OK)
        {
            GD.PrintErr($"[{nameof(Connection)}] [ClientJoinResponse] Failed");

            //TODO: Signal fail or retry?
            Disconnect();
            return;
        }

        GD.Print($"[{nameof(Connection)}] [ClientJoinResponse] OK");
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
            if (!movements.Movements(i).HasValue)
            {
                GD.PrintErr($"[{nameof(Connection)}] [{nameof(HandleEntityMovements)}] Invalid array");
                return;
            }

            var movement = movements.Movements(i).Value;
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
            if (!spawns.Spawns(i).HasValue)
            {
                GD.PrintErr($"[{nameof(Connection)}] [{nameof(HandleEntitySpawns)}] Invalid array");
                return;
            }

            var spawn = spawns.Spawns(i).Value;
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
        var position = new Godot.Vector2();
        if (spawn.Position.HasValue)
        {
            var pos = spawn.Position.Value;
            position.X = pos.X;
            position.Y = pos.Y;
        }
        
        CallDeferred(GodotObject.MethodName.EmitSignal, SignalName.SPlayerSpawn, (int)spawn.EntityId, (int)spawn.EntityType, position, spawn.Rotation);
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