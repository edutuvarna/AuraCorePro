using AuraCore.API.Domain.Entities;
using AuraCore.API.Helpers;
using AuraCore.API.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuraCore.API.Controllers;

[ApiController]
[Route("api/admin/me")]
[Authorize]
public sealed class MeController : ControllerBase
{
    private readonly AuraCoreDbContext _db;
    public MeController(AuraCoreDbContext db) => _db = db;

    [HttpPost("fcm-token")]
    public async Task<IActionResult> RegisterFcmToken([FromBody] FcmTokenDto dto, CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (userId is null) return Unauthorized();
        if (string.IsNullOrWhiteSpace(dto.Token)) return BadRequest(new { error = "missing_token" });

        var existing = await _db.FcmDeviceTokens
            .FirstOrDefaultAsync(t => t.UserId == userId.Value && t.Token == dto.Token, ct);
        if (existing != null)
        {
            existing.LastSeenAt = DateTimeOffset.UtcNow;
            existing.Platform = dto.Platform ?? existing.Platform;
            existing.DeviceId = dto.DeviceId ?? existing.DeviceId;
        }
        else
        {
            _db.FcmDeviceTokens.Add(new FcmDeviceToken
            {
                UserId = userId.Value,
                Token = dto.Token,
                Platform = string.IsNullOrEmpty(dto.Platform) ? "android" : dto.Platform,
                DeviceId = dto.DeviceId,
            });
        }
        await _db.SaveChangesAsync(ct);
        return Ok();
    }

    [HttpDelete("fcm-token")]
    public async Task<IActionResult> UnregisterFcmToken([FromQuery] string token, CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (userId is null) return Unauthorized();
        var row = await _db.FcmDeviceTokens
            .FirstOrDefaultAsync(t => t.UserId == userId.Value && t.Token == token, ct);
        if (row != null)
        {
            _db.FcmDeviceTokens.Remove(row);
            await _db.SaveChangesAsync(ct);
        }
        return NoContent();
    }
}

public sealed class FcmTokenDto
{
    public string Token { get; set; } = string.Empty;
    public string? Platform { get; set; }
    public string? DeviceId { get; set; }
}
