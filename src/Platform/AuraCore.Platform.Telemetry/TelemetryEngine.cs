using Microsoft.Extensions.DependencyInjection;
using AuraCore.Application.Interfaces.Platform;
namespace AuraCore.Platform.Telemetry;

public sealed class TelemetryEngine : ITelemetryEngine
{
    public void Track(TelemetryEvent evt) { }
    public void TrackMetric(string name, double value, IDictionary<string, string>? dimensions = null) { }
    public Task FlushAsync(CancellationToken ct = default) => Task.CompletedTask;
}

public static class TelemetryRegistration
{
    public static IServiceCollection AddTelemetryEngine(this IServiceCollection services) => services.AddSingleton<ITelemetryEngine, TelemetryEngine>();
}
