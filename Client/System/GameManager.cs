using Godot;
using Google.FlatBuffers;
using Tower.Entity;
using Tower.Network;
using tower.network.packet;
using Tower.World;

namespace Tower.System;

public partial class GameManager : Node
{
	public string Username { get; private set; }
	public string AuthToken { get; set; }

	private ConnectionManager _connectionManager;
	private EntityManager _entityManager;

	private Timer _pingTimer;

	public override void _Ready()
	{
		_connectionManager = GetNode<ConnectionManager>("/root/ConnectionManager");
		_connectionManager.Connection.ClientJoinResponseEvent += OnClientJoinResponse;
		_connectionManager.Connection.PlayerEnterZoneResponseEvent += OnPlayerEnterZoneResponse;

		_entityManager = GetNode<EntityManager>("/root/EntityManager");

		_pingTimer = GetNode<Timer>("PingTimer");
		_pingTimer.Timeout += () =>
		{
			GD.Print($"ping {_connectionManager.Connection.CurrentPing.Milliseconds}ms");
		};
			
#if TOWER_PLATFORM_TEST
		foreach (var arg in OS.GetCmdlineUserArgs())
		{
			var option = arg.Split('=');
			if (option[0].TrimPrefix("--") == "username")
			{
				Username = option[1];
				GD.Print($"[{nameof(GameManager)}] username={Username}");
			}
		}

		if (Username == null)
		{
			GD.PrintErr($"[{nameof(GameManager)}] username not set");
		}

#elif TOWER_PLATFORM_STEAM
		//TODO: Get username and infos from Steamworks SDK
#endif
	}

	private void OnClientJoinResponse(ClientJoinResponse response)
	{
		var location = response.CurrentLocation.Value;
		_entityManager.OnPlayerSpawn(response.Spawn.Value);
		GD.Print($"Player spawn on {location.Floor}/{location.ZoneId}");

		var builder = new FlatBufferBuilder(128);
		PlayerEnterZoneRequest.StartPlayerEnterZoneRequest(builder);
		PlayerEnterZoneRequest.AddLocation(builder, WorldLocation.CreateWorldLocation(builder, location.Floor, location.ZoneId));
		var request = PlayerEnterZoneRequest.EndPlayerEnterZoneRequest(builder);
		var packetBase = PacketBase.CreatePacketBase(builder, PacketType.PlayerEnterZoneRequest, request.Value);
		builder.FinishSizePrefixed(packetBase.Value);

		_connectionManager.Connection.SendPacket(builder.DataBuffer);
		
		_pingTimer.Start();
	}

	private void OnPlayerEnterZoneResponse(PlayerEnterZoneResponse response)
	{
		if (!response.Result)
		{
			GD.PrintErr("PlayerEnterZoneResponse: Fail");
			return;
		}
		
		var location = response.Location.Value;
		GD.Print($"Player enter zone {location.Floor}/{location.ZoneId}");
		
		
		var packedZone = GD.Load<PackedScene>($"res://World/Levels/Zone{location.ZoneId}.tscn");
		_entityManager.Clear();
		if (GetTree().ChangeSceneToPacked(packedZone) != Error.Ok)
		{
			GD.PrintErr("Error changing scene");
		}
	}
}
