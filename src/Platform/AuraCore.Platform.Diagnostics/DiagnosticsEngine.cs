using Microsoft.Extensions.DependencyInjection;
using AuraCore.Application.Interfaces.Platform;
namespace AuraCore.Platform.Diagnostics;

public sealed class DiagnosticsEngine : IDiagnosticsEngine
{
    public Task<DiagnosticReport> RunFullDiagnosticAsync(CancellationToken ct = default)
        => Task.FromResult(new DiagnosticReport(Array.Empty<HealthCheckResult>(), DateTimeOffset.UtcNow));
    public Task<IReadOnlyList<HealthCheckResult>> RunHealthChecksAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<HealthCheckResult>>(Array.Empty<HealthCheckResult>());
}

public static class DiagnosticsRegistration
{
    public static IServiceCollection AddDiagnosticsEngine(this IServiceCollection services) => services.AddSingleton<IDiagnosticsEngine, DiagnosticsEngine>();
}
