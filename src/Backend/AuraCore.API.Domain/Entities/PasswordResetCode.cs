namespace AuraCore.API.Domain.Entities;

public sealed class PasswordResetCode
{
    public int Id { get; set; }
    public string Email { get; set; } = "";
    public string Code { get; set; } = "";
    public DateTimeOffset ExpiresAt { get; set; }
    public bool Used { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
