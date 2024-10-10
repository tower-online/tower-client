using Google.FlatBuffers;
using System;
using tower.network.packet;

namespace Tower.Network;

public partial class Connection
{
    public event Action<ClientJoinResponse>? ClientJoinResponseEvent;
    public event Action<PlayerSpawn>? PlayerSpawnEvent;
    public event Action<PlayerSpawns>? PlayerSpawnsEvent;
    public event Action<PlayerEnterZoneResponse>? PlayerEnterZoneResponseEvent;
    public event Action<EntityMovements>? EntityMovementsEvent;
    public event Action<EntitySpawns>? EntitySpawnsEvent;
    public event Action<EntityDespawn>? EntityDespawnEvent;
    public event Action<EntityResourceChanges>? EntityResourceChangesEvent; 
    public event Action<SkillMeleeAttack>? SkillMeleeAttackEvent;
    
    private void HandlePacket(ByteBuffer buffer)
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
            case PacketType.EntityMovements:
                EntityMovementsEvent?.Invoke(packetBase.PacketBase_AsEntityMovements());
                break;

            case PacketType.EntitySpawns:
                EntitySpawnsEvent?.Invoke(packetBase.PacketBase_AsEntitySpawns());
                break;

            case PacketType.EntityDespawn:
                EntityDespawnEvent?.Invoke(packetBase.PacketBase_AsEntityDespawn());
                break;
            
            case PacketType.EntityResourceChanges:
                EntityResourceChangesEvent?.Invoke(packetBase.PacketBase_AsEntityResourceChanges());
                break;
            
            case PacketType.SkillMeleeAttack:
                SkillMeleeAttackEvent?.Invoke(packetBase.PacketBase_AsSkillMeleeAttack());
                break;

            case PacketType.PlayerSpawn:
                PlayerSpawnEvent?.Invoke(packetBase.PacketBase_AsPlayerSpawn());
                break;

            case PacketType.PlayerSpawns:
                PlayerSpawnsEvent?.Invoke(packetBase.PacketBase_AsPlayerSpawns());
                break;

            case PacketType.PlayerEnterZoneResponse:
                PlayerEnterZoneResponseEvent?.Invoke(packetBase.PacketBase_AsPlayerEnterZoneResponse());
                break;

            case PacketType.ClientJoinResponse:
                ClientJoinResponseEvent?.Invoke(packetBase.PacketBase_AsClientJoinResponse());
                break;

            case PacketType.HeartBeat:
                HandleHeartBeat();
                break;

            case PacketType.Ping:
                HandlePing(packetBase.PacketBase_AsPing());
                break;
        }
    }
    
    private void HandleHeartBeat()
    {
        var builder = new FlatBufferBuilder(64);

        HeartBeat.StartHeartBeat(builder);
        var heartBeat = HeartBeat.EndHeartBeat(builder);
        var packetBase = PacketBase.CreatePacketBase(builder, PacketType.HeartBeat, heartBeat.Value);
        builder.FinishSizePrefixed(packetBase.Value);

        SendPacket(builder.DataBuffer);
    }
    
    private void HandlePing(Ping ping)
    {
        Ping = TimeSpan.FromMilliseconds(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - ping.Timestamp);
    }
}