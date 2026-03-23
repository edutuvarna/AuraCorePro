using AuraCore.API.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuraCore.API.Controllers.Admin;

[ApiController]
[Route("api/admin/users")]
[Authorize(Roles = "admin")]
public sealed class AdminUserController : ControllerBase
{
    private readonly AuraCoreDbContext _db;
    public AdminUserController(AuraCoreDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var query = _db.Users.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(u => u.Email.Contains(search));

        var total = await query.CountAsync(ct);

        var users = await query
            .OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new
            {
                u.Id, u.Email, u.Role, u.CreatedAt,
                license = _db.Licenses
                    .Where(l => l.UserId == u.Id && l.Status == "active")
                    .Select(l => new { l.Tier, l.ExpiresAt })
                    .FirstOrDefault()
            })
            .ToListAsync(ct);

        return Ok(new { total, page, pageSize, users });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var user = await _db.Users.FindAsync(new object[] { id }, ct);
        if (user is null) return NotFound();

        var license = await _db.Licenses
            .FirstOrDefaultAsync(l => l.UserId == id && l.Status == "active", ct);

        return Ok(new
        {
            user.Id, user.Email, user.Role, user.CreatedAt,
            tier = license?.Tier ?? "free",
            expiresAt = license?.ExpiresAt
        });
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest req, CancellationToken ct)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == req.Email, ct);
        if (user is null) return NotFound(new { error = "User not found" });

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.NewPassword);
        user.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        return Ok(new { message = $"Password reset for {req.Email}" });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteUser(Guid id, CancellationToken ct)
    {
        var user = await _db.Users.FindAsync(new object[] { id }, ct);
        if (user is null) return NotFound();

        _db.Users.Remove(user);
        await _db.SaveChangesAsync(ct);
        return Ok(new { message = $"User {user.Email} deleted" });
    }
}

public sealed record ResetPasswordRequest(string Email, string NewPassword);
