namespace AuraCore.API.Domain.Entities;

public sealed class User
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = "user";
    public string? TotpSecret { get; set; }
    public bool TotpEnabled { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    // Phase 6.11 additions
    public bool IsActive { get; set; } = true;
    public bool IsReadonly { get; set; } = false;
    public bool ForcePasswordChange { get; set; } = false;
    public DateTimeOffset? ForcePasswordChangeBy { get; set; }
    public DateTimeOffset? PasswordChangedAt { get; set; }
    public Guid? CreatedByUserId { get; set; }

    /// <summary>'signup' | 'admin_promote' | 'superadmin_create'</summary>
    public string CreatedVia { get; set; } = "signup";

    public bool Require2fa { get; set; } = false;

    public ICollection<License> Licenses { get; set; } = new List<License>();
    public ICollection<Subscription> Subscriptions { get; set; } = new List<Subscription>();
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}
