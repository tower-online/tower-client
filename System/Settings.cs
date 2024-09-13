using Godot;
using System.Collections.Generic;
using YamlDotNet.Serialization;

namespace Tower.System;

public static class Settings
{
    public static string RemoteHost { get; }
    public static ushort RemoteAuthPort { get; }
    public static ushort RemoteMainPort { get; }
    
    static Settings()
    {
        using var file = FileAccess.Open("res://settings.yaml", FileAccess.ModeFlags.Read);
        var content = file.GetAsText();
        var deserializer = new Deserializer();

        var settings = deserializer.Deserialize<Dictionary<string, string>>(content);
        RemoteHost = settings["remote_host"];
        RemoteAuthPort = ushort.Parse(settings["remote_auth_port"]);
        RemoteMainPort = ushort.Parse(settings["remote_main_port"]);
    }
}