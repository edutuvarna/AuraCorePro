namespace AuraCore.API.Domain.Entities;

public sealed class Subscription
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string StripeSubscriptionId { get; set; } = string.Empty;
    public string StripeCustomerId { get; set; } = string.Empty;
    public string Plan { get; set; } = "monthly";
    public string Status { get; set; } = "active";
    public DateTimeOffset CurrentPeriodEnd { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public User User { get; set; } = null!;
}
