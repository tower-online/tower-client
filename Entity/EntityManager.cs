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

    public override void _Ready()
    {
        _connectionManager = GetNode<Connection>("/root/ConnectionManager");
        _connectionManager.PlayerSpawnEvent += OnPlayerSpawn;
        _connectionManager.PlayerSpawnsEvent += OnPlayerSpawns;
        _connectionManager.EntityMovementsEvent += OnEntityMovements;
        _connectionManager.EntitySpawnsEvent += OnEntitySpawns;
        _connectionManager.EntityDespawnEvent += OnEntityDespawn;
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

            entity.TargetDirection = new Vector2(targetDirection.X, targetDirection.Y);
            entity.TargetPosition = new Vector2(targetPosition.X, targetPosition.Y);
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
            switch (spawn.EntityType)
            {
                default:
                    GD.PrintErr($"{nameof(EntityManager)}/{nameof(OnEntitySpawns)}: Invalid entity type {spawn.EntityType}");
                    return;
            }

            entityBase!.EntityId = spawn.EntityId;
            // entityBase.Position = new Vector3(positions[i].X, 0, positions[i].Y);
            // entity!.Rotation =

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

    public void OnPlayerSpawn(PlayerSpawn spawn)
    {
        GD.Print($"{nameof(EntityManager)}/{nameof(OnPlayerSpawn)}: {spawn.EntityId}");
        
        //TODO: Entity Type, Extract and remove duplication of spawning player
        var player = (PlayerBase)_playerScene.Instantiate();
        player.IsMaster = spawn.IsMaster;
        player.EntityId = spawn.EntityId;
        _entities[player.EntityId] = player;
        AddSibling(player);

        if (!player.IsMaster) return;
        _localPlayer = player;
        _localPlayer.SPlayerMovement += _connectionManager.HandlePlayerMovement;
        
        var mainCamera = _mainCameraScene.Instantiate();
        mainCamera.Set("target", (Node3D)player);
        AddSibling(mainCamera);
    }

    private void OnPlayerSpawns(PlayerSpawns spawns)
    {
        GD.Print($"OnPlayerSpawns: {spawns.SpawnsLength}");
        for (int i = 0; i < spawns.SpawnsLength; i++)
        {
            PlayerSpawn spawn;
            {
                var s = spawns.Spawns(i);
                if (!s.HasValue) continue;
                spawn = s.Value;
            }
            
            //TODO: Entity Type
            var player = (PlayerBase)_playerScene.Instantiate();
            player.IsMaster = spawn.IsMaster;
            player.EntityId = spawn.EntityId;
            _entities[player.EntityId] = player;
            AddSibling(player);
        }
    }
}