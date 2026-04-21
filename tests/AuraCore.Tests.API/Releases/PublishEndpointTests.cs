using AuraCore.API.Application.Services.Releases;
using AuraCore.API.Controllers.Admin;
using AuraCore.API.Domain.Entities;
using AuraCore.API.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AuraCore.Tests.API.Releases;

public class PublishEndpointTests
{
    private static (AdminUpdateController c, AuraCoreDbContext db, Mock<IR2Client> r2, Mock<IGitHubReleaseMirror> gh) Build()
    {
        var opts = new DbContextOptionsBuilder<AuraCoreDbContext>()
            .UseInMemoryDatabase($"p-{Guid.NewGuid()}").Options;
        var db = new AuraCoreDbContext(opts);
        var r2 = new Mock<IR2Client>();
        var gh = new Mock<IGitHubReleaseMirror>();
        return (new AdminUpdateController(
            db, r2.Object, gh.Object,
            Mock.Of<IServiceScopeFactory>(),
            Mock.Of<ILogger<AdminUpdateController>>()), db, r2, gh);
    }

    [Fact]
    public async Task Publish_400_when_r2_object_missing()
    {
        var (c, _, r2, _) = Build();
        r2.Setup(x => x.HeadObjectAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
          .ReturnsAsync((R2ObjectHead?)null);

        var result = await c.Publish(new PublishUpdateRequestV2(
            "1.7.0", AppUpdatePlatform.Windows, "pending/abc-setup.exe", "notes", "stable", false),
            CancellationToken.None);
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Publish_400_when_object_too_small()
    {
        var (c, _, r2, _) = Build();
        r2.Setup(x => x.HeadObjectAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
          .ReturnsAsync(new R2ObjectHead(500, DateTimeOffset.UtcNow, "application/octet-stream"));

        var result = await c.Publish(new PublishUpdateRequestV2(
            "1.7.0", AppUpdatePlatform.Windows, "pending/abc-setup.exe", null, null, false),
            CancellationToken.None);
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Publish_409_on_duplicate_version_platform_channel()
    {
        var (c, db, r2, _) = Build();
        db.AppUpdates.Add(new AppUpdate { Version = "1.7.0", Channel = "stable",
            Platform = AppUpdatePlatform.Windows, BinaryUrl = "x", SignatureHash = "", PublishedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();
        r2.Setup(x => x.HeadObjectAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
          .ReturnsAsync(new R2ObjectHead(50_000_000, DateTimeOffset.UtcNow, "application/octet-stream"));

        var result = await c.Publish(new PublishUpdateRequestV2(
            "1.7.0", AppUpdatePlatform.Windows, "pending/abc-setup.exe", null, "stable", false),
            CancellationToken.None);
        Assert.IsType<ConflictObjectResult>(result);
    }

    [Fact]
    public async Task Publish_happy_path_copies_computes_hash_inserts_row()
    {
        var (c, db, r2, gh) = Build();
        r2.Setup(x => x.HeadObjectAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
          .ReturnsAsync(new R2ObjectHead(50_000_000, DateTimeOffset.UtcNow, "application/octet-stream"));
        r2.Setup(x => x.CopyObjectAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
          .Returns(Task.CompletedTask);
        r2.Setup(x => x.DeleteObjectAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
          .Returns(Task.CompletedTask);
        r2.Setup(x => x.ComputeSha256Async(It.IsAny<string>(), It.IsAny<CancellationToken>()))
          .ReturnsAsync("abc123def456abc123def456abc123def456abc123def456abc123def456abcd");

        var result = await c.Publish(new PublishUpdateRequestV2(
            "1.7.0", AppUpdatePlatform.Windows, "pending/abc-setup.exe",
            "Release notes.", "stable", false), CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        var row = await db.AppUpdates.SingleAsync(u => u.Version == "1.7.0" && u.Platform == AppUpdatePlatform.Windows);
        Assert.Equal("abc123def456abc123def456abc123def456abc123def456abc123def456abcd", row.SignatureHash);
        Assert.StartsWith("https://download.auracore.pro/releases/v1.7.0/", row.BinaryUrl);
        Assert.EndsWith(".exe", row.BinaryUrl);

        r2.Verify(x => x.CopyObjectAsync("pending/abc-setup.exe", It.Is<string>(s => s.StartsWith("releases/v1.7.0/")), It.IsAny<CancellationToken>()), Times.Once);
        r2.Verify(x => x.DeleteObjectAsync("pending/abc-setup.exe", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Publish_canonical_filename_includes_platform_and_version()
    {
        var (c, db, r2, _) = Build();
        r2.Setup(x => x.HeadObjectAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(new R2ObjectHead(50_000_000, DateTimeOffset.UtcNow, null));
        r2.Setup(x => x.CopyObjectAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        r2.Setup(x => x.DeleteObjectAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        r2.Setup(x => x.ComputeSha256Async(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(new string('a', 64));

        await c.Publish(new PublishUpdateRequestV2(
            "1.7.0", AppUpdatePlatform.Linux, "pending/xyz-aura.deb", null, "stable", false), CancellationToken.None);

        var row = await db.AppUpdates.SingleAsync(u => u.Platform == AppUpdatePlatform.Linux);
        Assert.Contains("AuraCorePro-Linux-v1.7.0.deb", row.BinaryUrl);
    }
}
