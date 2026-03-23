namespace AuraCore.API.Domain.Entities;

public sealed class LoginAttempt
{
    public long Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public bool Success { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
