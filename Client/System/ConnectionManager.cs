using System.Threading;
using System.Threading.Tasks;
using Godot;
using Tower.Network;

namespace Tower.System;

public partial class ConnectionManager : Node
{
    public Connection Connection { get; } = new(new GodotLogger(nameof(ConnectionManager)));

    private readonly CancellationTokenSource _cts = new();

    public override void _Ready()
    {
        Connection.Disconnected += OnDisconnected;
    }

    public override void _Process(double _)
    {
        if (!Connection.ReceiveBufferBlock.TryReceiveAll(out var buffers)) return;
        foreach (var buffer in buffers)
        {
            Connection.HandlePacket(buffer);
        }
    }

    public void Run()
    {
        _ = Task.Run(async () =>
        {
            await Connection.Run(_cts.Token);
        });
    }

    public void Stop()
    {
        _cts.Cancel();
        Connection.Disconnect();
    }

    private void OnDisconnected()
    {
        GD.Print("Disconnected");
    }
}