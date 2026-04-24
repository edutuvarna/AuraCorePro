using AuraCore.API.Domain.Entities;
using AuraCore.API.Filters;
using AuraCore.API.Helpers;
using AuraCore.API.Hubs;
using AuraCore.API.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace AuraCore.API.Controllers.Payment;

[ApiController]
[Route("api/payment/crypto")]
public sealed class CryptoController : ControllerBase
{
    private readonly AuraCoreDbContext _db;
    private readonly IConfiguration _config;
    private readonly IHubContext<AdminHub> _hub;

    public CryptoController(AuraCoreDbContext db, IConfiguration config, IHubContext<AdminHub> hub)
    {
        _db = db;
        _config = config;
        _hub = hub;
    }

    /// <summary>Generate a crypto payment address for BTC or USDT</summary>
    [HttpPost("create")]
    [Authorize]
    public async Task<IActionResult> CreateCryptoPayment([FromBody] CryptoPaymentRequest req, CancellationToken ct)
    {
        var userIdClaim = User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized();

        var amount = req.Plan == "yearly"
            ? (req.Tier == "enterprise" ? 129.99m : 49.99m)
            : (req.Tier == "enterprise" ? 12.99m : 4.99m);

        // In production: use a crypto payment gateway (NOWPayments, CoinGate, BTCPay Server)
        // to generate a unique address per payment
        var walletAddress = req.Crypto switch
        {
            "btc" => _config["Crypto:BTC:Address"] ?? "bc1q_YOUR_BTC_ADDRESS_HERE",
            "usdt_trc20" => _config["Crypto:USDT_TRC20:Address"] ?? "T_YOUR_TRC20_ADDRESS_HERE",
            "usdt_erc20" => _config["Crypto:USDT_ERC20:Address"] ?? "0x_YOUR_ERC20_ADDRESS_HERE",
            _ => ""
        };

        var payment = new AuraCore.API.Domain.Entities.Payment
        {
            UserId = userId,
            Provider = req.Crypto,
            Amount = amount,
            Currency = req.Crypto == "btc" ? "BTC" : "USDT",
            Plan = req.Plan,
            Tier = req.Tier,
            Status = "awaiting_payment",
            CryptoAddress = walletAddress
        };
        _db.Payments.Add(payment);
        await _db.SaveChangesAsync(ct);

        return Ok(new
        {
            paymentId = payment.Id,
            address = walletAddress,
            amount,
            currency = payment.Currency,
            network = req.Crypto switch
            {
                "btc" => "Bitcoin",
                "usdt_trc20" => "Tron (TRC-20)",
                "usdt_erc20" => "Ethereum (ERC-20)",
                _ => "Unknown"
            },
            instructions = $"Send exactly {amount} {payment.Currency} to the address above. " +
                $"After sending, submit the transaction hash to confirm.",
            expiresIn = "30 minutes"
        });
    }

    /// <summary>User submits TX hash after sending crypto payment</summary>
    [HttpPost("confirm/{paymentId:guid}")]
    [Authorize]
    public async Task<IActionResult> ConfirmCryptoPayment(
        Guid paymentId, [FromBody] ConfirmCryptoRequest req, CancellationToken ct)
    {
        var userIdClaim = User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized();
        var payment = await _db.Payments.FirstOrDefaultAsync(
            p => p.Id == paymentId && p.UserId == userId, ct);

        if (payment is null) return NotFound();

        payment.CryptoTxHash = req.TxHash;
        payment.Status = "confirming"; // Admin needs to verify the TX

        await _db.SaveChangesAsync(ct);

        return Ok(new
        {
            status = "confirming",
            message = "Payment submitted. We will verify the transaction and activate your subscription within 1 hour. " +
                "For instant activation, use Stripe."
        });
    }

    /// <summary>Admin verifies and activates a crypto payment</summary>
    [HttpPost("admin/verify/{paymentId:guid}")]
    [Authorize(Roles = "admin")]
    [RequiresPermission(PermissionKeys.ActionPaymentsApproveCrypto)]
    public async Task<IActionResult> AdminVerifyPayment(Guid paymentId, CancellationToken ct)
    {
        var payment = await _db.Payments.Include(p => p.User).FirstOrDefaultAsync(p => p.Id == paymentId, ct);
        if (payment is null) return NotFound();

        payment.Status = "completed";
        payment.CompletedAt = DateTimeOffset.UtcNow;

        // Activate license
        var license = await _db.Licenses.FirstOrDefaultAsync(l => l.UserId == payment.UserId && l.Status == "active", ct);
        if (license is not null)
        {
            license.Tier = payment.Tier;
            license.ExpiresAt = payment.Plan == "yearly"
                ? DateTimeOffset.UtcNow.AddYears(1)
                : DateTimeOffset.UtcNow.AddMonths(1);
        }
        else
        {
            _db.Licenses.Add(new License
            {
                UserId = payment.UserId,
                Key = LicenseKeyGenerator.Generate(),
                Tier = payment.Tier,
                MaxDevices = payment.Tier == "enterprise" ? 5 : 1,
                ExpiresAt = payment.Plan == "yearly"
                    ? DateTimeOffset.UtcNow.AddYears(1)
                    : DateTimeOffset.UtcNow.AddMonths(1)
            });
        }

        await _db.SaveChangesAsync(ct);

        // Phase 6.10 Task 19: broadcast verified crypto payment to admin dashboard
        await _hub.Clients.Group("admins").SendAsync("Payment", new
        {
            email = payment.User?.Email ?? "(unknown)",
            amount = payment.Amount,
            currency = payment.Currency,
            plan = payment.Plan,
            createdAt = DateTimeOffset.UtcNow
        }, ct);

        return Ok(new { status = "activated", userEmail = payment.User.Email, tier = payment.Tier });
    }

    [HttpPost("admin/reject/{paymentId:guid}")]
    [Authorize(Roles = "admin")]
    [RequiresPermission(PermissionKeys.ActionPaymentsRejectCrypto)]
    [AuraCore.API.Filters.AuditAction("RejectCryptoPayment", "Payment", TargetIdFromRouteKey = "paymentId")]
    public async Task<IActionResult> AdminRejectPayment(Guid paymentId, CancellationToken ct)
    {
        var payment = await _db.Payments.FirstOrDefaultAsync(p => p.Id == paymentId, ct);
        if (payment is null) return NotFound(new { error = "Payment not found" });

        if (payment.Status is not "pending" and not "confirming")
            return BadRequest(new { error = $"Cannot reject payment in status '{payment.Status}'" });

        payment.Status = "rejected";
        await _db.SaveChangesAsync(ct);
        return Ok(new { message = "Payment rejected", payment.Id, payment.Status });
    }
}

public sealed record CryptoPaymentRequest(string Crypto, string Tier, string Plan);
public sealed record ConfirmCryptoRequest(string TxHash);
