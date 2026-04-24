using System.Net;
using AuraCore.API.Domain.Entities;
using AuraCore.API.Filters;
using AuraCore.API.Helpers;
using AuraCore.API.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuraCore.API.Controllers.Admin;

[ApiController]
[Route("api/admin/ip-whitelist")]
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
    [RequiresPermission(PermissionKeys.TabIpWhitelist)]
    [AuraCore.API.Filters.AuditAction("AddIpWhitelist", "IpWhitelist")]
    public async Task<IActionResult> Add([FromBody] AddIpWhitelistRequest req, CancellationToken ct)
    {
        // T1.24: validate IP format (IPv4 or IPv6) before hitting DB
        if (string.IsNullOrWhiteSpace(req.IpAddress) || !IsValidIpAddress(req.IpAddress))
            return BadRequest(new { error = "Invalid IP address format (expected IPv4 or IPv6)" });

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
    [RequiresPermission(PermissionKeys.TabIpWhitelist)]
    [AuraCore.API.Filters.AuditAction("RemoveIpWhitelist", "IpWhitelist", TargetIdFromRouteKey = "id")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var entry = await _db.IpWhitelists.FindAsync(new object[] { id }, ct);
        if (entry is null) return NotFound();

        _db.IpWhitelists.Remove(entry);
        await _db.SaveChangesAsync(ct);
        return Ok(new { message = "IP removed from whitelist" });
    }

    [HttpGet("my-ip")]
    public IActionResult GetMyIp()
    {
        // Admin calling from admin.auracore.pro → their IP is in X-Forwarded-For
        // (nginx proxy). HttpContext.Connection.RemoteIpAddress reads the full chain
        // via ForwardedHeaders middleware (configured in Program.cs).
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        // IPv6-mapped-IPv4 normalization (::ffff:192.168.1.1 → 192.168.1.1)
        if (ip.StartsWith("::ffff:")) ip = ip.Substring(7);
        return Ok(new { ip });
    }

    // T1.24: strict IP validation that rejects shortened IPv4 forms (1.2.3 → invalid)
    private static bool IsValidIpAddress(string ipStr)
    {
        if (!IPAddress.TryParse(ipStr, out var addr))
            return false;

        // For IPv4, require all 4 octets in standard dotted-decimal notation
        if (addr.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
            return ipStr.Split('.').Length == 4 && ipStr.Split('.').All(octet =>
                int.TryParse(octet, out var num) && num >= 0 && num <= 255);

        // IPv6 is valid if TryParse succeeded
        return true;
    }
}

public sealed class AddIpWhitelistRequest
{
    [System.Text.Json.Serialization.JsonPropertyName("ip")]
    public string IpAddress { get; set; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("label")]
    public string? Label { get; set; }
}
