using Godot;
using Google.FlatBuffers;
using System;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Collections.Generic;
using Tower.Network.Packet;

using System.Net.Http;
using System.Text;
using System.Text.Json;
using HttpClient = System.Net.Http.HttpClient;

namespace Tower.Network;

public partial class Connection : Node
{
    [Signal]
    public delegate void EntityMovementEventHandler();

    private readonly TcpClient _client = new TcpClient();
    private NetworkStream _stream;

    public override void _Ready()
    {
        _ = Task.Run(async () =>
        {
            const string username = "tester_00001";

            var token = await RequestAuthToken(username);
            if (token == default) return;

            if (!await ConnectAsync("localhost", 30000)) return;

            _ = Task.Run(async () =>
            {
                while (_client.Connected)
                {
                    byte[] buffer = await ReceivePacketAsync();
                    await HandlePacketAsync(new ByteBuffer(buffer));
                }
            });

            var builder = new FlatBufferBuilder(256);
            var usernameOffset = builder.CreateString(username);
            var tokenOffset = builder.CreateString(token);
            var request = ClientJoinRequest.CreateClientJoinRequest(builder, ClientPlatform.TEST, usernameOffset, tokenOffset);
            var packetBase = PacketBase.CreatePacketBase(builder, PacketType.ClientJoinRequest, request.Value);
            builder.FinishSizePrefixed(packetBase.Value);
            
            await SendPacketAsync(builder.DataBuffer);
        });
    }

    public static async Task<string> RequestAuthToken(string username)
    {
        const string url = "https://localhost:8000/token/test";
        var requestData = new Dictionary<string, string>()
        {
            ["username"] = username
        };
        GD.Print($"[{nameof(Connection)}] Requesting auth token: {url} as username={username}");

        using var handler = new HttpClientHandler();
        GD.Print("Warning: Allowing self-signed certification for auth server. Remove this in release");
        handler.ServerCertificateCustomValidationCallback = (_, _, _, _) => true;

        using var client = new HttpClient(handler);
        try
        {
            HttpResponseMessage response = await client.PostAsync(url, new StringContent(
                JsonSerializer.Serialize(requestData), Encoding.UTF8, "application/json"
            ));
            response.EnsureSuccessStatusCode();

            var body = await response.Content.ReadAsStringAsync();
            var json = JsonDocument.Parse(body).RootElement;
            if (json.TryGetProperty("jwt", out var jwtElem))
            {
                var jwt = jwtElem.GetString();
                GD.Print($"[{nameof(Connection)}] Requesting auth token succeed: {jwt}");
                return jwt;
            }

            GD.PrintErr($"Invalid response body");
            return default;
        }
        catch (HttpRequestException ex)
        {
            GD.PrintErr($"Error requesting token: {ex}");
            return default;
        }
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
        catch (Exception)
        {
            // GD.PrintErr($"[{nameof(Connection)}] Error reading: {ex}");
            Disconnect();
        }

        return [];
    }

    private async Task SendPacketAsync(ByteBuffer buffer)
    {
        if (!_client.Connected) return;
        
        try
        {
            await _stream.WriteAsync(buffer.ToSizedArray());
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception)
        {
            // GD.PrintErr($"[{nameof(Connection)}] Error sending: {ex}");
            Disconnect();
        }
    }

    private async Task HandlePacketAsync(ByteBuffer buffer)
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
            case PacketType.HeartBeat:
                await HandleHeartBeat();
                break;

            case PacketType.ClientJoinResponse:
                HandleClientJoinResponse(packetBase.PacketBase_AsClientJoinResponse());
                break;
        }
    }

    private async Task HandleHeartBeat()
    {
        var builder = new FlatBufferBuilder(64);

        HeartBeat.StartHeartBeat(builder);
        var heartBeat = HeartBeat.EndHeartBeat(builder);
        var packetBase = PacketBase.CreatePacketBase(builder, PacketType.HeartBeat, heartBeat.Value);
        builder.FinishSizePrefixed(packetBase.Value);

        await SendPacketAsync(builder.DataBuffer);
    }

    private void HandleClientJoinResponse(ClientJoinResponse response)
    {
        if (response.Result != ClientJoinResult.OK)
        {
            GD.PrintErr($"[{nameof(Connection)}] [ClientJoinResponse] Failed");
            return;
        }

        GD.Print($"[{nameof(Connection)}] [ClientJoinResponse] OK");
    }
}