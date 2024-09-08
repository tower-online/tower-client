using Godot;
using System;
using System.Collections.Generic;
using Tower.Network;
using Tower.Network.Packet;
using Tower.Players;

using Vector2 = Godot.Vector2;
using Vector3 = Godot.Vector3;

namespace Tower.World;

public partial class EntityManager : Node
{
    private Connection? _connectionManager;
    private readonly Dictionary<int, Entity> _entities = new();
    private Player? _player;
    
    private readonly PackedScene _playerScene = GD.Load<PackedScene>("res://Players/Player.tscn");
    private readonly PackedScene _mainCameraScene = GD.Load<PackedScene>("res://Players/MainCamera.tscn");

    public override void _Ready()
    {
        _connectionManager = GetNode<Connection>("/root/ConnectionManager");

        _connectionManager.PlayerSpawnEvent += OnPlayerSpawn;


        // _connectionManager.SEntityMovements += OnEntityMovements;
        // _connectionManager.SEntitySpawns += OnEntitySpawns;
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
            Entity entity;
            switch ((EntityType)entityTypes[i])
            {
                case EntityType.HUMAN:
                    entity = (Player)_playerScene.Instantiate();
                    break;
                
                default:
                    GD.PrintErr($"{nameof(EntityManager)}/{nameof(OnEntitySpawns)}: Invalid entity type {entityTypes[i]}");
                    return;
            }

            entity!.EntityId = entityIds[i];
            entity.Position = new Vector3(positions[i].X, 0, positions[i].Y);
            // entity!.Rotation =

            _entities[entity.EntityId] = entity;
            AddSibling(entity);
        }
    }

    private void OnPlayerSpawn(PlayerSpawn spawn)
    {
        _player = _playerScene.Instantiate<Player>();

        // GD.Print($"{nameof(EntityManager)}/{nameof(OnPlayerSpawn)}: {entityId}");
        //
        // //TODO: Entity Type
        // var player = (Player)_playerScene.Instantiate();
        // player!.IsMaster = true;
        // player.EntityId = entityId;
        // player.Position = new Vector3(position.X, 0, position.Y);
        // // player!.Rotation =
        //
        // player.SPlayerMovement += _connectionManager.HandlePlayerMovement;
        //
        // _entities[player.EntityId] = player;
        // AddSibling(player);
        //
        // var mainCamera = _mainCameraScene.Instantiate();
        // mainCamera.Set("target", (Node3D)player);
        // AddSibling(mainCamera);
    }
}