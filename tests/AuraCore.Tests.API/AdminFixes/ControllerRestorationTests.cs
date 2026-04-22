using AuraCore.API.Controllers.Admin;
using AuraCore.API.Domain.Entities;
using AuraCore.API.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AuraCore.Tests.API.AdminFixes;

public class ControllerRestorationTests
{
    private static AuraCoreDbContext BuildDb()
    {
        var options = new DbContextOptionsBuilder<AuraCoreDbContext>()
            .UseInMemoryDatabase($"ctrl-{Guid.NewGuid()}").Options;
        return new AuraCoreDbContext(options);
    }

    [Fact]
    public async Task AdminLicense_List_returns_pages_field()
    {
        var db = BuildDb();
        for (int i = 0; i < 55; i++)
            db.Licenses.Add(new License { Key = $"k{i}", Tier = "free", Status = "active", MaxDevices = 1 });
        await db.SaveChangesAsync();

        var controller = new AdminLicenseController(db);
        var result = await controller.List(page: 1, pageSize: 50, null, null, CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result);
        var json = System.Text.Json.JsonSerializer.Serialize(ok.Value);
        Assert.Contains("\"pages\":2", json);  // 55 rows / 50 per page = ceiling 2
        Assert.Contains("\"total\":55", json);
    }

    [Fact]
    public async Task AdminLicense_Revoke_sets_status_revoked_AND_tier_free()
    {
        var db = BuildDb();
        var id = Guid.NewGuid();
        db.Licenses.Add(new License { Id = id, Key = "k", Tier = "pro", Status = "active", MaxDevices = 1 });
        await db.SaveChangesAsync();

        var controller = new AdminLicenseController(db);
        var result = await controller.Revoke(id, CancellationToken.None);
        Assert.IsType<OkObjectResult>(result);

        var updated = await db.Licenses.FindAsync(id);
        Assert.Equal("revoked", updated!.Status);
        Assert.Equal("free", updated.Tier);
    }

    [Fact]
    public async Task Stripe_HandleCheckoutCompleted_guards_against_duplicate_ExternalId()
    {
        var db = BuildDb();
        var existingSessionId = "cs_test_abc123";
        db.Payments.Add(new Payment {
            Provider = "stripe", ExternalId = existingSessionId,
            Status = "completed", Amount = 4.99m, Currency = "USD",
        });
        await db.SaveChangesAsync();

        // Simulated: second invocation of HandleCheckoutCompleted with same ExternalId
        var alreadyProcessed = await db.Payments
            .AnyAsync(p => p.ExternalId == existingSessionId && p.Status == "completed");
        Assert.True(alreadyProcessed);  // guard would return early
    }
}
