using AuraCore.API.Domain.Entities;
using AuraCore.API.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuraCore.API.Controllers.Admin;

[ApiController]
[Route("api/admin/ip-whitelist")]
[Route("api/admin/whitelist")]
[Authorize(Roles = "admin")]
public sealed class AdminIpWhitelistController : ControllerBase
{
    private readonly AuraCoreDbContext _db;
    public AdminIpWhitelistController(AuraCoreDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        if (pageSize > 100) pageSize = 100;
        if (pageSize < 1) pageSize = 10;
        if (page < 1) page = 1;

        var total = await _db.IpWhitelists.CountAsync(ct);

        var items = await _db.IpWhitelists
            .OrderByDescending(i => i.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(i => new { i.Id, i.IpAddress, i.Label, i.CreatedAt })
            .ToListAsync(ct);

        return Ok(new { total, page, pageSize, items });
    }

    [HttpPost]
    public async Task<IActionResult> Add([FromBody] AddIpWhitelistRequest req, CancellationToken ct)
    {
        var exists = await _db.IpWhitelists.AnyAsync(i => i.IpAddress == req.IpAddress, ct);
        if (exists)
            return Conflict(new { error = "IP address already whitelisted" });

        var entry = new IpWhitelist
        {
            IpAddress = req.IpAddress,
            Label = req.Label,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _db.IpWhitelists.Add(entry);
        await _db.SaveChangesAsync(ct);

        return Created($"/api/admin/ip-whitelist/{entry.Id}", new
        {
            entry.Id, entry.IpAddress, entry.Label, entry.CreatedAt
        });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var entry = await _db.IpWhitelists.FindAsync(new object[] { id }, ct);
        if (entry is null) return NotFound();

        _db.IpWhitelists.Remove(entry);
        await _db.SaveChangesAsync(ct);
        return Ok(new { message = "IP removed from whitelist" });
    }
}

public sealed class AddIpWhitelistRequest
{
    [System.Text.Json.Serialization.JsonPropertyName("ip")]
    public string IpAddress { get; set; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("label")]
    public string? Label { get; set; }
}
