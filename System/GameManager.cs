using Godot;
using System;

namespace Tower.System;

public partial class GameManager : Node
{
    public string? Username { get; private set; }
    public string? AuthToken { get; set; }

    public override void _Ready()
    {
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
}