namespace AuraCore.API.Domain.Entities;

public sealed class PermissionRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid AdminUserId { get; set; }
    public User? AdminUser { get; set; }

    public string PermissionKey { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;

    public DateTimeOffset RequestedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>pending | approved | denied | cancelled</summary>
    public string Status { get; set; } = "pending";

    public Guid? ReviewedBy { get; set; }
    public User? ReviewedByUser { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public string? ReviewNote { get; set; }
}
