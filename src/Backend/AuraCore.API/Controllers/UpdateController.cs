using AuraCore.API.Domain.Entities;
using AuraCore.API.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace AuraCore.API.Controllers;

[ApiController, Route("api/updates")]
public sealed class UpdateController : ControllerBase
{
    private readonly AuraCoreDbContext _db;
    public UpdateController(AuraCoreDbContext db) => _db = db;

    /// <summary>
    /// Client calls this on startup: GET /api/updates/check?currentVersion=1.0.0&amp;platform=windows&amp;channel=stable
    /// </summary>
    [HttpGet("check")]
    public async Task<IActionResult> Check(
        [FromQuery] string currentVersion,
        [FromQuery] string? platform = null,
        [FromQuery] string channel = "stable",
        CancellationToken ct = default)
    {
        // T1.25 AutoUpdateEnabled enforcement
        var cache = HttpContext?.RequestServices?.GetService<IMemoryCache>();
        if (cache is not null
            && cache.TryGetValue<AppConfig>("maintenance-config", out var cachedCfg)
            && cachedCfg is not null
            && cachedCfg.AutoUpdateEnabled == false)
            return StatusCode(503, new { error = "Auto-update is currently disabled" });

        if (string.IsNullOrWhiteSpace(currentVersion))
            return BadRequest(new { error = "currentVersion is required" });

        // Parse platform param (case-insensitive, default Windows)
        AppUpdatePlatform p = AppUpdatePlatform.Windows;
        if (!string.IsNullOrWhiteSpace(platform) &&
            !Enum.TryParse(platform, ignoreCase: true, out p))
        {
            return BadRequest(new { error = $"Invalid platform '{platform}'. Expected: windows|linux|macos" });
        }

        var latest = await _db.AppUpdates
            .Where(u => u.Channel == channel && u.Platform == p)
            .OrderByDescending(u => u.PublishedAt)
            .FirstOrDefaultAsync(ct);

        if (latest is null)
            return Ok(new { updateAvailable = false });

        var isNewer = IsNewerVersion(latest.Version, currentVersion);

        return Ok(new
        {
            updateAvailable = isNewer,
            version = latest.Version,
            channel = latest.Channel,
            platform = latest.Platform.ToString(),
            releaseNotes = latest.ReleaseNotes,
            downloadUrl = latest.BinaryUrl,
            isMandatory = latest.IsMandatory,
            signatureHash = latest.SignatureHash,
            publishedAt = latest.PublishedAt
        });
    }

    /// <summary>Get all published updates (public)</summary>
    [HttpGet("history")]
    public async Task<IActionResult> History(
        [FromQuery] string channel = "stable",
        [FromQuery] int count = 10,
        CancellationToken ct = default)
    {
        var updates = await _db.AppUpdates
            .Where(u => u.Channel == channel)
            .OrderByDescending(u => u.PublishedAt)
            .Take(count)
            .Select(u => new {
                u.Id, u.Version, u.Channel, u.ReleaseNotes,
                u.BinaryUrl, u.IsMandatory, u.PublishedAt
            })
            .ToListAsync(ct);
        return Ok(updates);
    }

    private static bool IsNewerVersion(string latest, string current)
    {
        try
        {
            var l = latest.Split('.').Select(int.Parse).ToArray();
            var c = current.Split('.').Select(int.Parse).ToArray();
            for (int i = 0; i < Math.Min(l.Length, c.Length); i++)
            {
                if (l[i] > c[i]) return true;
                if (l[i] < c[i]) return false;
            }
            return l.Length > c.Length;
        }
        catch { return latest != current; }
    }
}
