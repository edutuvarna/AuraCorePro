using System.Security.Claims;
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
    private readonly AuraCoreDbContext _db;
    public AdminUpdateController(AuraCoreDbContext db) => _db = db;

    /// <summary>Publish a new update</summary>
    [HttpPost("publish")]
    public async Task<IActionResult> Publish([FromBody] PublishUpdateRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Version) || string.IsNullOrWhiteSpace(req.DownloadUrl))
            return BadRequest(new { error = "Version and DownloadUrl are required" });

        // Check if version already exists
        var exists = await _db.AppUpdates.AnyAsync(u => u.Version == req.Version && u.Channel == req.Channel, ct);
        if (exists)
            return Conflict(new { error = $"Version {req.Version} already published for channel '{req.Channel}'" });

        var update = new AppUpdate
        {
            Version = req.Version.Trim(),
            Channel = req.Channel?.Trim() ?? "stable",
            ReleaseNotes = req.ReleaseNotes?.Trim(),
            BinaryUrl = req.DownloadUrl.Trim(),
            SignatureHash = req.SignatureHash?.Trim() ?? "",
            IsMandatory = req.IsMandatory,
            PublishedAt = DateTimeOffset.UtcNow
        };

        _db.AppUpdates.Add(update);
        await _db.SaveChangesAsync(ct);

        // Send Discord webhook notification (fire-and-forget)
        _ = SendDiscordChangelogAsync(update);

        return Ok(new
        {
            message = $"Update v{update.Version} published successfully",
            update = new
            {
                update.Id, update.Version, update.Channel,
                update.ReleaseNotes, update.BinaryUrl,
                update.IsMandatory, update.PublishedAt
            }
        });
    }

    /// <summary>List all updates (admin view)</summary>
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var updates = await _db.AppUpdates
            .OrderByDescending(u => u.PublishedAt)
            .Select(u => new
            {
                u.Id, u.Version, u.Channel, u.ReleaseNotes,
                u.BinaryUrl, u.IsMandatory, u.SignatureHash, u.PublishedAt
            })
            .ToListAsync(ct);
        return Ok(updates);
    }

    /// <summary>Delete an update</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var update = await _db.AppUpdates.FindAsync(new object[] { id }, ct);
        if (update is null) return NotFound(new { error = "Update not found" });

        _db.AppUpdates.Remove(update);
        await _db.SaveChangesAsync(ct);

        return Ok(new { message = $"Update v{update.Version} deleted" });
    }

    /// <summary>Send changelog embed to Discord #changelog via webhook</summary>
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
                        title = $"🚀 AuraCore Pro v{update.Version} Released!",
                        description = notes,
                        color = 54442, // #00D4AA in decimal
                        fields = new[]
                        {
                            new { name = "Channel", value = update.Channel, inline = true },
                            new { name = "Mandatory", value = update.IsMandatory ? "Yes" : "No", inline = true },
                            new { name = "Download", value = $"[Download]({update.BinaryUrl})", inline = true }
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

public sealed record PublishUpdateRequest(
    string Version,
    string DownloadUrl,
    string? ReleaseNotes = null,
    string? Channel = "stable",
    bool IsMandatory = false,
    string? SignatureHash = null
);
