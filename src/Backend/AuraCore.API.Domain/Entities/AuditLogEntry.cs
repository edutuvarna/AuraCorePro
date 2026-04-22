namespace AuraCore.API.Domain.Entities;

public sealed class AuditLogEntry
{
    public long Id { get; set; }
    public Guid? ActorId { get; set; }
    public string ActorEmail { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string TargetType { get; set; } = string.Empty;
    public string? TargetId { get; set; }
    public string? BeforeData { get; set; }   // jsonb serialized
    public string? AfterData { get; set; }    // jsonb serialized
    public string? IpAddress { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public User? Actor { get; set; }
}
