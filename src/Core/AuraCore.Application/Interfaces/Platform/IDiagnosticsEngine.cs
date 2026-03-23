namespace AuraCore.Application.Interfaces.Platform;

public interface IDiagnosticsEngine
{
    Task<DiagnosticReport> RunFullDiagnosticAsync(CancellationToken ct = default);
    Task<IReadOnlyList<HealthCheckResult>> RunHealthChecksAsync(CancellationToken ct = default);
}

public sealed record DiagnosticReport(
    IReadOnlyList<HealthCheckResult> Checks,
    DateTimeOffset Timestamp);

public sealed record HealthCheckResult(
    string Name,
    HealthStatus Status,
    string? Message = null);

public enum HealthStatus { Healthy, Degraded, Unhealthy }
