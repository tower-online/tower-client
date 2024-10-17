using Godot;
using tower.network.packet;

namespace Tower.Item;

public partial class Item : Node3D
{
    public uint ObjectId { get; set; }
    public ItemType ItemType { get; set; }
    public uint Amount { get; set; }
    public string ItemName { get; set; }

    private Label3D _label;

    public override void _Ready()
    {
        _label = GetNode<Label3D>("Label");
        
        _label.Text = $"{ItemName} ({Amount})";
    }
}