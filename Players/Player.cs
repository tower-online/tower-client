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

	private AnimationTree _animationTree;
	private double _runBlend;
	private static readonly StringName RunBlendParameter = "parameters/run_blend/blend_amount";
	
	public override void _Ready()
	{
		_animationTree = GetNode<AnimationTree>("AnimationTree");
	}

	public override void _PhysicsProcess(double delta)
	{
		base._PhysicsProcess(delta);
		HandleAnimations(delta);

		if (!IsMaster) return;
		TargetDirectionInput = Input.GetVector("MoveLeft", "MoveRight", "MoveUp", "MoveDown");

		if (DateTime.Now < _lastMovementTick + _movementTickInterval) return;
		EmitSignal(SignalName.SPlayerMovement, TargetDirectionInput);
		_lastMovementTick = DateTime.Now;
	}

	private void HandleAnimations(double delta)
	{
		const double blendSpeed = 10.0;
		
		// If moving
		if (TargetDirection.IsZeroApprox())
		{
			_runBlend = Mathf.Lerp(_runBlend, 1.0, blendSpeed * delta);
		}
		else
		{
			_runBlend = Mathf.Lerp(_runBlend, 0.0, blendSpeed * delta);
		}

		_animationTree.Set(RunBlendParameter, _runBlend);
	}
}
