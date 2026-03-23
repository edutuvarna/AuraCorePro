namespace AuraCore.Application.Interfaces.IPC;

public interface IPrivilegedIpcClient
{
    Task<TResponse> SendAsync<TRequest, TResponse>(TRequest request, CancellationToken ct = default)
        where TRequest : class
        where TResponse : class;
    Task<bool> PingAsync(CancellationToken ct = default);
}
