using System.Collections.Generic;
using Godot;
using tower.network.packet;
using Tower.System;
using Vector3 = Godot.Vector3;

namespace Tower.Item;

public partial class ItemManager : Node
{
    private ConnectionManager _connectionManager;
    
    private readonly PackedScene _itemScene = GD.Load<PackedScene>("res://Item/Item.tscn");

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
    }

    private void OnItemSpawn(ItemSpawn spawn)
    {
        var item = _itemScene.Instantiate<Item>();
        item.ObjectId = spawn.ObjectId;
        item.ItemType = spawn.ItemType;
        item.Amount = spawn.Amount;
        item.ItemName = ItemTypeToName[item.ItemType];

        if (spawn.Position.HasValue)
        {
            var position = spawn.Position.Value;
            item.Position = new Vector3(position.X, position.Y, position.Z);
        }
        
        AddSibling(item);
    }
}