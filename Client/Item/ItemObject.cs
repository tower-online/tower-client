using Godot;
using tower.network.packet;
using Tower.Player;

namespace Tower.Item;

public partial class ItemObject : Area3D, IInteractable
{
    public uint ObjectId { get; set; }
    public ItemType ItemType { get; set; }
    public uint Amount { get; set; }
    public string ItemName { get; set; }

    private Label3D _label;
    private ItemManager _itemManager;

    public override void _Ready()
    {
        _label = GetNode<Label3D>("Label");
        _label.Text = $"{ItemName} ({Amount})";

        _itemManager = GetNode<ItemManager>("/root/ItemManager");
    }

    public void Interact()
    {
        _itemManager.HandleItemPickup(this);
    }

    public string GetInteractionPrompt()
    {
        return $"to pick up {ItemName} ({Amount})";
    }
}