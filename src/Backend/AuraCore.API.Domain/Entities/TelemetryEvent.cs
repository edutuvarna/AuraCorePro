namespace AuraCore.API.Domain.Entities;

public sealed class TelemetryEvent
{
    public long Id { get; set; }
    public Guid DeviceId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string EventData { get; set; } = "{}";
    public string? SessionId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public Device Device { get; set; } = null!;
}
