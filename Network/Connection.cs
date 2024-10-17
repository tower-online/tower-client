using Google.FlatBuffers;
using System.Net.Sockets;
using System.Threading.Tasks.Dataflow;
using Microsoft.Extensions.Logging;
using tower.network.packet;

namespace Tower.Network;

public partial class Connection(ILogger logger)
{
    public event Action? Disconnected;
    public bool IsConnected => _socket?.Connected ?? false; 
    public TimeSpan CurrentPing { get; private set; }
    
    private readonly TcpClient _socket = new();
    private NetworkStream? _stream;
    private bool _isConnecting;
    
    public readonly BufferBlock<ByteBuffer> ReceiveBufferBlock = new();
    private readonly BufferBlock<ByteBuffer> _sendBufferBlock = new();

    private readonly ILogger _logger = logger;
    
    // Flatbuffers has bug with int64, so using dictionary here instead of adding Ping.timestamp: int64
    private readonly Dictionary<uint, long> _pings = [];

    public async Task<bool> ConnectAsync(string host, ushort port)
    {
        if (_isConnecting || IsConnected) return false;
        _isConnecting = true;
        
        _logger.LogInformation("Connecting to {}:{}...", host, port);
        try
        {
            await _socket.ConnectAsync(host, port);
            _stream = _socket.GetStream();
            _socket.NoDelay = true;
        }
        catch (Exception ex)
        {
            _logger.LogError("Error connecting: {}", ex);
            return false;
        }

        _isConnecting = false;
        return true;
    }

    public async Task Run(CancellationToken cancellationToken)
    {
        var receivingTask = Task.Run(async () =>
        {
            while (_socket.Connected)
            {
                var buffer = await ReceivePacketAsync(cancellationToken);
                if (buffer is null) continue;
                ReceiveBufferBlock.Post(buffer);
            }
            // logger.LogDebug("Receiving Task ending");
        }, cancellationToken);
        
        var sendingTask = Task.Run(async () =>
        {
            while (_socket.Connected)
            {
                try
                {
                    var buffer = await _sendBufferBlock.ReceiveAsync(cancellationToken);
                    if (_stream is null) break;
                    await _stream.WriteAsync(buffer.ToSizedArray(), cancellationToken);
                }
                catch (Exception)
                {
                    Disconnect();
                }
            }
            // logger.LogDebug("Sending Task ending");
        }, cancellationToken);

        await Task.WhenAll(receivingTask, sendingTask);
    }

    public void Disconnect()
    {
        if (!IsConnected) return;
        
        ReceiveBufferBlock.Complete();
        _sendBufferBlock.Complete();
        
        _logger.LogInformation("Disconnecting...");
        _stream?.Close();
        _socket.Close();

        _stream?.Dispose();
        _stream?.Dispose();
        
        Disconnected?.Invoke();
    }
    
    public void SendPacket(ByteBuffer buffer)
    {
        if (!IsConnected) return;

        _sendBufferBlock.Post(buffer);
    }

    public async Task StartPingAsync(CancellationToken cancellationToken)
    {
        uint seq = 0;
        
        while (_socket.Connected)
        {
            var builder = new FlatBufferBuilder(64);
            var ping = Ping.CreatePing(builder, seq);
            var packetBase = PacketBase.CreatePacketBase(builder, PacketType.Ping, ping.Value);
            PacketBase.FinishSizePrefixedPacketBaseBuffer(builder, packetBase);

            SendPacket(builder.DataBuffer);

            _pings[seq] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            seq += 1;
            if (seq == uint.MaxValue) seq = 0;

            try
            {
                await Task.Delay(5000, cancellationToken);
            }
            catch (Exception)
            {
                break;
            }
            
            // _logger.LogDebug("ping {}ms", CurrentPing.Milliseconds);
        }
    }
    
    private async Task<ByteBuffer?> ReceivePacketAsync(CancellationToken cancellationToken)
    {
        if (!IsConnected) return null;

        try
        {
            var headerBuffer = new byte[FlatBufferConstants.SizePrefixLength];
            await _stream!.ReadExactlyAsync(headerBuffer, 0, headerBuffer.Length, cancellationToken);

            var bodyBuffer = new byte[ByteBufferUtil.GetSizePrefix(new ByteBuffer(headerBuffer))];
            await _stream.ReadExactlyAsync(bodyBuffer, 0, bodyBuffer.Length, cancellationToken);

            return new ByteBuffer(bodyBuffer);
        }
        catch (Exception)
        {
            Disconnect();
        }

        return null;
    }
}