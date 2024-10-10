using YamlDotNet.Serialization;

namespace Tower.Dummy;

public static class Settings
{
    public static string RemoteHost { get; }
    public static ushort RemoteAuthPort { get; }
    public static ushort RemoteMainPort { get; }
    public static uint NumClients { get; }
    public static bool MovementDisabled { get; }
    public static bool ZoneMovementDisabled { get; }
    
    static Settings()
    {
        
        var content = File.ReadAllText("settings.yaml");
        var deserializer = new Deserializer();

        var settings = deserializer.Deserialize<Dictionary<string, object>>(content);
        RemoteHost = Convert.ToString(settings["remote_host"])!;
        RemoteAuthPort = Convert.ToUInt16(settings["remote_auth_port"]);
        RemoteMainPort = Convert.ToUInt16(settings["remote_main_port"]);
        NumClients = Convert.ToUInt32(settings["num_clients"]);
        MovementDisabled = Convert.ToBoolean(settings["disable_movement"]);
        ZoneMovementDisabled = Convert.ToBoolean(settings["disable_zone_movement"]);
    }
}