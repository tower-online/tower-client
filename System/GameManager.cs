using Godot;
using Google.FlatBuffers;
using Tower.Network;
using Tower.Network.Packet;

namespace Tower.System;

public partial class GameManager : Node
{
    public string Username { get; private set; }
    public string AuthToken { get; set; }

    private Connection _connectionManager;

    public override void _Ready()
    {
        _connectionManager = GetNode<Connection>("/root/ConnectionManager");
        _connectionManager.SClientJoinResponse += HandleClientJoinResponse;
        _connectionManager.SPlayerEnterZoneResponse += HandlePlayerEnterZoneResponse;
        
#if TOWER_PLATFORM_TEST
        foreach (var arg in OS.GetCmdlineUserArgs())
        {
            var option = arg.Split('=');
            if (option[0] == "username")
            {
                Username = option[1];
                GD.Print($"[{nameof(GameManager)}] username={Username}");
            }
        }

        if (Username == null)
        {
            GD.PrintErr($"[{nameof(GameManager)}] username not set");
        }

#elif TOWER_PLATFORM_STEAM
        //TODO: Get username and infos from Steamworks SDK
#endif
    }

    private void HandleClientJoinResponse(ClientJoinResponseEventArgs args)
    {
        var response = args.Response;
        var location = response.CurrentLocation.Value;
        GD.Print($"Player spawn on {location.Floor}/{location.ZoneId}");

        var builder = new FlatBufferBuilder(128);
        PlayerEnterZoneRequest.StartPlayerEnterZoneRequest(builder);
        PlayerEnterZoneRequest.AddLocation(builder, WorldLocation.CreateWorldLocation(builder, location.Floor, location.ZoneId));
        var request = PlayerEnterZoneRequest.EndPlayerEnterZoneRequest(builder);
        var packetBase = PacketBase.CreatePacketBase(builder, PacketType.PlayerEnterZoneRequest, request.Value);
        builder.FinishSizePrefixed(packetBase.Value);

        _connectionManager.SendPacket(builder.DataBuffer);
    }

    private void HandlePlayerEnterZoneResponse(PlayerEnterZoneResponseEventArgs args)
    {
        var response = args.Response;
        if (!response.Result)
        {
            GD.PrintErr("PlayerEnterZoneResponse: Fail");
            return;
        }
        
        var location = response.Location.Value;
        GD.Print($"Player enter zone {location.Floor}/{location.ZoneId}");
    }
}