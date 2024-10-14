using tower.network.packet;

namespace Tower.Dummy;

public class Environment
{
    // public Dictionary<uint, Entity> Entities { get; } = new();
    public Dictionary<uint, PlayerBase> OtherPlayers { get; } = new();

    public void OnPlayerSpawn(PlayerSpawn spawn)
    {
        var characterName = "NULL";
        if (spawn.Data.HasValue)
        {
            var data = spawn.Data.Value;
            characterName = data.Name;
        }

        var newPlayer = new PlayerBase(spawn.EntityId, spawn.ClientId, characterName);
        OtherPlayers[newPlayer.EntityId] = newPlayer;
    }

    public void OnPlayerSpawns(PlayerSpawns spawns)
    {
        for (var i = 0; i < spawns.SpawnsLength; i++)
        {
            var s = spawns.Spawns(i);
            if (!s.HasValue) return;
            OnPlayerSpawn(s.Value);
        }
    }

    public void OnEntityDespawn(EntityDespawn despawn)
    {
        OtherPlayers.Remove(despawn.EntityId);
    }
}