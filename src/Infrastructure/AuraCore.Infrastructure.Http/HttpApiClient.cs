using Microsoft.Extensions.DependencyInjection;
using AuraCore.Application.Interfaces.Infrastructure;
namespace AuraCore.Infrastructure.Http;

public sealed class HttpApiClient : IHttpApiClient
{
    public Task<T?> GetAsync<T>(string endpoint, CancellationToken ct = default) => Task.FromResult<T?>(default);
    public Task<TResponse?> PostAsync<TRequest, TResponse>(string endpoint, TRequest body, CancellationToken ct = default) => Task.FromResult<TResponse?>(default);
}

public static class HttpInfrastructureRegistration
{
    public static IServiceCollection AddHttpInfrastructure(this IServiceCollection services) => services.AddSingleton<IHttpApiClient, HttpApiClient>();
}
