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

	private Connection _connectionManager;
	private EntityManager _entityManager;

	public override void _Ready()
	{
		_connectionManager = GetNode<Connection>("/root/ConnectionManager");
		_connectionManager.ClientJoinResponseEvent += OnClientJoinResponse;
		_connectionManager.PlayerEnterZoneResponseEvent += OnPlayerEnterZoneResponse;

		_entityManager = GetNode<EntityManager>("/root/EntityManager");
		
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

		_connectionManager.SendPacket(builder.DataBuffer);
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
