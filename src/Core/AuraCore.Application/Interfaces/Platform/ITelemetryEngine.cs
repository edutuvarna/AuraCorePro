namespace AuraCore.Application.Interfaces.Platform;

public interface ITelemetryEngine
{
    void Track(TelemetryEvent evt);
    void TrackMetric(string name, double value, IDictionary<string, string>? dimensions = null);
    Task FlushAsync(CancellationToken ct = default);
}

public record TelemetryEvent(string EventType, IDictionary<string, string>? Properties = null);
