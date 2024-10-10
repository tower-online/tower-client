using Google.FlatBuffers;
using System;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Microsoft.Extensions.Logging;

namespace Tower.Network;

public partial class Connection(ILogger _logger, CancellationToken _cancellationToken)
{
    public bool IsConnected => _socket?.Connected ?? false; 
    public TimeSpan Ping { get; private set; }
    
    
    private TcpClient? _socket;
    private NetworkStream? _stream;
    private bool _isConnecting = false;
    
    public readonly BufferBlock<ByteBuffer> ReceiveBufferBlock = new();
    private readonly BufferBlock<ByteBuffer> _sendBufferBlock = new();

    public async Task<bool> ConnectAsync(string host, ushort port)
    {
        if (_isConnecting || IsConnected) return false;
        _isConnecting = true;
        
        _logger.LogInformation("Connecting to {}:{}...", host, port);
        try
        {
            _socket = new TcpClient();
            await _socket.ConnectAsync(host, port);
            _stream = _socket.GetStream();
            _socket.NoDelay = true;
        }
        catch (Exception ex)
        {
            _logger.LogError("Error connecting: {}", ex);
            return false;
        }
        
        // Start receiving loop
        _ = Task.Run(async () =>
        {
            while (IsConnected)
            {
                var buffer = await ReceivePacketAsync();
                if (buffer is null) continue;
                ReceiveBufferBlock.Post(buffer);
            }
        });
        
        // Start sending loop
        _ = Task.Run(async () =>
        {
            while (IsConnected)
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
        if (!IsConnected) return;
        
        _logger.LogInformation("Disconnecting...");
        _stream?.Close();
        _socket?.Close();

        _stream?.Dispose();
        _stream?.Dispose();
    }
    
    public void SendPacket(ByteBuffer buffer)
    {
        if (!IsConnected) return;

        _sendBufferBlock.Post(buffer);
    }
    
    private async Task<ByteBuffer?> ReceivePacketAsync()
    {
        if (!IsConnected) return null;

        try
        {
            var headerBuffer = new byte[FlatBufferConstants.SizePrefixLength];
            await _stream!.ReadExactlyAsync(headerBuffer, 0, headerBuffer.Length);

            var bodyBuffer = new byte[ByteBufferUtil.GetSizePrefix(new ByteBuffer(headerBuffer))];
            await _stream.ReadExactlyAsync(bodyBuffer, 0, bodyBuffer.Length);

            return new ByteBuffer(bodyBuffer);
        }
        catch (Exception)
        {
            Disconnect();
        }

        return null;
    }
}