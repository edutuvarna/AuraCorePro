using StripeSubscription = Stripe.Subscription;
using DbSubscription = AuraCore.API.Domain.Entities.Subscription;
using AuraCore.API.Infrastructure.Data;
using AuraCore.API.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Stripe;
using Stripe.Checkout;

namespace AuraCore.API.Controllers.Payment;

[ApiController]
[Route("api/payment/stripe")]
public sealed class StripeController : ControllerBase
{
    private readonly AuraCoreDbContext _db;
    private readonly IConfiguration _config;

    public StripeController(AuraCoreDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    [HttpPost("create-session")]
    [Authorize]
    public async Task<IActionResult> CreateCheckoutSession([FromBody] CreateSessionRequest req, CancellationToken ct)
    {
        var userIdClaim = User.FindFirst("sub");
        if (userIdClaim is null || !Guid.TryParse(userIdClaim.Value, out var userId))
            return Unauthorized(new { error = "Invalid token" });
        var user = await _db.Users.FindAsync(new object[] { userId }, ct);
        if (user is null) return NotFound(new { error = "User not found" });
        var secretKey = _config["Stripe:SecretKey"];
        if (string.IsNullOrEmpty(secretKey))
            return StatusCode(503, new { error = "Stripe is not configured" });
        StripeConfiguration.ApiKey = secretKey;
        var (amount, description) = GetPricing(req.Tier, req.Plan, req.DeviceCount);
        var options = new SessionCreateOptions
        {
            PaymentMethodTypes = new List<string> { "card" },
            Mode = "subscription",
            CustomerEmail = user.Email,
            LineItems = new List<SessionLineItemOptions>
            {
                new SessionLineItemOptions
                {
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        Currency = "usd",
                        UnitAmount = (long)(amount * 100),
                        Recurring = new SessionLineItemPriceDataRecurringOptions { Interval = req.Plan == "yearly" ? "year" : "month" },
                        ProductData = new SessionLineItemPriceDataProductDataOptions { Name = $"AuraCore Pro - {req.Tier.ToUpper()}", Description = description }
                    },
                    Quantity = 1
                }
            },
            SuccessUrl = _config["Stripe:SuccessUrl"] ?? "https://auracore.pro?payment=success",
            CancelUrl = _config["Stripe:CancelUrl"] ?? "https://auracore.pro#pricing",
            Locale = "en",
            Metadata = new Dictionary<string, string> { { "userId", userId.ToString() }, { "tier", req.Tier }, { "plan", req.Plan }, { "deviceCount", req.DeviceCount.ToString() } }
        };
        try
        {
            var service = new SessionService();
            var session = await service.CreateAsync(options, cancellationToken: ct);
            _db.Payments.Add(new AuraCore.API.Domain.Entities.Payment { UserId = userId, Provider = "stripe", ExternalId = session.Id, Amount = amount, Currency = "USD", Plan = req.Plan, Tier = req.Tier, Status = "pending" });
            await _db.SaveChangesAsync(ct);
            return Ok(new { sessionId = session.Id, url = session.Url });
        }
        catch (StripeException ex)
        {
            return StatusCode(502, new { error = "Payment error: " + ex.Message });
        }
    }

    [HttpPost("webhook")]
    [AllowAnonymous]
    public async Task<IActionResult> Webhook(CancellationToken ct)
    {
        var webhookSecret = _config["Stripe:WebhookSecret"];
        if (string.IsNullOrEmpty(webhookSecret)) return BadRequest(new { error = "Webhook secret not configured" });
        using var reader = new StreamReader(Request.Body);
        var json = await reader.ReadToEndAsync(ct);
        Event stripeEvent;
        try { stripeEvent = EventUtility.ConstructEvent(json, Request.Headers["Stripe-Signature"], webhookSecret); }
        catch (StripeException) { return BadRequest(new { error = "Invalid signature" }); }
        switch (stripeEvent.Type)
        {
            case "checkout.session.completed": await HandleCheckoutCompleted(stripeEvent, ct); break;
            case "invoice.paid": await HandleInvoicePaid(stripeEvent, ct); break;
            case "customer.subscription.deleted": await HandleSubscriptionCancelled(stripeEvent, ct); break;
            case "customer.subscription.updated": await HandleSubscriptionUpdated(stripeEvent, ct); break;
        }
        return Ok(new { received = true });
    }

    [HttpGet("status/{paymentId:guid}")]
    [Authorize]
    public async Task<IActionResult> GetPaymentStatus(Guid paymentId, CancellationToken ct)
    {
        var userIdClaim = User.FindFirst("sub");
        if (userIdClaim is null || !Guid.TryParse(userIdClaim.Value, out var userId)) return Unauthorized();
        var payment = await _db.Payments.FirstOrDefaultAsync(p => p.Id == paymentId && p.UserId == userId, ct);
        if (payment is null) return NotFound();
        return Ok(new { id = payment.Id, status = payment.Status, tier = payment.Tier, plan = payment.Plan, amount = payment.Amount, completedAt = payment.CompletedAt });
    }

    [HttpPost("portal")]
    [Authorize]
    public async Task<IActionResult> CreatePortalSession(CancellationToken ct)
    {
        var userIdClaim = User.FindFirst("sub");
        if (userIdClaim is null || !Guid.TryParse(userIdClaim.Value, out var userId)) return Unauthorized();
        StripeConfiguration.ApiKey = _config["Stripe:SecretKey"];
        var sub = await _db.Subscriptions.FirstOrDefaultAsync(s => s.UserId == userId && s.Status == "active", ct);
        if (sub is null || string.IsNullOrEmpty(sub.StripeCustomerId)) return NotFound(new { error = "No active subscription" });
        var options = new Stripe.BillingPortal.SessionCreateOptions { Customer = sub.StripeCustomerId, ReturnUrl = "https://admin.auracore.pro" };
        var service = new Stripe.BillingPortal.SessionService();
        var session = await service.CreateAsync(options, cancellationToken: ct);
        return Ok(new { url = session.Url });
    }

    private static (decimal amount, string description) GetPricing(string tier, string plan, int deviceCount)
    {
        decimal basePrice, extraDevicePrice;
        if (tier == "enterprise") { basePrice = plan == "yearly" ? 129.99m : 12.99m; extraDevicePrice = plan == "yearly" ? 15.00m : 1.50m; }
        else { basePrice = plan == "yearly" ? 49.99m : 4.99m; extraDevicePrice = plan == "yearly" ? 20.00m : 2.00m; }
        var extra = Math.Max(0, deviceCount - 1);
        var total = basePrice + (extra * extraDevicePrice);
        var desc = $"{(tier == "enterprise" ? "Enterprise" : "Pro")} {(plan == "yearly" ? "Yearly" : "Monthly")} - {deviceCount} device(s)";
        return (total, desc);
    }

    private async Task HandleCheckoutCompleted(Event stripeEvent, CancellationToken ct)
    {
        var session = stripeEvent.Data.Object as Session;
        if (session is null) return;
        var tier = session.Metadata.GetValueOrDefault("tier", "pro");
        var plan = session.Metadata.GetValueOrDefault("plan", "monthly");
        int.TryParse(session.Metadata.GetValueOrDefault("deviceCount", "1"), out var deviceCount);
        if (deviceCount < 1) deviceCount = 1;
        var email = session.CustomerEmail ?? session.CustomerDetails?.Email;
        if (string.IsNullOrEmpty(email)) return;
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);
        if (user is null) return;
        var userId = user.Id;
        var expiresAt = plan switch { "yearly" => DateTimeOffset.UtcNow.AddYears(1), "lifetime" => DateTimeOffset.UtcNow.AddYears(100), _ => DateTimeOffset.UtcNow.AddMonths(1) };
        var amount = session.AmountTotal.HasValue ? session.AmountTotal.Value / 100m : 0;
        _db.Payments.Add(new AuraCore.API.Domain.Entities.Payment { UserId = userId, Provider = "stripe", ExternalId = session.Id, Amount = amount, Currency = "USD", Plan = plan, Tier = tier, Status = "completed", CompletedAt = DateTimeOffset.UtcNow });
        var license = await _db.Licenses.FirstOrDefaultAsync(l => l.UserId == userId && l.Status == "active", ct);
        if (license is not null) { license.Tier = tier; license.MaxDevices = Math.Max(deviceCount, 1); license.ExpiresAt = expiresAt; }
        else { _db.Licenses.Add(new License { UserId = userId, Key = Guid.NewGuid().ToString("N"), Tier = tier, MaxDevices = Math.Max(deviceCount, 1), ExpiresAt = expiresAt }); }
        if (session.Mode == "subscription" && !string.IsNullOrEmpty(session.SubscriptionId))
        {
            var sub = await _db.Subscriptions.FirstOrDefaultAsync(s => s.UserId == userId, ct);
            if (sub is not null) { sub.StripeSubscriptionId = session.SubscriptionId; sub.StripeCustomerId = session.CustomerId; sub.Plan = plan; sub.Status = "active"; sub.CurrentPeriodEnd = expiresAt; }
            else { _db.Subscriptions.Add(new DbSubscription { UserId = userId, StripeSubscriptionId = session.SubscriptionId, StripeCustomerId = session.CustomerId, Plan = plan, Status = "active", CurrentPeriodEnd = expiresAt }); }
        }
        await _db.SaveChangesAsync(ct);
    }

    private async Task HandleInvoicePaid(Event stripeEvent, CancellationToken ct)
    {
        var invoice = stripeEvent.Data.Object as Invoice;
        if (invoice is null) return;
        var sub = await _db.Subscriptions.FirstOrDefaultAsync(s => s.StripeCustomerId == invoice.CustomerId && s.Status == "active", ct);
        if (sub is null) return;
        sub.CurrentPeriodEnd = sub.Plan == "yearly" ? DateTimeOffset.UtcNow.AddYears(1) : DateTimeOffset.UtcNow.AddMonths(1);
        var license = await _db.Licenses.FirstOrDefaultAsync(l => l.UserId == sub.UserId && l.Status == "active", ct);
        if (license is not null) license.ExpiresAt = sub.CurrentPeriodEnd;
        _db.Payments.Add(new AuraCore.API.Domain.Entities.Payment { UserId = sub.UserId, Provider = "stripe", ExternalId = invoice.Id, Amount = (decimal)(invoice.AmountPaid / 100.0), Currency = invoice.Currency.ToUpper(), Plan = sub.Plan, Tier = license?.Tier ?? "pro", Status = "completed", CompletedAt = DateTimeOffset.UtcNow });
        await _db.SaveChangesAsync(ct);
    }

    private async Task HandleSubscriptionCancelled(Event stripeEvent, CancellationToken ct)
    {
        var subscription = stripeEvent.Data.Object as StripeSubscription;
        if (subscription is null) return;
        var sub = await _db.Subscriptions.FirstOrDefaultAsync(s => s.StripeSubscriptionId == subscription.Id, ct);
        if (sub is null) return;
        sub.Status = "cancelled";
        var license = await _db.Licenses.FirstOrDefaultAsync(l => l.UserId == sub.UserId && l.Status == "active", ct);
        if (license is not null) { license.Tier = "free"; license.MaxDevices = 1; }
        await _db.SaveChangesAsync(ct);
    }

    private async Task HandleSubscriptionUpdated(Event stripeEvent, CancellationToken ct)
    {
        if (stripeEvent.Data.Object is not StripeSubscription subscription) return;
        var sub = await _db.Subscriptions.FirstOrDefaultAsync(s => s.StripeSubscriptionId == subscription.Id, ct);
        if (sub is null) return;
        sub.Status = subscription.Status;
        sub.CurrentPeriodEnd = DateTimeOffset.UtcNow.AddMonths(1);
        await _db.SaveChangesAsync(ct);
    }
}

public sealed record CreateSessionRequest(string Tier = "pro", string Plan = "monthly", int DeviceCount = 1);
