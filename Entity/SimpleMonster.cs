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
        
        _animationTree = GetNode<AnimationTree>("Pivot/Model/AnimationTree");
        _healthLabel = GetNode<Label3D>("HealthLabel");

        ResourceModified += OnResourceModified;
        
        _healthLabel.Text = $"{Health} / {MaxHealth}";
    }

    public override void _PhysicsProcess(double delta)
    {
        base._PhysicsProcess(delta);
        HandleAnimations(delta);
    }

    public void HandleAttack()
    {
        _animationTree?.Set("parameters/PunchingShot/request", (int)AnimationNodeOneShot.OneShotRequest.Fire);
    }

    public void HanldeHit()
    {
        _animationTree?.Set("parameters/HitShot/request", (int)AnimationNodeOneShot.OneShotRequest.Fire);
    }
    
    private void HandleAnimations(double delta)
    {
        // If moving
        if (TargetDirection.IsZeroApprox())
        {
            _animationTree?.Set("parameters/Movement/transition_request", "Idle");
        }
        else
        {
            _animationTree?.Set("parameters/Movement/transition_request", "Running");
        }
    }
    
    private void OnResourceModified(EntityResourceType type, EntityResourceChangeMode mode, int amount)
    {
        if (type == EntityResourceType.HEALTH)
        {
            _healthLabel.Text = $"{Health} / {MaxHealth}";
            
            if (amount < 0) HanldeHit();
        }
    }
}