// src/Backend/AuraCore.API/Controllers/Superadmin/SecurityPolicyController.cs
using AuraCore.API.Domain.Entities;
using AuraCore.API.Helpers;
using AuraCore.API.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuraCore.API.Controllers.Superadmin;

[ApiController]
[Route("api/superadmin/security-policy")]
[Authorize(Roles = "superadmin")]
public sealed class SecurityPolicyController : ControllerBase
{
    private readonly AuraCoreDbContext _db;
    public SecurityPolicyController(AuraCoreDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var global = await _db.SystemSettings
            .Where(s => s.Key == "require_2fa_for_all_admins")
            .Select(s => s.Value).FirstOrDefaultAsync(ct);
        var overrides = await _db.Users
            .Where(u => u.Role == "admin" || u.Role == "superadmin")
            .Select(u => new { userId = u.Id, email = u.Email, require2fa = u.Require2fa, role = u.Role })
            .ToListAsync(ct);
        return Ok(new {
            require2faForAllAdmins = string.Equals(global, "true", StringComparison.OrdinalIgnoreCase),
            perAccountOverrides = overrides,
        });
    }

    [HttpPut]
    public async Task<IActionResult> Update([FromBody] UpdateDto dto, CancellationToken ct)
    {
        var row = await _db.SystemSettings.FirstOrDefaultAsync(s => s.Key == "require_2fa_for_all_admins", ct);
        if (row is null) { row = new SystemSetting { Key = "require_2fa_for_all_admins" }; _db.SystemSettings.Add(row); }
        row.Value = dto.Require2faForAllAdmins ? "true" : "false";
        row.UpdatedAt = DateTimeOffset.UtcNow;
        row.UpdatedBy = User.GetUserId();
        await _db.SaveChangesAsync(ct);
        return Ok(new { require2faForAllAdmins = dto.Require2faForAllAdmins });
    }

    public sealed record UpdateDto(bool Require2faForAllAdmins);
}
