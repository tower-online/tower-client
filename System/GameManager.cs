using Godot;
using System;
using Tower.Network;

namespace Tower.System;

public partial class GameManager : Node
{
    public string Username { get; private set; }
    public string AuthToken { get; set; }

    private Connection _connectionManager;

    public override void _Ready()
    {
        _connectionManager = GetNode<Connection>("/root/ConnectionManager");
        _connectionManager.SClientJoinRequest += HandleClientJoinResponse;
        
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
        var location = args.Response.CurrentLocation.Value;
        GD.Print($"Player spawn on {location.Floor}/{location.ZoneId}");
    }
}