namespace AuraCore.API.Domain.Entities;

public sealed class PermissionGrant
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid AdminUserId { get; set; }
    public User? AdminUser { get; set; }

    /// <summary>Permission key, e.g. "tab:configuration" or "action:users.delete".</summary>
    public string PermissionKey { get; set; } = string.Empty;

    public Guid GrantedBy { get; set; }
    public User? GrantedByUser { get; set; }

    public DateTimeOffset GrantedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ExpiresAt { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }
    public Guid? RevokedBy { get; set; }
    public User? RevokedByUser { get; set; }
    public string? RevokeReason { get; set; }

    public Guid? SourceRequestId { get; set; }
    public PermissionRequest? SourceRequest { get; set; }

    public bool IsActive() => RevokedAt == null && (ExpiresAt == null || ExpiresAt > DateTimeOffset.UtcNow);
}
