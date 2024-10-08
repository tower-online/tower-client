using Godot;
using System.Collections.Generic;
using Tower.Network;
using Tower.Network.Packet;
using Tower.Player;

using Vector2 = Godot.Vector2;
using Vector3 = Godot.Vector3;

namespace Tower.Entity;

public partial class EntityManager : Node
{
    private Connection _connectionManager;
    private readonly Dictionary<uint, EntityBase> _entities = new();
    private PlayerBase _localPlayer;
    
    private readonly PackedScene _playerScene = GD.Load<PackedScene>("res://Player/Player.tscn");
    private readonly PackedScene _mainCameraScene = GD.Load<PackedScene>("res://Player/MainCamera.tscn");
    
    private readonly PackedScene _simpleMonsterScene = GD.Load<PackedScene>("res://Entity/SimpleMonster.tscn");

    public override void _Ready()
    {
        _connectionManager = GetNode<Connection>("/root/ConnectionManager");
        _connectionManager.PlayerSpawnEvent += OnPlayerSpawn;
        _connectionManager.PlayerSpawnsEvent += OnPlayerSpawns;
        _connectionManager.EntityMovementsEvent += OnEntityMovements;
        _connectionManager.EntitySpawnsEvent += OnEntitySpawns;
        _connectionManager.EntityDespawnEvent += OnEntityDespawn;
        _connectionManager.EntityResourceChangesEvent += OnEntityResourceChanges;
        _connectionManager.SkillMeleeAttackEvent += OnSkillMeleeAttack;
    }

    public void Clear()
    {
        _entities.Clear();
        
        if (_localPlayer is not null) _entities[_localPlayer.EntityId] = _localPlayer;
    }

    private void OnEntityMovements(EntityMovements movements)
    {
        for (var i = 0; i < movements.MovementsLength; i++)
        {
            EntityMovement movement;
            {
                var m = movements.Movements(i);
                if (!m.HasValue) continue;
                movement = m.Value;
            }
            
            if (!_entities.TryGetValue(movement.EntityId, out var entity)) continue;

            var targetDirection = movement.TargetDirection;
            var targetPosition = movement.TargetPosition;

            entity.TargetDirection = new Vector3(targetDirection.X, targetDirection.Y, targetDirection.Z);
            entity.TargetPosition = new Vector3(targetPosition.X, targetPosition.Y, targetPosition.Z);
            if (!entity.TargetDirection.IsZeroApprox())
            {
                entity.TargetDirection = entity.TargetDirection.Normalized();
            }
        }
    }

    private void OnEntitySpawns(EntitySpawns spawns)
    {
        for (var i = 0; i < spawns.SpawnsLength; i++)
        {
            EntitySpawn spawn;
            {
                var s = spawns.Spawns(i);
                if (!s.HasValue) continue;
                spawn = s.Value;
            }
            
            EntityBase entityBase;
            var entityType = spawn.EntityType;
            if (entityType == EntityType.SIMPLE_MONSTER)
            {
                entityBase = (SimpleMonster)_simpleMonsterScene.Instantiate();
            }
            else
            {
                GD.PrintErr($"{nameof(EntityManager)}/{nameof(OnEntitySpawns)}: Invalid entity type {spawn.EntityType}");
                return;
            }

            entityBase.EntityId = spawn.EntityId;
            if (spawn.Position.HasValue)
            {
                var position = spawn.Position.Value;
                entityBase.Position = new Vector3(position.X, 0, position.Y);
            }
            // entityBase.Rotation = spawn.Rotation;
            entityBase.Health = (int)spawn.Health;
            entityBase.MaxHealth = (int)spawn.MaxHealth;

            _entities[entityBase.EntityId] = entityBase;
            AddSibling(entityBase);
        }
    }

    private void OnEntityDespawn(EntityDespawn despawn)
    {
        var entityId = despawn.EntityId;
        if (!_entities.TryGetValue(entityId, out var entity)) return;
        
        entity.QueueFree();
        _entities.Remove(entityId);
    }

    private void OnEntityResourceChanges(EntityResourceChanges changes)
    {
        for (var i = 0; i < changes.ChangesLength; i++)
        {
            EntityResourceChange change;
            {
                var c = changes.Changes(i);
                if (!c.HasValue) continue;
                change = c.Value;
            }
            
            if (!_entities.TryGetValue(change.EntityId, out var entity)) continue;
            entity.ModifyResource(change.ResourceType, change.ModifyMode, change.ModifyingAmount);
        }
    }

    public void OnPlayerSpawn(PlayerSpawn spawn)
    {
        // GD.Print($"{nameof(EntityManager)}/{nameof(OnPlayerSpawn)}: {spawn.EntityId}");
        
        //TODO: Entity Type, Extract and remove duplication of spawning player
        var player = (PlayerBase)_playerScene.Instantiate();
        player.IsMaster = spawn.IsMaster;
        player.EntityId = spawn.EntityId;

        if (spawn.Data is not null)
        {
            var data = spawn.Data.Value;
            player.CharacterName = data.Name;

            if (data.Stats.HasValue)
            {
                var stats = data.Stats.Value;
            }

            for (var i = 0; i < data.ResourcesLength; i += 1)
            {
                var r = data.Resources(i);
                if (!r.HasValue) return;
                var resource = r.Value;

                if (resource.Type == EntityResourceType.HEALTH)
                {
                    player.MaxHealth = resource.MaxValue;
                    player.Health = resource.Value;
                }
            }
        }
        
        _entities[player.EntityId] = player;
        AddSibling(player);

        if (!player.IsMaster) return;
        _localPlayer = player;
        _localPlayer.SPlayerMovement += _connectionManager.HandlePlayerMovement;
        _localPlayer.Attack1Event += _connectionManager.HandlePlayerAttack1;
        
        var mainCamera = _mainCameraScene.Instantiate();
        mainCamera.Set("target", (Node3D)player);
        AddSibling(mainCamera);
    }

    private void OnPlayerSpawns(PlayerSpawns spawns)
    {
        for (int i = 0; i < spawns.SpawnsLength; i++)
        {
            PlayerSpawn spawn;
            {
                var s = spawns.Spawns(i);
                if (!s.HasValue) continue;
                spawn = s.Value;
            }
            
            OnPlayerSpawn(spawn);
        }
    }

    private void OnSkillMeleeAttack(SkillMeleeAttack attack)
    {
        var entityId = attack.EntityId;
        // var weaponSlot = attack.WeaponSlot;

        if (!_entities.TryGetValue(entityId, out var entity)) return;
        if (entity is not PlayerBase player) return;
        player.HandleAttack1();
    }
}