using AuraCore.API.Application.Services.Releases;
using AuraCore.API.Controllers.Admin;
using AuraCore.API.Domain.Entities;
using AuraCore.API.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace AuraCore.Tests.API.Releases;

public class PrepareUploadEndpointTests
{
    private static (AdminUpdateController controller, AuraCoreDbContext db, Mock<IR2Client> r2) Build()
    {
        var options = new DbContextOptionsBuilder<AuraCoreDbContext>()
            .UseInMemoryDatabase($"t-{Guid.NewGuid()}").Options;
        var db = new AuraCoreDbContext(options);
        var r2 = new Mock<IR2Client>();
        var c = new AdminUpdateController(db, r2.Object, Mock.Of<IGitHubReleaseMirror>());
        return (c, db, r2);
    }

    [Fact]
    public async Task PrepareUpload_rejects_invalid_semver()
    {
        var (c, _, _) = Build();
        var result = await c.PrepareUpload(
            new PrepareUploadRequest("notversion", AppUpdatePlatform.Windows, "setup.exe", null),
            CancellationToken.None);
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task PrepareUpload_rejects_wrong_extension_for_platform()
    {
        var (c, _, _) = Build();
        var result = await c.PrepareUpload(
            new PrepareUploadRequest("1.7.0", AppUpdatePlatform.Windows, "setup.dmg", null),
            CancellationToken.None);
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task PrepareUpload_rejects_duplicate_version_on_same_platform()
    {
        var (c, db, _) = Build();
        db.AppUpdates.Add(new AppUpdate { Version = "1.7.0", Channel = "stable",
            Platform = AppUpdatePlatform.Windows, BinaryUrl = "x", SignatureHash = "", PublishedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();

        var result = await c.PrepareUpload(
            new PrepareUploadRequest("1.7.0", AppUpdatePlatform.Windows, "setup.exe", null),
            CancellationToken.None);
        Assert.IsType<ConflictObjectResult>(result);
    }

    [Fact]
    public async Task PrepareUpload_mints_presigned_url_on_valid_request()
    {
        var (c, _, r2) = Build();
        r2.Setup(x => x.GeneratePresignedPutUrlAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
          .ReturnsAsync((string key, TimeSpan _, long _, CancellationToken _) =>
              new PresignedPutResult($"https://r2/{key}?sig=x", key, DateTimeOffset.UtcNow.AddMinutes(10)));

        var result = await c.PrepareUpload(
            new PrepareUploadRequest("1.7.0", AppUpdatePlatform.Windows, "setup.exe", null),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(ok.Value);
    }

    [Theory]
    [InlineData(AppUpdatePlatform.Linux, "auracore.deb")]
    [InlineData(AppUpdatePlatform.Linux, "auracore.AppImage")]
    [InlineData(AppUpdatePlatform.MacOS, "auracore.dmg")]
    public async Task PrepareUpload_accepts_platform_specific_extensions(AppUpdatePlatform platform, string filename)
    {
        var (c, _, r2) = Build();
        r2.Setup(x => x.GeneratePresignedPutUrlAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
          .ReturnsAsync(new PresignedPutResult("https://r2/x", "x", DateTimeOffset.UtcNow.AddMinutes(10)));

        var result = await c.PrepareUpload(
            new PrepareUploadRequest("1.7.0", platform, filename, null),
            CancellationToken.None);
        Assert.IsType<OkObjectResult>(result);
    }
}
