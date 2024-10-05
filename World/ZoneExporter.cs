using Godot;
using Google.FlatBuffers;
using System;

using Tower.World.Data;

namespace Tower.World;

public partial class ZoneExporter : Node
{
	[Export] private Godot.Collections.Array<PackedScene> Zones { get; set; }
	
	public override void _Ready()
	{
		GD.Print($"[{nameof(ZoneExporter)}] Exporting...");
		
		foreach (var packedZone in Zones)
		{
			var zone = packedZone.Instantiate<Zone>();
			GD.Print($"{zone.ZoneId} ({zone.SizeX},{zone.SizeZ})");
			
			var builder = new FlatBufferBuilder(1024);
			
			ZoneData.StartGridVector(builder, zone.SizeX * zone.SizeZ);
			for (var x = 0; x < zone.SizeX; x += 1)
			{
				for (var z = 0; z < zone.SizeZ; z += 1)
				{
					// TODO: Determine whether the object is obstacle or not
					var isBlocked = zone.GridMap.GetCellItem(new Vector3I(x, 0, z)) != GridMap.InvalidCellItem;
					Cell.CreateCell(builder, isBlocked);
				}
			}
			var grid = builder.EndVector();
			
			var zoneId = builder.CreateString(zone.ZoneId);
			
			ZoneData.StartZoneData(builder);
			ZoneData.AddZoneId(builder, zoneId);
			ZoneData.AddSizeX(builder, zone.SizeX);
			ZoneData.AddSizeZ(builder, zone.SizeZ);
			ZoneData.AddGrid(builder, grid);

			var zoneData = ZoneData.EndZoneData(builder);
			builder.Finish(zoneData.Value);

			// if (!ZoneData.VerifyZoneData(new ByteBuffer(builder.SizedByteArray())))
			// {
			// 	throw new Exception("Failed to verify ZoneData buffer");
			// }

			var path = $"res://bin/levels/{zone.ZoneId}.bin";
			using var file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
			if (file is null)
			{
				GD.PrintErr(FileAccess.GetOpenError());
				continue;
			}
			file.StoreBuffer(builder.SizedByteArray());
			
			GD.Print(path);
			
			zone.QueueFree();
		}
		
		GD.Print($"[{nameof(ZoneExporter)}] Exporting done.");
		GetTree().Quit();
	}
}
