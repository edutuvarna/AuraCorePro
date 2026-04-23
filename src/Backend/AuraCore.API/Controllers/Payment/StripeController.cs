using StripeSubscription = Stripe.Subscription;
using DbSubscription = AuraCore.API.Domain.Entities.Subscription;
using AuraCore.API.Hubs;
using AuraCore.API.Infrastructure.Data;
using AuraCore.API.Domain.Entities;
using AuraCore.API.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
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
    private readonly IHubContext<AdminHub> _hub;

    public StripeController(AuraCoreDbContext db, IConfiguration config, IHubContext<AdminHub> hub)
    {
        _db = db;
        _config = config;
        _hub = hub;
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
        var secretKey = Environment.GetEnvironmentVariable("STRIPE_SECRET_KEY") ?? _config["Stripe:SecretKey"];
        if (string.IsNullOrEmpty(secretKey) || secretKey == "LOADED_FROM_ENV")
            return StatusCode(503, new { error = "Stripe is not configured" });
        StripeConfiguration.ApiKey = secretKey;
        var validTiers = new[] { "pro", "enterprise" };
        if (!validTiers.Contains(req.Tier?.ToLower()))
            return BadRequest(new { error = "Invalid tier" });
        var validPlans = new[] { "monthly", "yearly" };
        if (!validPlans.Contains(req.Plan?.ToLower()))
            return BadRequest(new { error = "Invalid plan" });
        var currency = (req.Currency?.ToLower()) switch { "try" => "try", "eur" => "eur", _ => "usd" };
        var (amount, description) = GetPricing(req.Tier, req.Plan, req.DeviceCount, currency);
        var locale = currency == "try" ? "tr" : "en";
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
                        Currency = currency,
                        UnitAmount = (long)(amount * 100),
                        Recurring = new SessionLineItemPriceDataRecurringOptions { Interval = req.Plan == "yearly" ? "year" : "month" },
                        ProductData = new SessionLineItemPriceDataProductDataOptions { Name = $"AuraCore Pro - {req.Tier.ToUpper()}", Description = description }
                    },
                    Quantity = 1
                }
            },
            SuccessUrl = _config["Stripe:SuccessUrl"] ?? "https://auracore.pro?payment=success",
            CancelUrl = _config["Stripe:CancelUrl"] ?? "https://auracore.pro#pricing",
            Locale = locale,
            Metadata = new Dictionary<string, string> { { "userId", userId.ToString() }, { "tier", req.Tier }, { "plan", req.Plan }, { "deviceCount", req.DeviceCount.ToString() }, { "currency", currency } }
        };
        try
        {
            var service = new SessionService();
            var session = await service.CreateAsync(options, cancellationToken: ct);
            _db.Payments.Add(new AuraCore.API.Domain.Entities.Payment { UserId = userId, Provider = "stripe", ExternalId = session.Id, Amount = amount, Currency = currency.ToUpper(), Plan = req.Plan, Tier = req.Tier, Status = "pending" });
            await _db.SaveChangesAsync(ct);
            return Ok(new { sessionId = session.Id, url = session.Url });
        }
        catch (StripeException ex)
        {
            return StatusCode(502, new { error = "Payment error: " + ex.Message });
        }
    }

    [HttpPost("guest-checkout")]
    [AllowAnonymous]
    public async Task<IActionResult> GuestCheckout([FromBody] GuestCheckoutRequest req, CancellationToken ct)
    {
        var secretKey = Environment.GetEnvironmentVariable("STRIPE_SECRET_KEY") ?? _config["Stripe:SecretKey"];
        if (string.IsNullOrEmpty(secretKey) || secretKey == "LOADED_FROM_ENV")
            return StatusCode(503, new { error = "Stripe is not configured" });
        StripeConfiguration.ApiKey = secretKey;

        var validTiers = new[] { "pro", "enterprise" };
        if (!validTiers.Contains(req.Tier?.ToLower()))
            return BadRequest(new { error = "Invalid tier" });
        var validPlans = new[] { "monthly", "yearly" };
        if (!validPlans.Contains(req.Plan?.ToLower()))
            return BadRequest(new { error = "Invalid plan" });

        var currency = (req.Currency?.ToLower()) switch { "try" => "try", "eur" => "eur", _ => "usd" };
        var (amount, description) = GetPricing(req.Tier, req.Plan, req.DeviceCount, currency);
        var locale = currency == "try" ? "tr" : "en";

        var options = new SessionCreateOptions
        {
            PaymentMethodTypes = new List<string> { "card" },
            Mode = "subscription",
            LineItems = new List<SessionLineItemOptions>
            {
                new SessionLineItemOptions
                {
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        Currency = currency,
                        UnitAmount = (long)(amount * 100),
                        Recurring = new SessionLineItemPriceDataRecurringOptions { Interval = req.Plan == "yearly" ? "year" : "month" },
                        ProductData = new SessionLineItemPriceDataProductDataOptions { Name = $"AuraCore Pro - {req.Tier.ToUpper()}", Description = description }
                    },
                    Quantity = 1
                }
            },
            SuccessUrl = _config["Stripe:SuccessUrl"] ?? "https://auracore.pro?payment=success",
            CancelUrl = _config["Stripe:CancelUrl"] ?? "https://auracore.pro#pricing",
            Locale = locale,
            Metadata = new Dictionary<string, string> { { "tier", req.Tier }, { "plan", req.Plan }, { "deviceCount", req.DeviceCount.ToString() }, { "currency", currency }, { "source", "website" } }
        };

        try
        {
            var service = new SessionService();
            var session = await service.CreateAsync(options, cancellationToken: ct);
            return Ok(new { sessionId = session.Id, url = session.Url });
        }
        catch (StripeException ex)
        {
            return StatusCode(502, new { error = "Payment error: " + ex.Message });
        }
    }

    [HttpPost("webhook")]
    [AllowAnonymous]
    [AuraCore.API.Filters.AuditAction("StripeWebhookEvent", "Payment")]
    public async Task<IActionResult> Webhook(CancellationToken ct)
    {
        var webhookSecret = Environment.GetEnvironmentVariable("STRIPE_WEBHOOK_SECRET") ?? _config["Stripe:WebhookSecret"];
        if (string.IsNullOrEmpty(webhookSecret) || webhookSecret == "LOADED_FROM_ENV") return BadRequest(new { error = "Webhook secret not configured" });

        var signature = Request.Headers["Stripe-Signature"].ToString();
        if (string.IsNullOrEmpty(signature)) return BadRequest(new { error = "Missing signature" });

        using var reader = new StreamReader(Request.Body);
        var json = await reader.ReadToEndAsync(ct);
        Event stripeEvent;
        try { stripeEvent = EventUtility.ConstructEvent(json, signature, webhookSecret); }
        catch (StripeException) { return BadRequest(new { error = "Invalid signature" }); }
        catch (NullReferenceException) { return BadRequest(new { error = "Malformed webhook payload" }); }
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
        StripeConfiguration.ApiKey = Environment.GetEnvironmentVariable("STRIPE_SECRET_KEY") ?? _config["Stripe:SecretKey"];
        var sub = await _db.Subscriptions.FirstOrDefaultAsync(s => s.UserId == userId && s.Status == "active", ct);
        if (sub is null || string.IsNullOrEmpty(sub.StripeCustomerId)) return NotFound(new { error = "No active subscription" });
        var options = new Stripe.BillingPortal.SessionCreateOptions { Customer = sub.StripeCustomerId, ReturnUrl = "https://admin.auracore.pro" };
        var service = new Stripe.BillingPortal.SessionService();
        var session = await service.CreateAsync(options, cancellationToken: ct);
        return Ok(new { url = session.Url });
    }

    private static (decimal amount, string description) GetPricing(string tier, string plan, int deviceCount, string currency = "usd")
    {
        decimal basePrice, extraDevicePrice;
        if (currency == "try")
        {
            // TRY pricing
            if (tier == "enterprise") { basePrice = plan == "yearly" ? 3590m : 449m; extraDevicePrice = plan == "yearly" ? 312m : 39m; }
            else { basePrice = plan == "yearly" ? 1190m : 149m; extraDevicePrice = plan == "yearly" ? 472m : 59m; }
        }
        else
        {
            // USD pricing
            if (tier == "enterprise") { basePrice = plan == "yearly" ? 129.99m : 12.99m; extraDevicePrice = plan == "yearly" ? 15.00m : 1.50m; }
            else { basePrice = plan == "yearly" ? 49.99m : 4.99m; extraDevicePrice = plan == "yearly" ? 20.00m : 2.00m; }
        }
        var extra = Math.Max(0, deviceCount - 1);
        var total = basePrice + (extra * extraDevicePrice);
        var cur = currency.ToUpper();
        var desc = $"{(tier == "enterprise" ? "Enterprise" : "Pro")} {(plan == "yearly" ? "Yearly" : "Monthly")} - {deviceCount} device(s) ({cur})";
        return (total, desc);
    }

    private async Task HandleCheckoutCompleted(Event stripeEvent, CancellationToken ct)
    {
        var session = stripeEvent.Data.Object as Session;
        if (session is null) return;

        // Idempotency guard: Stripe may retry webhooks. Skip if we've already recorded
        // a completed payment for this session. Prevents duplicate Payment/License/Subscription rows.
        var alreadyProcessed = await _db.Payments
            .AnyAsync(p => p.ExternalId == session.Id && p.Status == "completed", ct);
        if (alreadyProcessed) return;

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
        // T1.12 fix: derive currency from session metadata (populated by CreateCheckoutSession).
        // Fallback to session.Currency (Stripe-provided 3-letter lowercase) or "USD".
        var paymentCurrency = session.Metadata.TryGetValue("currency", out var c) && !string.IsNullOrEmpty(c)
            ? c.ToUpperInvariant()
            : (!string.IsNullOrEmpty(session.Currency) ? session.Currency.ToUpperInvariant() : "USD");
        _db.Payments.Add(new AuraCore.API.Domain.Entities.Payment { UserId = userId, Provider = "stripe", ExternalId = session.Id, Amount = amount, Currency = paymentCurrency, Plan = plan, Tier = tier, Status = "completed", CompletedAt = DateTimeOffset.UtcNow });
        var license = await _db.Licenses.FirstOrDefaultAsync(l => l.UserId == userId && l.Status == "active", ct);
        if (license is not null) { license.Tier = tier; license.MaxDevices = Math.Max(deviceCount, 1); license.ExpiresAt = expiresAt; }
        else { _db.Licenses.Add(new License { UserId = userId, Key = LicenseKeyGenerator.Generate(), Tier = tier, MaxDevices = Math.Max(deviceCount, 1), ExpiresAt = expiresAt }); }
        if (session.Mode == "subscription" && !string.IsNullOrEmpty(session.SubscriptionId))
        {
            var sub = await _db.Subscriptions.FirstOrDefaultAsync(s => s.UserId == userId, ct);
            if (sub is not null) { sub.StripeSubscriptionId = session.SubscriptionId; sub.StripeCustomerId = session.CustomerId; sub.Plan = plan; sub.Status = "active"; sub.CurrentPeriodEnd = expiresAt; }
            else { _db.Subscriptions.Add(new DbSubscription { UserId = userId, StripeSubscriptionId = session.SubscriptionId, StripeCustomerId = session.CustomerId, Plan = plan, Status = "active", CurrentPeriodEnd = expiresAt }); }
        }
        await _db.SaveChangesAsync(ct);

        // Phase 6.10 Task 19: broadcast completed payment to admin dashboard
        await _hub.Clients.Group("admins").SendAsync("Payment", new
        {
            email = user.Email,
            amount,
            currency = paymentCurrency,
            plan,
            createdAt = DateTimeOffset.UtcNow
        }, ct);
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
        // T2.14 fix: use decimal arithmetic (/100m) not float /100.0. Currency already
        // comes from Stripe's invoice object so it's already the correct ISO code.
        _db.Payments.Add(new AuraCore.API.Domain.Entities.Payment { UserId = sub.UserId, Provider = "stripe", ExternalId = invoice.Id, Amount = invoice.AmountPaid / 100m, Currency = (invoice.Currency ?? "usd").ToUpperInvariant(), Plan = sub.Plan, Tier = license?.Tier ?? "pro", Status = "completed", CompletedAt = DateTimeOffset.UtcNow });
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

public sealed record CreateSessionRequest(string Tier = "pro", string Plan = "monthly", int DeviceCount = 1, string Currency = "usd");
public sealed record GuestCheckoutRequest(string Tier = "pro", string Plan = "monthly", int DeviceCount = 1, string Currency = "usd");
