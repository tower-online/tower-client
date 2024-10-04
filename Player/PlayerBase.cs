using Godot;
using System;
using Tower.Entity;

namespace Tower.Player;

[GlobalClass]
public partial class PlayerBase : EntityBase
{
	[Signal]
	public delegate void SPlayerMovementEventHandler(Vector2 targetDirection);

	public event Action Attack1Event; 
	
	public bool IsMaster { get; set; } = false;
	public string CharacterName { get; set; }
	
	private Vector2 TargetDirectionInput { get; set; }
	//TODO: Change to edge trigger
	private readonly TimeSpan _movementTickInterval = TimeSpan.FromMilliseconds(50);
	private DateTime _lastMovementTick = DateTime.Now;

	private AnimationTree _animationTree;
	private double _runBlend;
	private static readonly StringName RunBlendParameter = "parameters/run_blend/blend_amount";
	
	public override void _Ready()
	{
		base._Ready();
		
		_animationTree = GetNode<AnimationTree>("Pivot/Character/AnimationTree");
		GetNode<Label3D>("CharacterNameLabel").Text = CharacterName;
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

	public override void _Input(InputEvent @event)
	{
		if (!IsMaster) return;
		
		if (@event.IsActionPressed("Attack1"))
		{
			Attack1Event?.Invoke();
		}
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

		_animationTree?.Set(RunBlendParameter, _runBlend);
	}
}
