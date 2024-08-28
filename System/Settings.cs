using Godot;
using System.Collections.Generic;
using YamlDotNet.Serialization;

namespace Tower.System;

public static class Settings
{
    public static string RemoteHost { get; }
    
    static Settings()
    {
        using var file = FileAccess.Open("res://settings.yaml", FileAccess.ModeFlags.Read);
        var content = file.GetAsText();
        var deserializer = new Deserializer();

        var settings = deserializer.Deserialize<Dictionary<string, object>>(content);
        RemoteHost = settings["remote_host"] as string;
    }
}