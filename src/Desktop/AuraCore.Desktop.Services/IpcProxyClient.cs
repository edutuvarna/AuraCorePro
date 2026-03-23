using Microsoft.Extensions.DependencyInjection;
using AuraCore.Application.Interfaces.IPC;
namespace AuraCore.Desktop.Services;

public sealed class IpcProxyClient : IPrivilegedIpcClient
{
    public Task<TResponse> SendAsync<TRequest, TResponse>(TRequest request, CancellationToken ct = default)
        where TRequest : class where TResponse : class
        => throw new NotImplementedException("IPC not yet implemented.");
    public Task<bool> PingAsync(CancellationToken ct = default) => Task.FromResult(false);
}

public static class DesktopServicesRegistration
{
    public static IServiceCollection AddDesktopServices(
        this IServiceCollection services)
        => services.AddSingleton<IPrivilegedIpcClient, IpcProxyClient>();
}
