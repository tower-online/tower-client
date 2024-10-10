using Godot;
using System;


namespace Tower.World;

[GlobalClass]
public partial class Zone : Node
{
    [Export] public string ZoneId { get; private set; }
    [Export] public ushort SizeX { get; private set; }
    [Export] public ushort SizeZ { get; private set; }
    [Export] public GridMap GridMap { get; private set; }

    public override void _Ready()
    {
        GridMap = GetNode<GridMap>("GridMap");
    }
}
