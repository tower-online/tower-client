using System.Collections.Generic;
using Godot;
using Google.FlatBuffers;
using tower.network.packet;
using Tower.System;
using Vector3 = Godot.Vector3;

namespace Tower.Item;

public partial class ItemManager : Node
{
    private ConnectionManager _connectionManager;
    
    private readonly PackedScene _itemScene = GD.Load<PackedScene>("res://Item/ItemObject.tscn");
    private readonly Dictionary<uint, ItemObject> _items = [];

    private static readonly Dictionary<ItemType, string> ItemTypeToName = new()
    {
        [ItemType.NONE] = "None",
        [ItemType.FIST] = "Fist",
        [ItemType.GOLD] = "Gold"
    };
        
    public override void _Ready()
    {
        _connectionManager = GetNode<ConnectionManager>("/root/ConnectionManager");
        _connectionManager.Connection.ItemSpawnEvent += OnItemSpawn;
        _connectionManager.Connection.ItemSpawnsEvent += OnItemSpawns;
        _connectionManager.Connection.ItemDespawnEvent += OnItemDespawn;
    }

    public void HandleItemPickup(ItemObject item)
    {
        FlatBufferBuilder builder = new(64);
        var pickup = PlayerPickupItem.CreatePlayerPickupItem(builder, item.ObjectId);
        var packetBase = PacketBase.CreatePacketBase(builder, PacketType.PlayerPickupItem, pickup.Value);
        builder.FinishSizePrefixed(packetBase.Value);
        
        _connectionManager.Connection.SendPacket(builder.DataBuffer);
    }

    private void OnItemSpawn(ItemSpawn spawn)
    {
        var item = _itemScene.Instantiate<ItemObject>();
        item.ObjectId = spawn.ObjectId;
        item.ItemType = spawn.ItemType;
        item.Amount = spawn.Amount;
        item.ItemName = ItemTypeToName[item.ItemType];

        if (spawn.Position.HasValue)
        {
            var position = spawn.Position.Value;
            item.Position = new Vector3(position.X, position.Y, position.Z);
        }

        _items[item.ObjectId] = item;
        AddSibling(item);
    }

    private void OnItemSpawns(ItemSpawns spawns)
    {
        var length = spawns.SpawnsLength;
        for (var i = 0; i < length; i += 1)
        {
            var s = spawns.Spawns(i);
            if (!s.HasValue) continue;
            OnItemSpawn(s.Value);
        }
    }

    private void OnItemDespawn(ItemDespawn despawn)
    {
        var objectId = despawn.ObjectId;
        if (!_items.TryGetValue(objectId, out var item)) return;
        
        item.QueueFree();
        _items.Remove(objectId);
    }
}