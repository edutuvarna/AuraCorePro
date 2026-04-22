using AuraCore.API.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuraCore.API.Controllers.Admin;

[ApiController]
[Route("api/admin/charts")]
[Authorize(Roles = "admin")]
public sealed class AdminChartController : ControllerBase
{
    private readonly AuraCoreDbContext _db;
    public AdminChartController(AuraCoreDbContext db) => _db = db;

    [HttpGet("revenue")]
    public async Task<IActionResult> Revenue(
        [FromQuery] int days = 30,
        CancellationToken ct = default)
    {
        days = Math.Clamp(days, 1, 365);
        var since = DateTimeOffset.UtcNow.AddDays(-days);

        var rows = await _db.Payments
            .Where(p => p.Status == "completed" && p.CreatedAt >= since)
            .GroupBy(p => p.CreatedAt.Date)
            .Select(g => new { date = g.Key, revenue = g.Sum(p => p.Amount), count = g.Count() })
            .OrderBy(x => x.date)
            .ToListAsync(ct);

        return Ok(new { days, total = rows.Sum(r => r.revenue), items = rows });
    }
}
