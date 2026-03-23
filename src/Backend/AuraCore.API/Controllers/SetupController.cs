using AuraCore.API.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuraCore.API.Controllers;

/// <summary>
/// First-time setup endpoint. Promotes a user to admin.
/// Only works if NO admin users exist yet (security).
/// After the first admin is created, this endpoint is disabled.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public sealed class SetupController : ControllerBase
{
    private readonly AuraCoreDbContext _db;
    public SetupController(AuraCoreDbContext db) => _db = db;

    /// <summary>
    /// POST /api/setup/promote-admin
    /// Body: { "email": "admin@auracore.pro" }
    /// Only works when there are zero admin users in the database.
    /// </summary>
    [HttpPost("promote-admin")]
    public async Task<IActionResult> PromoteAdmin([FromBody] PromoteRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
            return BadRequest(new { error = "Email is required" });

        // Security: only allow if no admin exists yet
        var adminExists = await _db.Users.AnyAsync(u => u.Role == "admin", ct);
        if (adminExists)
            return Forbid();

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == request.Email, ct);
        if (user is null)
            return NotFound(new { error = "User not found. Register first." });

        user.Role = "admin";
        user.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        return Ok(new
        {
            message = $"{request.Email} is now admin",
            userId = user.Id,
            role = "admin"
        });
    }
}

public sealed record PromoteRequest(string Email);
