using Godot;
using Google.FlatBuffers;
using System;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Tower.Network;

public partial class Connection : Node
{
    private readonly TcpClient _client = new TcpClient();
    private NetworkStream _stream;
    private Task _io;

    public async Task ConnectAsync(string host, int port)
    {
        GD.Print($"[{nameof(Connection)}] Connecting to {host}:{port}...");
        try
        {
            await _client.ConnectAsync(host, port);
            _stream = _client.GetStream();
        }
        catch (ArgumentOutOfRangeException ex)
        {
            GD.PrintErr($"[{nameof(Connection)}] port out of range: {port}");
            return;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[{nameof(Connection)}] Error connecting");
            return;
        }
    }

    public void Disconnect()
    {
        GD.Print($"[{nameof(Connection)}] Disconnecting...");
        _stream?.Close();
        _client?.Close();
    }

    public void Run()
    {
        _ = Task.Run(async () =>
        {
            while (_client.Connected)
            {
                byte[] buffer = await ReceivePacketAsync();
                await HandlePacketAsync(new ByteBuffer(buffer));
            }
        });
    }
    
    public override void _ExitTree()
    {
        Disconnect();
        base._ExitTree();
    }

    private async Task<byte[]> ReceivePacketAsync()
    {
        if (!_client.Connected) return [];

        try
        {
            var headerBuffer = new byte[FlatBufferConstants.SizePrefixLength];
            await _stream.ReadExactlyAsync(headerBuffer, 0, headerBuffer.Length);


            var bodyBuffer = new byte[ByteBufferUtil.GetSizePrefix(new ByteBuffer(headerBuffer))];
            await _stream.ReadExactlyAsync(bodyBuffer, 0, bodyBuffer.Length);

            return bodyBuffer;
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[{nameof(Connection)}] Error reading: {ex}");
            Disconnect();
        }

        return [];
    }

    private async Task SendPacketAsync(ByteBuffer buffer)
    {
        if (!_client.Connected) return;

        try
        {
            await _stream.WriteAsync(buffer.ToArray(0, buffer.Length).AsMemory(0, buffer.Length));
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[{nameof(Connection)}] Error sending: {ex}");
            Disconnect();
        }
    }

    private async Task HandlePacketAsync(ByteBuffer buffer)
    {
        var verifier = new Verifier(buffer);
        if (!verifier.VerifyPacketBase())
        {
            GD.PrintErr($"[{nameof(Connection)}] Invalid packet base");
            Disconnect();
            return;
        }
    }
}