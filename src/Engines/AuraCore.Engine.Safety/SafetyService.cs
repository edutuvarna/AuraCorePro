using Microsoft.Extensions.DependencyInjection;
using AuraCore.Application.Interfaces.Engines;

namespace AuraCore.Engine.Safety;

public sealed class SafetyService : ISafetyService
{
    public Task<string> CreateRestorePointAsync(string description, CancellationToken ct = default)
        => Task.FromResult(Guid.NewGuid().ToString());
    public Task RollbackAsync(string operationId, CancellationToken ct = default)
        => Task.CompletedTask;
}

public static class SafetyRegistration
{
    public static IServiceCollection AddSafetyEngine(this IServiceCollection services)
        => services.AddSingleton<ISafetyService, SafetyService>();
}
