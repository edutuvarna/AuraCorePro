using System.Text.RegularExpressions;
using AuraCore.API.Application.Services.Releases;
using AuraCore.API.Domain.Entities;
using AuraCore.API.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuraCore.API.Controllers.Admin;

[ApiController]
[Route("api/admin/updates")]
[Authorize(Roles = "admin")]
public sealed class AdminUpdateController : ControllerBase
{
    private static readonly Regex SemverRegex = new(@"^\d+\.\d+\.\d+(-[a-zA-Z0-9.]+)?$", RegexOptions.Compiled);

    private readonly AuraCoreDbContext _db;
    private readonly IR2Client _r2;
    private readonly IGitHubReleaseMirror _githubMirror;

    public AdminUpdateController(AuraCoreDbContext db, IR2Client r2, IGitHubReleaseMirror githubMirror)
    {
        _db = db;
        _r2 = r2;
        _githubMirror = githubMirror;
    }

    /// <summary>Admin: mint presigned R2 PUT URL for direct browser upload</summary>
    [HttpPost("prepare-upload")]
    public async Task<IActionResult> PrepareUpload([FromBody] PrepareUploadRequest req, CancellationToken ct)
    {
        if (req is null || string.IsNullOrWhiteSpace(req.Version) || !SemverRegex.IsMatch(req.Version))
            return BadRequest(new { error = "Invalid version (semver X.Y.Z required)" });

        if (!Enum.IsDefined(typeof(AppUpdatePlatform), req.Platform))
            return BadRequest(new { error = "Invalid platform" });

        var ext = Path.GetExtension(req.Filename ?? "").ToLowerInvariant();
        var allowed = req.Platform switch
        {
            AppUpdatePlatform.Windows => new[] { ".exe", ".msi" },
            AppUpdatePlatform.Linux   => new[] { ".deb", ".rpm", ".tar.gz", ".appimage" },
            AppUpdatePlatform.MacOS   => new[] { ".dmg", ".pkg" },
            _ => Array.Empty<string>()
        };
        // Special case: ".appimage" extension has inconsistent casing in filenames
        if (!allowed.Contains(ext) && !(req.Platform == AppUpdatePlatform.Linux && (req.Filename ?? "").EndsWith(".AppImage", StringComparison.OrdinalIgnoreCase)))
            return BadRequest(new { error = $"Invalid extension '{ext}' for platform {req.Platform}. Allowed: {string.Join(", ", allowed)}" });

        var channel = (req.Channel ?? "stable").Trim().ToLowerInvariant();
        var duplicate = await _db.AppUpdates.AnyAsync(u =>
            u.Version == req.Version && u.Channel == channel && u.Platform == req.Platform, ct);
        if (duplicate)
            return Conflict(new { error = $"v{req.Version} already exists for {req.Platform} in channel '{channel}'" });

        var safeFilename = Path.GetFileName(req.Filename!).Replace(" ", "-");
        var objectKey = $"pending/{Guid.NewGuid():N}-{safeFilename}";
        var presigned = await _r2.GeneratePresignedPutUrlAsync(
            objectKey, TimeSpan.FromMinutes(10), maxSizeBytes: 500_000_000, ct);

        return Ok(new {
            uploadUrl = presigned.UploadUrl,
            objectKey = presigned.ObjectKey,
            expiresAt = presigned.ExpiresAt,
        });
    }

    /// <summary>Admin: finalize upload — HEAD verify, copy to releases/, compute SHA256, insert row, fire Discord + GitHub mirror.</summary>
    [HttpPost("publish")]
    public async Task<IActionResult> Publish([FromBody] PublishUpdateRequestV2 req, CancellationToken ct)
    {
        if (req is null || string.IsNullOrWhiteSpace(req.Version) || string.IsNullOrWhiteSpace(req.ObjectKey))
            return BadRequest(new { error = "Version and ObjectKey are required" });
        if (!SemverRegex.IsMatch(req.Version))
            return BadRequest(new { error = "Invalid version (semver X.Y.Z required)" });

        var channel = (req.Channel ?? "stable").Trim().ToLowerInvariant();

        // Race-guard: recheck duplicate (prepare-upload may have been minutes ago)
        var duplicate = await _db.AppUpdates.AnyAsync(u =>
            u.Version == req.Version && u.Channel == channel && u.Platform == req.Platform, ct);
        if (duplicate)
            return Conflict(new { error = $"v{req.Version} already exists for {req.Platform} in channel '{channel}'" });

        // Verify upload actually occurred + size sane
        var head = await _r2.HeadObjectAsync(req.ObjectKey, ct);
        if (head is null)
            return BadRequest(new { error = "R2 object not found — PUT to uploadUrl first" });
        if (head.SizeBytes < 10_000 || head.SizeBytes > 500_000_000)
            return BadRequest(new { error = $"Invalid size: {head.SizeBytes} bytes (expected 10KB-500MB)" });

        // Copy pending/* → releases/v{ver}/<canonical-filename>
        var ext = Path.GetExtension(req.ObjectKey);
        var canonical = $"AuraCorePro-{req.Platform}-v{req.Version}{ext}";
        var finalKey = $"releases/v{req.Version}/{canonical}";
        await _r2.CopyObjectAsync(req.ObjectKey, finalKey, ct);
        await _r2.DeleteObjectAsync(req.ObjectKey, ct);  // cleanup pending/

        var sha256 = await _r2.ComputeSha256Async(finalKey, ct);

        var update = new AppUpdate
        {
            Version = req.Version.Trim(),
            Channel = channel,
            Platform = req.Platform,
            ReleaseNotes = req.ReleaseNotes?.Trim(),
            BinaryUrl = $"https://download.auracore.pro/{finalKey}",
            SignatureHash = sha256,
            IsMandatory = req.IsMandatory,
            PublishedAt = DateTimeOffset.UtcNow,
        };
        _db.AppUpdates.Add(update);
        await _db.SaveChangesAsync(ct);

        // Fire-and-forget: Discord + GitHub mirror
        _ = SendDiscordChangelogAsync(update);
        _ = MirrorToGitHubInBackgroundAsync(update, finalKey);

        return Ok(new {
            message = $"v{update.Version} ({update.Platform}) published",
            update = new {
                update.Id, update.Version, update.Channel, update.Platform,
                update.ReleaseNotes, update.BinaryUrl, update.SignatureHash,
                update.IsMandatory, update.PublishedAt
            }
        });
    }

    /// <summary>List all updates (admin view) — includes Platform + GitHubReleaseId</summary>
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var updates = await _db.AppUpdates
            .OrderByDescending(u => u.PublishedAt)
            .Select(u => new
            {
                u.Id, u.Version, u.Channel, u.Platform, u.ReleaseNotes,
                u.BinaryUrl, u.IsMandatory, u.SignatureHash, u.PublishedAt, u.GitHubReleaseId
            })
            .ToListAsync(ct);
        return Ok(updates);
    }

    /// <summary>Delete an update</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var u = await _db.AppUpdates.FindAsync(new object[] { id }, ct);
        if (u is null) return NotFound(new { error = "Update not found" });
        _db.AppUpdates.Remove(u);
        await _db.SaveChangesAsync(ct);
        return Ok(new { message = $"Update v{u.Version} ({u.Platform}) deleted" });
    }

    /// <summary>Admin: retry GitHub mirror for an existing AppUpdate (e.g., after transient failure)</summary>
    [HttpPost("{id:guid}/mirror-to-github")]
    public async Task<IActionResult> RetryGitHubMirror(Guid id, CancellationToken ct)
    {
        var u = await _db.AppUpdates.FindAsync(new object[] { id }, ct);
        if (u is null) return NotFound(new { error = "Update not found" });

        // Reconstruct the R2 object key from BinaryUrl
        var canonical = Path.GetFileName(new Uri(u.BinaryUrl).AbsolutePath);
        var r2Key = $"releases/v{u.Version}/{canonical}";

        try
        {
            var releaseId = await _githubMirror.MirrorAsync(u, r2Key, ct);
            u.GitHubReleaseId = releaseId;
            await _db.SaveChangesAsync(ct);
            return Ok(new { message = "GitHub mirror completed", releaseId });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = $"Mirror failed: {ex.Message}" });
        }
    }

    private async Task MirrorToGitHubInBackgroundAsync(AppUpdate update, string r2ObjectKey)
    {
        try
        {
            var releaseId = await _githubMirror.MirrorAsync(update, r2ObjectKey, CancellationToken.None);
            if (releaseId is not null)
            {
                // Update row with GitHubReleaseId (best-effort; ignore errors)
                using var scope = HttpContext?.RequestServices?.CreateScope();
                var db = scope?.ServiceProvider.GetService<AuraCoreDbContext>() ?? _db;
                var row = await db.AppUpdates.FindAsync(update.Id);
                if (row is not null)
                {
                    row.GitHubReleaseId = releaseId;
                    await db.SaveChangesAsync();
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"GitHub mirror failed for v{update.Version}: {ex.Message}");
        }
    }

    private static async Task SendDiscordChangelogAsync(AppUpdate update)
    {
        try
        {
            var webhookUrl = Environment.GetEnvironmentVariable("DISCORD_WEBHOOK_URL");
            if (string.IsNullOrEmpty(webhookUrl)) return;
            var notes = update.ReleaseNotes ?? "No release notes provided.";
            if (notes.Length > 1800) notes = notes[..1800] + "...";
            var payload = new
            {
                content = string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DISCORD_UPDATES_ROLE_ID"))
                    ? "" : $"<@&{Environment.GetEnvironmentVariable("DISCORD_UPDATES_ROLE_ID")}>",
                embeds = new[]
                {
                    new
                    {
                        title = $"AuraCore Pro v{update.Version} ({update.Platform}) Released!",
                        description = notes,
                        color = 54442,
                        fields = new[]
                        {
                            new { name = "Channel", value = update.Channel, inline = true },
                            new { name = "Platform", value = update.Platform.ToString(), inline = true },
                            new { name = "Mandatory", value = update.IsMandatory ? "Yes" : "No", inline = true },
                            new { name = "Download", value = $"[Download]({update.BinaryUrl})", inline = false }
                        },
                        footer = new { text = "AuraCore Pro • auracore.pro" },
                        timestamp = update.PublishedAt.ToString("o")
                    }
                }
            };
            using var client = new HttpClient();
            var json = System.Text.Json.JsonSerializer.Serialize(payload);
            await client.PostAsync(webhookUrl, new StringContent(json, System.Text.Encoding.UTF8, "application/json"));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Discord webhook error: {ex.Message}");
        }
    }
}

public sealed record PrepareUploadRequest(
    string Version,
    AppUpdatePlatform Platform,
    string Filename,
    string? Channel
);

public sealed record PublishUpdateRequestV2(
    string Version,
    AppUpdatePlatform Platform,
    string ObjectKey,
    string? ReleaseNotes,
    string? Channel,
    bool IsMandatory
);
