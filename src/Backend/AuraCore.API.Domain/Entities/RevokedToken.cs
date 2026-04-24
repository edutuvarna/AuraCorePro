namespace AuraCore.API.Domain.Entities;

public sealed class RevokedToken
{
    /// <summary>JWT 'jti' (unique ID) claim. Primary key.</summary>
    public string Jti { get; set; } = string.Empty;

    public Guid UserId { get; set; }
    public User? User { get; set; }

    public DateTimeOffset RevokedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid? RevokedBy { get; set; }
    public User? RevokedByUser { get; set; }

    /// <summary>'suspend' | 'password_reset' | 'logout_all' | 'admin_deleted' | 'logout'</summary>
    public string RevokeReason { get; set; } = string.Empty;
}
