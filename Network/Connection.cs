using Google.FlatBuffers;
using System;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Microsoft.Extensions.Logging;

namespace Tower.Network;

public partial class Connection(ILogger logger)
{
    public event Action? Disconnected;
    public bool IsConnected => _socket?.Connected ?? false; 
    public TimeSpan Ping { get; private set; }
    
    private readonly TcpClient _socket = new();
    private NetworkStream? _stream;
    private bool _isConnecting = false;
    
    public readonly BufferBlock<ByteBuffer> ReceiveBufferBlock = new();
    private readonly BufferBlock<ByteBuffer> _sendBufferBlock = new();

    public async Task<bool> ConnectAsync(string host, ushort port)
    {
        if (_isConnecting || IsConnected) return false;
        _isConnecting = true;
        
        logger.LogInformation("Connecting to {}:{}...", host, port);
        try
        {
            await _socket.ConnectAsync(host, port);
            _stream = _socket.GetStream();
            _socket.NoDelay = true;
        }
        catch (Exception ex)
        {
            logger.LogError("Error connecting: {}", ex);
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
        
        logger.LogInformation("Disconnecting...");
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