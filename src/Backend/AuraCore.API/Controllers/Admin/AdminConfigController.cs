using AuraCore.API.Domain.Entities;
using AuraCore.API.Filters;
using AuraCore.API.Helpers;
using AuraCore.API.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuraCore.API.Controllers.Admin;

[ApiController]
[Route("api/admin/config")]
[Authorize(Roles = "admin")]
public sealed class AdminConfigController : ControllerBase
{
    private readonly AuraCoreDbContext _db;
    private readonly Microsoft.Extensions.Caching.Memory.IMemoryCache _cache;
    public AdminConfigController(AuraCoreDbContext db, Microsoft.Extensions.Caching.Memory.IMemoryCache cache)
    {
        _db = db;
        _cache = cache;
    }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var config = await _db.AppConfigs.FirstOrDefaultAsync(c => c.Id == 1, ct);
        if (config is null)
        {
            config = new AppConfig { Id = 1 };
            _db.AppConfigs.Add(config);
            await _db.SaveChangesAsync(ct);
        }

        return Ok(new
        {
            config.IsMaintenanceMode,
            config.MaintenanceMessage,
            config.NewRegistrations,
            config.TelemetryEnabled,
            config.CrashReportsEnabled,
            config.AutoUpdateEnabled,
            config.LastUpdated
        });
    }

    [HttpPut]
    [RequiresPermission(PermissionKeys.TabConfiguration)]
    [AuraCore.API.Filters.AuditAction("UpdateAppConfig", "AppConfig")]
    public async Task<IActionResult> Update([FromBody] UpdateConfigRequest req, CancellationToken ct)
    {
        var config = await _db.AppConfigs.FirstOrDefaultAsync(c => c.Id == 1, ct);
        if (config is null)
        {
            config = new AppConfig { Id = 1 };
            _db.AppConfigs.Add(config);
        }

        if (req.IsMaintenanceMode.HasValue) config.IsMaintenanceMode = req.IsMaintenanceMode.Value;
        if (req.MaintenanceMessage is not null) config.MaintenanceMessage = req.MaintenanceMessage;
        if (req.NewRegistrations.HasValue) config.NewRegistrations = req.NewRegistrations.Value;
        if (req.TelemetryEnabled.HasValue) config.TelemetryEnabled = req.TelemetryEnabled.Value;
        if (req.CrashReportsEnabled.HasValue) config.CrashReportsEnabled = req.CrashReportsEnabled.Value;
        if (req.AutoUpdateEnabled.HasValue) config.AutoUpdateEnabled = req.AutoUpdateEnabled.Value;

        config.LastUpdated = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        _cache.Remove("maintenance-config");

        return Ok(new
        {
            config.IsMaintenanceMode,
            config.MaintenanceMessage,
            config.NewRegistrations,
            config.TelemetryEnabled,
            config.CrashReportsEnabled,
            config.AutoUpdateEnabled,
            config.LastUpdated
        });
    }
}

public sealed record UpdateConfigRequest(
    bool? IsMaintenanceMode = null,
    [System.ComponentModel.DataAnnotations.MaxLength(1000)] string? MaintenanceMessage = null,
    bool? NewRegistrations = null,
    bool? TelemetryEnabled = null,
    bool? CrashReportsEnabled = null,
    bool? AutoUpdateEnabled = null
);
