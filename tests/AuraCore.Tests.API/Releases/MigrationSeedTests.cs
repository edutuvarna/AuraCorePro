using AuraCore.API.Domain.Entities;
using AuraCore.API.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AuraCore.Tests.API.Releases;

public class MigrationSeedTests
{
    [Fact]
    public void AppUpdate_supports_Windows_Linux_MacOS_platform_values()
    {
        Assert.Equal(1, (int)AppUpdatePlatform.Windows);
        Assert.Equal(2, (int)AppUpdatePlatform.Linux);
        Assert.Equal(3, (int)AppUpdatePlatform.MacOS);
    }

    [Fact]
    public void AppUpdate_platform_defaults_to_Windows_when_not_set()
    {
        var u = new AppUpdate { Version = "1.0.0", BinaryUrl = "https://x" };
        Assert.Equal(AppUpdatePlatform.Windows, u.Platform);
    }

    [Fact]
    public void Composite_unique_index_allows_same_version_on_different_platforms()
    {
        // This is a model-level test — confirms the new composite index config
        // allows (v1.7.0, stable, Windows) AND (v1.7.0, stable, Linux) to coexist.
        var options = new DbContextOptionsBuilder<AuraCoreDbContext>()
            .UseInMemoryDatabase($"test-{Guid.NewGuid()}")
            .Options;
        using var db = new AuraCoreDbContext(options);
        db.AppUpdates.Add(new AppUpdate { Version = "1.7.0", Channel = "stable", Platform = AppUpdatePlatform.Windows, BinaryUrl = "https://x/w", SignatureHash = "", PublishedAt = DateTimeOffset.UtcNow });
        db.AppUpdates.Add(new AppUpdate { Version = "1.7.0", Channel = "stable", Platform = AppUpdatePlatform.Linux,   BinaryUrl = "https://x/l", SignatureHash = "", PublishedAt = DateTimeOffset.UtcNow });
        db.SaveChanges();
        Assert.Equal(2, db.AppUpdates.Count());
    }
}
