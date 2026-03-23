namespace AuraCore.Application.Interfaces.Infrastructure;

public interface IHttpApiClient
{
    Task<T?> GetAsync<T>(string endpoint, CancellationToken ct = default);
    Task<TResponse?> PostAsync<TRequest, TResponse>(string endpoint, TRequest body, CancellationToken ct = default);
}
