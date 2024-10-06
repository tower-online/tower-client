using System;
using Godot;
using Tower.Network.Packet;
using Vector3 = Godot.Vector3;

namespace Tower.Entity;

[GlobalClass]
public partial class EntityBase : Node3D
{
    public event Action<EntityResourceType, EntityResourceChangeMode, int> ResourceModified;
    
    public uint EntityId { get; set; }
    public Vector3 TargetDirection { get; set; }
    public Vector3 TargetPosition { get; set; }
    public Node3D Pivot { get; private set; }

    private float _targetRotation;
	
    //TODO: Extract to stats?
    public int Health { get; set; }
    public int MaxHealth { get; set; }

    public override void _Ready()
    {
        Pivot = GetNode<Node3D>("Pivot");
    }

    public override void _PhysicsProcess(double delta)
    {
        var alpha = 20 * delta;
        Position = Position.Lerp(TargetPosition, (float)alpha);

        if (TargetDirection.IsZeroApprox()) return;
        var angle = Mathf.Atan2(TargetDirection.X, TargetDirection.Z);
        var targetRotation = new Vector3(0, angle, 0);
        Pivot.Rotation = targetRotation;
        // Pivot.Rotation = Pivot.Rotation.Lerp(targetRotation, (float)alpha);
    }

    public void ModifyResource(EntityResourceType type, EntityResourceChangeMode mode, int amount)
    {
        if (type == EntityResourceType.HEALTH)
        {
            if (mode == EntityResourceChangeMode.ADD)
            {
                Health = int.Clamp(Health + amount, 0, MaxHealth);
            }
            else if (mode == EntityResourceChangeMode.SET)
            {
                Health = int.Clamp(amount, 0, MaxHealth);
            }
        }
        
        ResourceModified?.Invoke(type, mode, amount);
    }
}