using Godot;
using System;
using Tower.World;

namespace Tower.Players;

[GlobalClass]
public partial class Player : Entity
{
	[Signal]
	public delegate void SPlayerMovementEventHandler(Vector2 targetDirection);
	
	public bool IsMaster { get; set; } = false;
	private Vector2 TargetDirectionInput { get; set; }
	
	private readonly TimeSpan _movementTickInterval = TimeSpan.FromMilliseconds(50);
	private DateTime _lastMovementTick = DateTime.Now;
	
	public override void _Ready()
	{
	}

	public override void _PhysicsProcess(double delta)
	{
		base._PhysicsProcess(delta);

		if (!IsMaster) return;
		TargetDirectionInput = Input.GetVector("MoveLeft", "MoveRight", "MoveUp", "MoveDown");

		if (DateTime.Now < _lastMovementTick + _movementTickInterval) return;
		EmitSignal(SignalName.SPlayerMovement, TargetDirectionInput);
		_lastMovementTick = DateTime.Now;
	}
}
