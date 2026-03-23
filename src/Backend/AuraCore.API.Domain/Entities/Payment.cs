namespace AuraCore.API.Domain.Entities;

public sealed class Payment
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Provider { get; set; } = "stripe"; // stripe, btc, usdt
    public string ExternalId { get; set; } = "";
    public string Status { get; set; } = "pending"; // pending, completed, failed, refunded
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
    public string Plan { get; set; } = "monthly";
    public string Tier { get; set; } = "pro";
    public string? CryptoAddress { get; set; }
    public string? CryptoTxHash { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }

    public User User { get; set; } = null!;
}
