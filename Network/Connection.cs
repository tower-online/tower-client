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
    public delegate void SClientJoinResponseEventHandler(ClientJoinResponseEventArgs args);

    [Signal]
    public delegate void SPlayerEnterZoneResponseEventHandler(PlayerEnterZoneResponseEventArgs args);

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
                // HandleEntityMovements(packetBase.PacketBase_AsEntityMovements());
                break;

            case PacketType.EntitySpawns:
                // HandleEntitySpawns(packetBase.PacketBase_AsEntitySpawns());
                break;

            case PacketType.EntityDespawn:
                // HandleEntityDespawn(packetBase.PacketBase_AsEntityDespawn());
                break;

            case PacketType.PlayerSpawn:
                PlayerSpawnEvent?.Invoke(packetBase.PacketBase_AsPlayerSpawn());
                break;
            
            case PacketType.PlayerEnterZoneResponse:
                CallDeferred(GodotObject.MethodName.EmitSignal, SignalName.SPlayerEnterZoneResponse,
                    new PlayerEnterZoneResponseEventArgs(packetBase.PacketBase_AsPlayerEnterZoneResponse()));
                break;
            
            case PacketType.ClientJoinResponse:
                CallDeferred(GodotObject.MethodName.EmitSignal, SignalName.SClientJoinResponse,
                    new ClientJoinResponseEventArgs(packetBase.PacketBase_AsClientJoinResponse()));
                break;

            case PacketType.HeartBeat:
                HandleHeartBeat();
                break;
        }
    }

    private void HandleHeartBeat()
    {
        var builder = new FlatBufferBuilder(64);

        HeartBeat.StartHeartBeat(builder);
        var heartBeat = HeartBeat.EndHeartBeat(builder);
        var packetBase = PacketBase.CreatePacketBase(builder, PacketType.HeartBeat, heartBeat.Value);
        builder.FinishSizePrefixed(packetBase.Value);

        SendPacket(builder.DataBuffer);
    }

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
}

public partial class ClientJoinResponseEventArgs(ClientJoinResponse response) : GodotObject
{
    public ClientJoinResponse Response { get; } = response;
}

public partial class PlayerEnterZoneResponseEventArgs(PlayerEnterZoneResponse response) : GodotObject
{
    public PlayerEnterZoneResponse Response { get; } = response;
}