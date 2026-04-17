using AuraCore.PrivilegedService.IPC;
using AuraCore.PrivilegedService.Ops;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSingleton<DriverOperations>();
builder.Services.AddSingleton<DefenderOperations>();
builder.Services.AddSingleton<ServiceOperations>();

builder.Services.AddSingleton(sp => new PipeServer(
    "AuraCorePro",
    sp.GetRequiredService<DriverOperations>(),
    sp.GetRequiredService<DefenderOperations>(),
    sp.GetRequiredService<ServiceOperations>(),
    sp.GetRequiredService<ILogger<PipeServer>>()));

builder.Services.AddHostedService<PipeServerHost>();

var app = builder.Build();
await app.RunAsync();

// ---------------------------------------------------------------------------
// Hosted-service wrapper — keeps Program.cs top-level and avoids a separate file.
// ---------------------------------------------------------------------------

internal sealed class PipeServerHost : BackgroundService
{
    private readonly PipeServer _server;

    public PipeServerHost(PipeServer server) => _server = server;

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
        => _server.RunAsync(stoppingToken);
}
