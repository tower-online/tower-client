using Godot;

namespace Tower.Entity;

[GlobalClass]
public partial class EntityBase : Node3D
{
    public uint EntityId { get; set; }
    public Vector3 TargetDirection { get; set; }
    public Vector3 TargetPosition { get; set; }
    public Node3D Pivot { get; private set; }

    private float _targetRotation;

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
}