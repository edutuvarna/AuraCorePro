using AuraCore.Application.Interfaces.Infrastructure;
namespace AuraCore.Infrastructure.Windows;

public sealed class WindowsServiceController : IServiceController
{
    public Task<ServiceState> GetStatusAsync(string serviceName, CancellationToken ct = default) => Task.FromResult(ServiceState.Unknown);
    public Task StartAsync(string serviceName, CancellationToken ct = default) => Task.CompletedTask;
    public Task StopAsync(string serviceName, CancellationToken ct = default) => Task.CompletedTask;
    public Task SetStartupTypeAsync(string serviceName, ServiceStartupType type, CancellationToken ct = default) => Task.CompletedTask;
}
