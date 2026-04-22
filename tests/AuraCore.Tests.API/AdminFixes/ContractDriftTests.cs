using AuraCore.API.Controllers.Admin;
using AuraCore.API.Domain.Entities;
using AuraCore.API.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AuraCore.Tests.API.AdminFixes;

public class ContractDriftTests
{
    private static AuraCoreDbContext BuildDb()
    {
        var options = new DbContextOptionsBuilder<AuraCoreDbContext>()
            .UseInMemoryDatabase($"drift-{Guid.NewGuid()}").Options;
        return new AuraCoreDbContext(options);
    }

    [Fact]
    public async Task AdminUser_GetAll_exposes_top_level_tier_field()
    {
        var db = BuildDb();
        var userId = Guid.NewGuid();
        db.Users.Add(new User { Id = userId, Email = "pro@test.local", PasswordHash = "x" });
        db.Licenses.Add(new License { UserId = userId, Key = "kctp1", Tier = "pro", Status = "active" });
        await db.SaveChangesAsync();

        var controller = new AdminUserController(db);
        var result = await controller.GetAll(search: null, page: 1, pageSize: 50, CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result);
        var json = System.Text.Json.JsonSerializer.Serialize(ok.Value);

        // CTP-1: top-level u.tier exists in the response shape
        Assert.Contains("\"tier\":\"pro\"", json);
        // And the nested license object is retained for back-compat
        Assert.Contains("\"license\":{", json);
    }

    [Fact]
    public async Task AdminUser_GetAll_default_tier_for_users_without_license()
    {
        var db = BuildDb();
        db.Users.Add(new User { Email = "newbie@test.local", PasswordHash = "x" });
        await db.SaveChangesAsync();

        var controller = new AdminUserController(db);
        var result = await controller.GetAll(search: null, page: 1, pageSize: 50, CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result);
        var json = System.Text.Json.JsonSerializer.Serialize(ok.Value);

        Assert.Contains("\"tier\":\"free\"", json);
    }

    [Fact]
    public async Task AdminCrashReport_GetStats_has_semantic_aliases()
    {
        var db = BuildDb();
        var deviceId = Guid.NewGuid();
        db.Devices.Add(new Device { Id = deviceId, HardwareFingerprint = "fp", LicenseId = Guid.NewGuid() });
        db.CrashReports.Add(new CrashReport {
            DeviceId = deviceId, AppVersion = "1.0", ExceptionType = "Null", StackTrace = "st",
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        var controller = new AdminCrashReportController(db);
        var result = await controller.GetStats(CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result);
        var json = System.Text.Json.JsonSerializer.Serialize(ok.Value);

        // CTP-11: both time-window and semantic keys present
        Assert.Contains("\"last24h\":", json);
        Assert.Contains("\"today\":", json);
        Assert.Contains("\"last7d\":", json);
        Assert.Contains("\"thisWeek\":", json);
        Assert.Contains("\"last30d\":", json);
        Assert.Contains("\"thisMonth\":", json);
    }

    [Fact]
    public async Task AdminTelemetry_GetAll_has_pages_field()
    {
        var db = BuildDb();
        var deviceId = Guid.NewGuid();
        db.Devices.Add(new Device { Id = deviceId, HardwareFingerprint = "fp", LicenseId = Guid.NewGuid() });
        for (int i = 0; i < 60; i++)
        {
            db.TelemetryEvents.Add(new TelemetryEvent { DeviceId = deviceId, EventType = "e", EventData = "{}" });
        }
        await db.SaveChangesAsync();

        var controller = new AdminTelemetryController(db);
        var result = await controller.GetAll(eventType: null, page: 1, pageSize: 50, ct: CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result);
        var json = System.Text.Json.JsonSerializer.Serialize(ok.Value);

        // CTP-10: pages field
        Assert.Contains("\"pages\":2", json);
        Assert.Contains("\"total\":60", json);
    }
}
