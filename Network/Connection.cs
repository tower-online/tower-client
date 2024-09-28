using Godot;
using Google.FlatBuffers;
using System;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Tower.Network.Packet;
using Timer = System.Timers.Timer;

namespace Tower.Network;

public partial class Connection : Node
{
    public event Action<ClientJoinResponse> ClientJoinResponseEvent;
    public event Action<PlayerSpawn> PlayerSpawnEvent;
    public event Action<PlayerSpawns> PlayerSpawnsEvent;
    public event Action<PlayerEnterZoneResponse> PlayerEnterZoneResponseEvent;
    public event Action<EntityMovements> EntityMovementsEvent;
    public event Action<EntitySpawns> EntitySpawnsEvent;
    public event Action<EntityDespawn> EntityDespawnEvent;

    private TcpClient _client;
    private NetworkStream _stream;
    private readonly BufferBlock<ByteBuffer> _receiveBufferBlock = new();
    private readonly BufferBlock<ByteBuffer> _sendBufferBlock = new();
    private bool _isConnecting = false;
    private readonly Timer _pingTimer = new(TimeSpan.FromSeconds(1));

    public bool IsClientConnected => _client?.Connected ?? false;
    public TimeSpan RoundtripLatency { get; private set; }

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
                if (buffer is null) continue;
                _receiveBufferBlock.Post(buffer);
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

        // TODO: Fix invalid packet base when add timestamp field
        // Ping loop
        // _pingTimer.Elapsed += (_, _) =>
        // {
        //     FlatBufferBuilder builder = new(64);
        //     var ping = Ping.CreatePing(builder, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        //     var packetBase = PacketBase.CreatePacketBase(builder, PacketType.Ping, ping.Value);
        //     builder.FinishSizePrefixed(packetBase.Value);
        //     SendPacket(builder.DataBuffer);
        // };
        // _pingTimer.AutoReset = true;
        // _pingTimer.Enabled = true;

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

    public override void _Process(double delta)
    {
        if (!_receiveBufferBlock.TryReceiveAll(out var buffers)) return;
        foreach (var buffer in buffers)
        {
            HandlePacket(buffer);
        }
    }

    public override void _ExitTree()
    {
        Disconnect();
        base._ExitTree();
    }

    private async Task<ByteBuffer> ReceivePacketAsync()
    {
        if (!_client.Connected) return null;

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

        return null;
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
                EntityMovementsEvent?.Invoke(packetBase.PacketBase_AsEntityMovements());
                break;

            case PacketType.EntitySpawns:
                EntitySpawnsEvent?.Invoke(packetBase.PacketBase_AsEntitySpawns());
                break;

            case PacketType.EntityDespawn:
                EntityDespawnEvent?.Invoke(packetBase.PacketBase_AsEntityDespawn());
                break;

            case PacketType.PlayerSpawn:
                PlayerSpawnEvent?.Invoke(packetBase.PacketBase_AsPlayerSpawn());
                break;

            case PacketType.PlayerSpawns:
                PlayerSpawnsEvent?.Invoke(packetBase.PacketBase_AsPlayerSpawns());
                break;

            case PacketType.PlayerEnterZoneResponse:
                PlayerEnterZoneResponseEvent?.Invoke(packetBase.PacketBase_AsPlayerEnterZoneResponse());
                break;

            case PacketType.ClientJoinResponse:
                ClientJoinResponseEvent?.Invoke(packetBase.PacketBase_AsClientJoinResponse());
                break;

            case PacketType.HeartBeat:
                HandleHeartBeat();
                break;

            case PacketType.Ping:
                HandlePing(packetBase.PacketBase_AsPing());
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

    private void HandlePing(Ping ping)
    {
        RoundtripLatency = TimeSpan.FromMilliseconds(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - ping.Timestamp);
        GD.Print($"current: {DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}");
        GD.Print($"ping: latency={RoundtripLatency.Milliseconds}ms");
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