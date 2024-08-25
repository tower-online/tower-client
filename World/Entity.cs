using Godot;

namespace Tower.World;

[GlobalClass]
public partial class Entity : Node3D
{
    public int EntityId { get; set; }
    public Vector2 TargetDirection { get; set; }
    public Vector2 TargetPosition { get; set; }

    public override void _PhysicsProcess(double delta)
    {
        var alpha = 20 * delta;
        Position = Position.Lerp(new Vector3(TargetPosition.X, 0, TargetPosition.Y), (float)alpha);
    }
}