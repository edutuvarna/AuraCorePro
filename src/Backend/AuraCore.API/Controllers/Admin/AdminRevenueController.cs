using AuraCore.API.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuraCore.API.Controllers.Admin;

[ApiController]
[Route("api/admin/revenue")]
[Authorize(Roles = "admin")]
public sealed class AdminRevenueController : ControllerBase
{
    private readonly AuraCoreDbContext _db;
    public AdminRevenueController(AuraCoreDbContext db) => _db = db;

    [HttpGet("chart-data")]
    public async Task<IActionResult> GetChartData(
        [FromQuery] int months = 12,
        CancellationToken ct = default)
    {
        if (months < 1) months = 1;
        if (months > 24) months = 24;

        var since = DateTimeOffset.UtcNow.AddMonths(-months);

        var monthlyData = await _db.Payments
            .Where(p => p.Status == "completed" && p.CompletedAt != null && p.CompletedAt > since)
            .GroupBy(p => new { p.CompletedAt!.Value.Year, p.CompletedAt!.Value.Month })
            .Select(g => new
            {
                year = g.Key.Year,
                month = g.Key.Month,
                revenue = g.Sum(p => p.Amount),
                count = g.Count()
            })
            .OrderBy(x => x.year).ThenBy(x => x.month)
            .ToListAsync(ct);

        var totalRevenue = await _db.Payments
            .Where(p => p.Status == "completed")
            .SumAsync(p => p.Amount, ct);

        var periodRevenue = monthlyData.Sum(m => m.revenue);

        return Ok(new { totalRevenue, periodRevenue, months, data = monthlyData });
    }
}
