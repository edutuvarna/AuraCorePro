using AuraCore.API.Application.Services.Releases;
using AuraCore.API.Domain.Entities;
using AuraCore.API.Infrastructure.Services.Releases;
using Moq;
using Octokit;
using Xunit;

namespace AuraCore.Tests.API.Releases;

public class GitHubReleaseMirrorTests : IDisposable
{
    private readonly string? _originalToken;

    public GitHubReleaseMirrorTests()
    {
        _originalToken = Environment.GetEnvironmentVariable("ASPNETCORE_GITHUB_TOKEN");
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_GITHUB_TOKEN", _originalToken);
    }

    [Fact]
    public async Task MirrorAsync_returns_null_when_no_github_token_configured()
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_GITHUB_TOKEN", "");
        var r2 = new Mock<IR2Client>();
        var mirror = new OctokitReleaseMirror(r2.Object, githubClientFactory: token => null!);

        var result = await mirror.MirrorAsync(
            new AppUpdate { Version = "1.7.0", Platform = AppUpdatePlatform.Windows, BinaryUrl = "x", SignatureHash = new string('a', 64) },
            "releases/v1.7.0/x.exe", CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task MirrorAsync_creates_release_and_uploads_asset_when_token_set()
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_GITHUB_TOKEN", "fake-pat");

        var r2 = new Mock<IR2Client>();
        r2.Setup(x => x.DownloadToStreamAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
          .Callback<string, Stream, CancellationToken>((_, stream, _) =>
              stream.Write(System.Text.Encoding.UTF8.GetBytes("fake-installer-bytes")))
          .Returns(Task.CompletedTask);

        var releases = new Mock<IReleasesClient>();
        // Simulate tag not found → triggers Create path
        releases.Setup(x => x.Get(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ThrowsAsync(new NotFoundException("not found", System.Net.HttpStatusCode.NotFound));
        releases.Setup(x => x.Create(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<NewRelease>()))
                .ReturnsAsync(new Release(
                    url: "", htmlUrl: "", assetsUrl: "", uploadUrl: "", id: 999L,
                    nodeId: "", tagName: "v1.7.0", targetCommitish: "main",
                    name: "v1.7.0", body: "notes", draft: false, prerelease: false,
                    createdAt: DateTimeOffset.UtcNow, publishedAt: DateTimeOffset.UtcNow,
                    author: null!, tarballUrl: "", zipballUrl: "", assets: Array.Empty<ReleaseAsset>()));
        releases.Setup(x => x.UploadAsset(It.IsAny<Release>(), It.IsAny<ReleaseAssetUpload>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ReleaseAsset(
                    url: "", id: 0, nodeId: "", name: "x", label: "", state: "",
                    contentType: "", size: 0, downloadCount: 0, createdAt: DateTimeOffset.UtcNow,
                    updatedAt: DateTimeOffset.UtcNow, browserDownloadUrl: "", uploader: null!));

        var gh = new Mock<IGitHubClient>();
        var repo = new Mock<IRepositoriesClient>();
        repo.Setup(x => x.Release).Returns(releases.Object);
        gh.Setup(x => x.Repository).Returns(repo.Object);

        var mirror = new OctokitReleaseMirror(r2.Object, _ => gh.Object);
        var releaseId = await mirror.MirrorAsync(
            new AppUpdate { Version = "1.7.0", Platform = AppUpdatePlatform.Windows, Channel = "stable",
                ReleaseNotes = "notes", BinaryUrl = "x", SignatureHash = new string('a', 64) },
            "releases/v1.7.0/AuraCorePro-Windows-v1.7.0.exe", CancellationToken.None);

        Assert.Equal("999", releaseId);
        releases.Verify(x => x.Create(It.IsAny<string>(), It.IsAny<string>(), It.Is<NewRelease>(r =>
            r.TagName == "v1.7.0" && r.Name != null && r.Name.IndexOf("1.7.0", StringComparison.Ordinal) >= 0)), Times.Once);
        // Assert 2 assets uploaded: binary + sha256sums.txt
        releases.Verify(x => x.UploadAsset(It.IsAny<Release>(), It.IsAny<ReleaseAssetUpload>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task MirrorAsync_uploads_to_existing_release_when_tag_already_exists()
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_GITHUB_TOKEN", "fake-pat");

        var r2 = new Mock<IR2Client>();
        r2.Setup(x => x.DownloadToStreamAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
          .Callback<string, Stream, CancellationToken>((_, stream, _) =>
              stream.Write(System.Text.Encoding.UTF8.GetBytes("fake-installer-bytes")))
          .Returns(Task.CompletedTask);

        var existing = new Release(
            url: "", htmlUrl: "", assetsUrl: "", uploadUrl: "", id: 555L,
            nodeId: "", tagName: "v1.7.0", targetCommitish: "main",
            name: "v1.7.0", body: "notes", draft: false, prerelease: false,
            createdAt: DateTimeOffset.UtcNow, publishedAt: DateTimeOffset.UtcNow,
            author: null, tarballUrl: "", zipballUrl: "", assets: Array.Empty<ReleaseAsset>());

        var releases = new Mock<IReleasesClient>();
        releases.Setup(x => x.Get(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(existing);
        releases.Setup(x => x.UploadAsset(It.IsAny<Release>(), It.IsAny<ReleaseAssetUpload>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ReleaseAsset(
                    url: "", id: 0, nodeId: "", name: "x", label: "", state: "",
                    contentType: "", size: 0, downloadCount: 0, createdAt: DateTimeOffset.UtcNow,
                    updatedAt: DateTimeOffset.UtcNow, browserDownloadUrl: "", uploader: null));

        var gh = new Mock<IGitHubClient>();
        var repo = new Mock<IRepositoriesClient>();
        repo.Setup(x => x.Release).Returns(releases.Object);
        gh.Setup(x => x.Repository).Returns(repo.Object);

        var mirror = new OctokitReleaseMirror(r2.Object, _ => gh.Object);
        var releaseId = await mirror.MirrorAsync(
            new AppUpdate { Version = "1.7.0", Platform = AppUpdatePlatform.Linux, Channel = "stable",
                ReleaseNotes = "notes", BinaryUrl = "x", SignatureHash = new string('a', 64) },
            "releases/v1.7.0/AuraCorePro-Linux-v1.7.0.deb", CancellationToken.None);

        Assert.Equal("555", releaseId);
        // Create was NOT called (release already existed)
        releases.Verify(x => x.Create(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<NewRelease>()), Times.Never);
        // But both assets STILL uploaded
        releases.Verify(x => x.UploadAsset(It.IsAny<Release>(), It.IsAny<ReleaseAssetUpload>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }
}
