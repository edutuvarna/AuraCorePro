using AuraCore.API.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuraCore.API.Controllers.Admin;

[ApiController]
[Route("api/admin/dashboard")]
[Authorize(Roles = "admin")]
public sealed class AdminDashboardController : ControllerBase
{
    private readonly AuraCoreDbContext _db;
    public AdminDashboardController(AuraCoreDbContext db) => _db = db;

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats(CancellationToken ct)
    {
        var totalUsers = await _db.Users.CountAsync(ct);
        var proUsers = await _db.Licenses.CountAsync(l => l.Status == "active" && l.Tier == "pro", ct);
        var enterpriseUsers = await _db.Licenses.CountAsync(l => l.Status == "active" && l.Tier == "enterprise", ct);
        var totalRevenue = await _db.Payments.Where(p => p.Status == "completed").SumAsync(p => p.Amount, ct);
        var monthlyRevenue = await _db.Payments
            .Where(p => p.Status == "completed" && p.CompletedAt > DateTimeOffset.UtcNow.AddDays(-30))
            .SumAsync(p => p.Amount, ct);
        var pendingCrypto = await _db.Payments.CountAsync(p => p.Status == "confirming", ct);

        return Ok(new
        {
            totalUsers, proUsers, enterpriseUsers,
            freeUsers = totalUsers - proUsers - enterpriseUsers,
            totalRevenue, monthlyRevenue,
            pendingCryptoPayments = pendingCrypto
        });
    }

    [HttpGet("recent-payments")]
    public async Task<IActionResult> GetRecentPayments([FromQuery] int count = 20, CancellationToken ct = default)
    {
        var payments = await _db.Payments.Include(p => p.User)
            .OrderByDescending(p => p.CreatedAt).Take(count)
            .Select(p => new { p.Id, p.Provider, p.Status, p.Amount, p.Currency, p.Tier, p.Plan, p.CryptoTxHash, p.CreatedAt, userEmail = p.User.Email })
            .ToListAsync(ct);
        return Ok(payments);
    }

    [HttpGet("pending-crypto")]
    public async Task<IActionResult> GetPendingCrypto(CancellationToken ct)
    {
        var pending = await _db.Payments.Include(p => p.User)
            .Where(p => p.Status == "confirming").OrderBy(p => p.CreatedAt)
            .Select(p => new { p.Id, p.Provider, p.Amount, p.Currency, p.Tier, p.CryptoAddress, p.CryptoTxHash, p.CreatedAt, userEmail = p.User.Email })
            .ToListAsync(ct);
        return Ok(pending);
    }
}
