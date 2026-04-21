using AuraCore.API.Controllers;
using AuraCore.API.Domain.Entities;
using AuraCore.API.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AuraCore.Tests.API.Releases;

public class CheckPlatformFilterTests
{
    private static (UpdateController c, AuraCoreDbContext db) Build()
    {
        var opts = new DbContextOptionsBuilder<AuraCoreDbContext>()
            .UseInMemoryDatabase($"ck-{Guid.NewGuid()}").Options;
        var db = new AuraCoreDbContext(opts);
        return (new UpdateController(db), db);
    }

    [Fact]
    public async Task Check_defaults_to_Windows_when_platform_omitted()
    {
        var (c, db) = Build();
        db.AppUpdates.Add(new AppUpdate { Version = "1.7.0", Channel = "stable",
            Platform = AppUpdatePlatform.Windows, BinaryUrl = "https://r/w", SignatureHash = new string('a', 64), PublishedAt = DateTimeOffset.UtcNow });
        db.AppUpdates.Add(new AppUpdate { Version = "1.7.0", Channel = "stable",
            Platform = AppUpdatePlatform.Linux, BinaryUrl = "https://r/l", SignatureHash = new string('b', 64), PublishedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();

        var result = await c.Check("1.0.0", null, "stable", CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result);
        // Use reflection-light pattern: serialize + read
        var json = System.Text.Json.JsonSerializer.Serialize(ok.Value);
        Assert.Contains("\"https://r/w\"", json);
    }

    [Fact]
    public async Task Check_filters_by_platform_when_specified()
    {
        var (c, db) = Build();
        db.AppUpdates.Add(new AppUpdate { Version = "1.7.0", Channel = "stable",
            Platform = AppUpdatePlatform.Windows, BinaryUrl = "https://r/w", SignatureHash = new string('a', 64), PublishedAt = DateTimeOffset.UtcNow });
        db.AppUpdates.Add(new AppUpdate { Version = "1.7.0", Channel = "stable",
            Platform = AppUpdatePlatform.Linux, BinaryUrl = "https://r/l", SignatureHash = new string('b', 64), PublishedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();

        var result = await c.Check("1.0.0", "linux", "stable", CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result);
        var json = System.Text.Json.JsonSerializer.Serialize(ok.Value);
        Assert.Contains("\"https://r/l\"", json);
    }

    [Fact]
    public async Task Check_returns_no_update_when_platform_has_no_release()
    {
        var (c, db) = Build();
        db.AppUpdates.Add(new AppUpdate { Version = "1.7.0", Channel = "stable",
            Platform = AppUpdatePlatform.Windows, BinaryUrl = "https://r/w", SignatureHash = new string('a', 64), PublishedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();

        var result = await c.Check("1.0.0", "macos", "stable", CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result);
        var json = System.Text.Json.JsonSerializer.Serialize(ok.Value);
        Assert.Contains("\"updateAvailable\":false", json);
    }

    [Theory]
    [InlineData("windows", AppUpdatePlatform.Windows)]
    [InlineData("WINDOWS", AppUpdatePlatform.Windows)]
    [InlineData("linux", AppUpdatePlatform.Linux)]
    [InlineData("macos", AppUpdatePlatform.MacOS)]
    public async Task Check_platform_query_is_case_insensitive(string input, AppUpdatePlatform expected)
    {
        var (c, db) = Build();
        db.AppUpdates.Add(new AppUpdate { Version = "1.7.0", Channel = "stable",
            Platform = expected, BinaryUrl = "https://r/x", SignatureHash = new string('a', 64), PublishedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();

        var result = await c.Check("1.0.0", input, "stable", CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result);
        var json = System.Text.Json.JsonSerializer.Serialize(ok.Value);
        Assert.Contains("\"updateAvailable\":true", json);
    }
}
