using Godot;

namespace Tower.Entity;

[GlobalClass]
public partial class EntityBase : Node3D
{
    public uint EntityId { get; set; }
    public Vector2 TargetDirection { get; set; }
    public Vector2 TargetPosition { get; set; }

    public override void _PhysicsProcess(double delta)
    {
        var alpha = 20 * delta;
        Position = Position.Lerp(new Vector3(TargetPosition.X, 0, TargetPosition.Y), (float)alpha);
    }
}