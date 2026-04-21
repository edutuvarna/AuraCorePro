using AuraCore.UI.Avalonia.Services.Update;
using Moq;
using Moq.Protected;
using System.Net;
using System.Net.Http;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.Services;

public class UpdateDownloaderTests
{
    private static HttpMessageHandler BuildHandler(byte[] body)
    {
        var h = new Mock<HttpMessageHandler>();
        h.Protected()
         .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
         .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(body) });
        return h.Object;
    }

    [Fact]
    public async Task DownloadAsync_writes_file_and_returns_path_when_hash_matches()
    {
        var payload = System.Text.Encoding.UTF8.GetBytes("fake-installer-v1.7.0");
        var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(payload)).ToLowerInvariant();

        var http = new HttpClient(BuildHandler(payload));
        var downloader = new UpdateDownloader(http);
        var avail = new AvailableUpdate("1.7.0", "https://x/AuraCorePro-Windows-v1.7.0.exe", hash, false);

        var progress = new Progress<double>(_ => { });
        var path = await downloader.DownloadAsync(avail, progress, CancellationToken.None);

        Assert.True(File.Exists(path));
        Assert.Equal(payload.Length, new FileInfo(path).Length);
        Assert.EndsWith("AuraCorePro-Windows-v1.7.0.exe", path);

        File.Delete(path);
    }

    [Fact]
    public async Task DownloadAsync_throws_and_deletes_file_when_hash_mismatches()
    {
        var payload = System.Text.Encoding.UTF8.GetBytes("corrupted-bytes");
        var wrongHash = new string('0', 64);

        var http = new HttpClient(BuildHandler(payload));
        var downloader = new UpdateDownloader(http);
        var avail = new AvailableUpdate("1.7.0", "https://x/AuraCorePro-Windows-v1.7.0.exe", wrongHash, false);

        var progress = new Progress<double>(_ => { });
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => downloader.DownloadAsync(avail, progress, CancellationToken.None));

        // File should be cleaned up after mismatch
        var expectedPath = Path.Combine(Path.GetTempPath(), "AuraCorePro-Windows-v1.7.0.exe");
        Assert.False(File.Exists(expectedPath));
    }
}
