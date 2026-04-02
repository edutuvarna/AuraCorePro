namespace AuraCore.API.Domain.Entities;

public sealed class AppConfig
{
    public int Id { get; set; } = 1; // Singleton row
    public bool IsMaintenanceMode { get; set; }
    public string MaintenanceMessage { get; set; } = "";
    public bool NewRegistrations { get; set; } = true;
    public bool TelemetryEnabled { get; set; } = true;
    public bool CrashReportsEnabled { get; set; } = true;
    public bool AutoUpdateEnabled { get; set; } = true;
    public DateTimeOffset LastUpdated { get; set; } = DateTimeOffset.UtcNow;
}
