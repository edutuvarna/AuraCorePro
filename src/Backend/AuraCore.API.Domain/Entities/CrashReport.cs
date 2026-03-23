namespace AuraCore.API.Domain.Entities;

public sealed class CrashReport
{
    public Guid Id { get; set; }
    public Guid DeviceId { get; set; }
    public string AppVersion { get; set; } = string.Empty;
    public string ExceptionType { get; set; } = string.Empty;
    public string StackTrace { get; set; } = string.Empty;
    public string SystemInfo { get; set; } = "{}";
    public DateTimeOffset CreatedAt { get; set; }

    public Device Device { get; set; } = null!;
}
