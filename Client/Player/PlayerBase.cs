using Godot;
using Google.FlatBuffers;
using System;
using Tower.Entity;
using Tower.Network;
using tower.network.packet;
using Vector3 = Godot.Vector3;

namespace Tower.Player;

[GlobalClass]
public partial class PlayerBase : EntityBase
{
	public bool IsMaster { get; set; } = false;
	public string CharacterName { get; set; }
	
	//TODO: Change to edge trigger
	private readonly TimeSpan _movementTickInterval = TimeSpan.FromMilliseconds(50);
	private DateTime _lastMovementTick = DateTime.Now;

	private AnimationTree _animationTree;
	private Label3D _healthLabel;

	private Connection _connectionManager;
	
	public override void _Ready()
	{
		base._Ready();

		_connectionManager = GetNode<Connection>("/root/ConnectionManager");
		
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

		HandleMovement(targetDirection);
		_lastMovementTick = DateTime.Now;
	}

	public override void _Input(InputEvent @event)
	{
		if (!IsMaster) return;
		
		HandleAttack1();
	}
	
	private void HandleMovement(Vector3 targetDirection)
	{
		var builder = new FlatBufferBuilder(128);
		PlayerMovement.StartPlayerMovement(builder);
		PlayerMovement.AddX(builder, targetDirection.X);
		PlayerMovement.AddZ(builder, targetDirection.Z);
		var movement = PlayerMovement.EndPlayerMovement(builder);
		var packetBase = PacketBase.CreatePacketBase(builder, PacketType.PlayerMovement, movement.Value);
		builder.FinishSizePrefixed(packetBase.Value);

		_connectionManager.SendPacket(builder.DataBuffer);
	}

	private void HandleAttack1()
	{
		var builder = new FlatBufferBuilder(128);
		var attack = SkillMeleeAttack.CreateSkillMeleeAttack(builder);
		var packetBase = PacketBase.CreatePacketBase(builder, PacketType.SkillMeleeAttack, attack.Value);
		builder.FinishSizePrefixed(packetBase.Value);

		_connectionManager.SendPacket(builder.DataBuffer);
	}

	public void HandleAttack1Response()
	{
		_animationTree?.Set("parameters/PunchingShot/request", (int)AnimationNodeOneShot.OneShotRequest.Fire);
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

			if (amount < 0)
			{
				_animationTree?.Set("parameters/HitShot/request", (int)AnimationNodeOneShot.OneShotRequest.Fire);
			}
		}
	}
}
