namespace AuraCore.Application.Interfaces.Infrastructure;

public interface IServiceController
{
    Task<ServiceState> GetStatusAsync(string serviceName, CancellationToken ct = default);
    Task StartAsync(string serviceName, CancellationToken ct = default);
    Task StopAsync(string serviceName, CancellationToken ct = default);
    Task SetStartupTypeAsync(string serviceName, ServiceStartupType type, CancellationToken ct = default);
}

public enum ServiceState { Running, Stopped, Paused, Unknown }
public enum ServiceStartupType { Automatic, Manual, Disabled }
