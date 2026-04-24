namespace AuraCore.API.Domain.Entities;

public sealed class AdminInvitation
{
    /// <summary>SHA256 hex hash of the raw token emailed to admin. Primary key.</summary>
    public string TokenHash { get; set; } = string.Empty;

    public Guid AdminUserId { get; set; }
    public User? AdminUser { get; set; }

    public Guid CreatedBy { get; set; }
    public User? CreatedByUser { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? ConsumedAt { get; set; }

    public bool IsValid() => ConsumedAt == null && ExpiresAt > DateTimeOffset.UtcNow;
}
