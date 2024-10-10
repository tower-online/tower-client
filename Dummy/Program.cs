using Microsoft.Extensions.Logging;
using Tower.Dummy;

var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddConsole();
    builder.SetMinimumLevel(LogLevel.Debug);
});

var cts = new CancellationTokenSource();
Console.CancelKeyPress += (sender, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

// Instantiate and run dummy clients
Console.WriteLine($"{Settings.NumClients} dummy clients");

List<DummyClient> clients = [];
for (var i = 1; i < Settings.NumClients + 1; i++)
{
    clients.Add(new DummyClient($"dummy_{i:D5}", loggerFactory));
}

try
{
    await Task.WhenAll(clients.Select(client => client.Run(cts.Token)));
}
catch (OperationCanceledException)
{
}
finally
{
    foreach (var client in clients)
    {
        client.Stop();
    }
}

Console.WriteLine("Gracefully shutting down");