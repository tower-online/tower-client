using Godot;
using System;
using Tower.Entity;
using Tower.Network.Packet;
using Vector3 = Godot.Vector3;

namespace Tower.Player;

[GlobalClass]
public partial class PlayerBase : EntityBase
{
	[Signal]
	public delegate void SPlayerMovementEventHandler(Vector3 targetDirection);

	public event Action Attack1Event; 
	
	public bool IsMaster { get; set; } = false;
	public string CharacterName { get; set; }
	
	//TODO: Change to edge trigger
	private readonly TimeSpan _movementTickInterval = TimeSpan.FromMilliseconds(50);
	private DateTime _lastMovementTick = DateTime.Now;

	private AnimationTree _animationTree;
	private Label3D _healthLabel;
	
	public override void _Ready()
	{
		base._Ready();
		
		_animationTree = GetNode<AnimationTree>("Pivot/Character/AnimationTree");
		_healthLabel = GetNode<Label3D>("HealthLabel");
		GetNode<Label3D>("CharacterNameLabel").Text = CharacterName;

		ResourceModified += OnResourceModified;

		_healthLabel.Text = $"{Health} / {MaxHealth}";
	}

	public override void _PhysicsProcess(double delta)
	{
		base._PhysicsProcess(delta);
		HandleAnimations(delta);

		if (!IsMaster) return;
		if (DateTime.Now < _lastMovementTick + _movementTickInterval) return;
		
		var targetDirection = new Vector3(
			Input.GetAxis("MoveLeft", "MoveRight"),
			0,
			Input.GetAxis("MoveUp", "MoveDown"));
		if (!targetDirection.IsZeroApprox())
			targetDirection = targetDirection.Normalized();

		EmitSignal(SignalName.SPlayerMovement, targetDirection);
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

	public void HandleAttack1()
	{
		_animationTree?.Set("parameters/PunchingShot/request", (int)AnimationNodeOneShot.OneShotRequest.Fire);
	}

	private void HandleAnimations(double delta)
	{
		const double blendSpeed = 10.0;
		
		// If moving
		if (TargetDirection.IsZeroApprox())
		{
			_animationTree?.Set("parameters/Movement/transition_request", "Idle");
		}
		else
		{
			_animationTree?.Set("parameters/Movement/transition_request", "JogForward");
		}
	}

	private void OnResourceModified(EntityResourceType type, EntityResourceChangeMode mode, int amount)
	{
		if (type == EntityResourceType.HEALTH)
		{
			_healthLabel.Text = $"{Health} / {MaxHealth}";
		}
	}
}
