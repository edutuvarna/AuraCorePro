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
    public void Composite_unique_index_covers_Version_Channel_Platform()
    {
        // Model-metadata assertion (InMemory DB does NOT enforce unique indexes, so
        // we inspect EF's compiled model directly to verify the composite index
        // has the expected shape).
        var options = new DbContextOptionsBuilder<AuraCoreDbContext>()
            .UseInMemoryDatabase($"meta-{Guid.NewGuid()}")
            .Options;
        using var db = new AuraCoreDbContext(options);

        var entityType = db.Model.FindEntityType(typeof(AppUpdate));
        Assert.NotNull(entityType);

        var uniqueIndex = entityType!.GetIndexes()
            .FirstOrDefault(i => i.IsUnique && i.Properties.Count == 3);
        Assert.NotNull(uniqueIndex);

        var names = uniqueIndex!.Properties.Select(p => p.Name).ToHashSet();
        Assert.Contains("Version", names);
        Assert.Contains("Channel", names);
        Assert.Contains("Platform", names);
    }
}
