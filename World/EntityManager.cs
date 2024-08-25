using System;
using Godot;
using System.Collections.Generic;
using Tower.Network;
using Tower.Network.Packet;
using Tower.Players;

using Vector2 = Godot.Vector2;
using Vector3 = Godot.Vector3;

namespace Tower.World;

public partial class EntityManager : Node
{
    private Connection _connectionManager;
    private readonly Dictionary<int, Entity> _entities = new();
    private Player _player;
    
    private readonly PackedScene _playerScene = GD.Load<PackedScene>("res://Players/Player.tscn");
    private readonly PackedScene _mainCameraScene = GD.Load<PackedScene>("res://Players/MainCamera.tscn");

    public override void _Ready()
    {
        _connectionManager = GetNode<Connection>("/root/ConnectionManager");
        _connectionManager.SEntityMovements += OnEntityMovements;
        _connectionManager.SEntitySpawns += OnEntitySpawns;
        _connectionManager.SPlayerSpawn += OnPlayerSpawn;
    }

    public override void _Process(double delta)
    {
    }

    private void OnEntityMovements(int[] entityIds, Vector2[] targetDirections, Vector2[] targetPositions)
    {
        for (var i = 0; i < entityIds.Length; i++)
        {
            if (!_entities.TryGetValue(entityIds[i], out var entity)) continue;

            entity.TargetDirection = targetDirections[i];
            entity.TargetPosition = targetPositions[i];
        }
    }

    private void OnEntitySpawns(int[] entityIds, int[] entityTypes, Vector2[] positions, float[] rotations)
    {
        for (var i = 0; i < entityIds.Length; i++)
        {
            if (!Enum.IsDefined(typeof(EntityType), entityIds[i]))
            {
                GD.PrintErr($"{nameof(EntityManager)}/{nameof(OnEntitySpawns)}: Invalid entity type {entityTypes[i]}");
                continue;
            }

            Entity entity;
            switch ((EntityType)entityIds[i])
            {
                case EntityType.PLAYER_HUMAN:
                    entity = (Player)_playerScene.Instantiate();
                    break;
                
                default:
                    return;
            }

            entity!.EntityId = entityIds[i];
            entity.Position = new Vector3(positions[i].X, 0, positions[i].Y);
            // entity!.Rotation =

            _entities[entity.EntityId] = entity;
            AddSibling(entity);
        }
    }

    private void OnPlayerSpawn(int entityId, int entityType, Vector2 position, float rotation)
    {
        GD.Print($"{nameof(EntityManager)}/{nameof(OnPlayerSpawn)}: {entityId}");

        //TODO: Entity Type
        var player = (Player)_playerScene.Instantiate();
        player!.IsMaster = true;
        player.EntityId = entityId;
        player.Position = new Vector3(position.X, 0, position.Y);
        // player!.Rotation =

        player.SPlayerMovement += _connectionManager.HandlePlayerMovement;

        _entities[player.EntityId] = player;
        AddSibling(player);

        var mainCamera = _mainCameraScene.Instantiate();
        mainCamera.Set("target", (Node3D)player);
        AddSibling(mainCamera);
    }
}