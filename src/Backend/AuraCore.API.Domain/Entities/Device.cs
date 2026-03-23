namespace AuraCore.API.Domain.Entities;

public sealed class Device
{
    public Guid Id { get; set; }
    public Guid LicenseId { get; set; }
    public string HardwareFingerprint { get; set; } = string.Empty;
    public string MachineName { get; set; } = string.Empty;
    public string OsVersion { get; set; } = string.Empty;
    public DateTimeOffset LastSeenAt { get; set; }
    public DateTimeOffset RegisteredAt { get; set; }

    public License License { get; set; } = null!;
    public ICollection<TelemetryEvent> TelemetryEvents { get; set; } = new List<TelemetryEvent>();
    public ICollection<CrashReport> CrashReports { get; set; } = new List<CrashReport>();
}
