using Godot;
using Tower.Network.Packet;

namespace Tower.Entity;

public partial class SimpleMonster : EntityBase
{
    private AnimationTree _animationTree;
    private Label3D _healthLabel;
    
    public override void _Ready()
    {
        base._Ready();
        
        _animationTree = GetNode<AnimationTree>("Pivot/Character/AnimationTree");
        _healthLabel = GetNode<Label3D>("HealthLabel");

        ResourceModified += OnResourceModified;
        
        _healthLabel.Text = $"{Health} / {MaxHealth}";
    }
    
    private void OnResourceModified(EntityResourceType type, EntityResourceChangeMode mode, int amount)
    {
        if (type == EntityResourceType.HEALTH)
        {
            _healthLabel.Text = $"{Health} / {MaxHealth}";
        }
    }
}