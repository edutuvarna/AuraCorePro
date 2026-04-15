using AuraCore.UI.Avalonia.Services.AI;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.Services.AI;

public class ModelDownloadServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly ModelDescriptor _testModel;

    public ModelDownloadServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "auracore-dl-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        // SizeBytes = 2 MB so that a 5-byte "short" content triggers mismatch (diff ~2MB > 1MB tolerance)
        _testModel = new ModelDescriptor(
            Id: "tinyllama",
            DisplayName: "TinyLlama",
            Filename: "auracore-tinyllama.gguf",
            SizeBytes: 2 * 1024 * 1024, // 2 MB — ensures size-mismatch test fires (5 bytes diff >> 1 MB)
            EstimatedRamBytes: 2L * 1024 * 1024 * 1024,
            Tier: ModelTier.Lite,
            Speed: SpeedClass.Fast,
            IsRecommended: false,
            DescriptionKey: "modelManager.model.tinyllama.description");
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    private HttpClient StubHttp(byte[] content, HttpStatusCode status = HttpStatusCode.OK)
    {
        return new HttpClient(new StubHandler(content, status));
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly byte[] _content;
        private readonly HttpStatusCode _status;
        public StubHandler(byte[] content, HttpStatusCode status) { _content = content; _status = status; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var response = new HttpResponseMessage(_status);
            if (_status == HttpStatusCode.OK)
            {
                var stream = new MemoryStream(_content);
                response.Content = new StreamContent(stream);
                response.Content.Headers.ContentLength = _content.Length;
            }
            return Task.FromResult(response);
        }
    }

    [Fact]
    public async Task DownloadAsync_Success_CreatesGgufFile()
    {
        // Content is exactly 2 MB, matching SizeBytes — within tolerance (diff = 0)
        var content = new byte[2 * 1024 * 1024];
        var http = StubHttp(content);
        var settings = new ModelDownloadSettings("https://example.com", _tempDir, 30, 4, "Test/1.0");
        var svc = new ModelDownloadService(http, settings);

        var file = await svc.DownloadAsync(_testModel, new Progress<DownloadProgress>(), CancellationToken.None);

        Assert.True(File.Exists(file.FullName));
        Assert.EndsWith(".gguf", file.FullName);
        Assert.Equal(2 * 1024 * 1024, new FileInfo(file.FullName).Length);
    }

    [Fact]
    public async Task DownloadAsync_Success_DeletesPartialTempFile()
    {
        var content = new byte[2 * 1024 * 1024];
        var http = StubHttp(content);
        var settings = new ModelDownloadSettings("https://example.com", _tempDir, 30, 4, "Test/1.0");
        var svc = new ModelDownloadService(http, settings);

        await svc.DownloadAsync(_testModel, new Progress<DownloadProgress>(), CancellationToken.None);

        var tempPath = Path.Combine(_tempDir, _testModel.Filename + ".download");
        Assert.False(File.Exists(tempPath));
    }

    [Fact]
    public async Task DownloadAsync_SizeMismatch_ThrowsAndDeletesFile()
    {
        // 5 bytes received vs 2 MB expected: diff ~2 MB > 1 MB tolerance → ModelSizeMismatchException
        var content = Encoding.ASCII.GetBytes("short");
        var http = StubHttp(content);
        var settings = new ModelDownloadSettings("https://example.com", _tempDir, 30, 4, "Test/1.0");
        var svc = new ModelDownloadService(http, settings);

        await Assert.ThrowsAsync<ModelSizeMismatchException>(() =>
            svc.DownloadAsync(_testModel, new Progress<DownloadProgress>(), CancellationToken.None));

        var path = Path.Combine(_tempDir, _testModel.Filename);
        Assert.False(File.Exists(path));
    }

    [Fact]
    public async Task DownloadAsync_Cancelled_DeletesPartialFile()
    {
        var content = new byte[1024 * 1024]; // 1 MB stream
        var http = StubHttp(content);
        var settings = new ModelDownloadSettings("https://example.com", _tempDir, 30, 4, "Test/1.0");
        var svc = new ModelDownloadService(http, settings);

        using var cts = new CancellationTokenSource();
        cts.Cancel(); // pre-cancelled

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            svc.DownloadAsync(_testModel, new Progress<DownloadProgress>(), cts.Token));

        var tempPath = Path.Combine(_tempDir, _testModel.Filename + ".download");
        Assert.False(File.Exists(tempPath));
    }

    [Fact]
    public async Task DownloadAsync_HttpError_Throws()
    {
        var http = StubHttp(Array.Empty<byte>(), HttpStatusCode.Forbidden);
        var settings = new ModelDownloadSettings("https://example.com", _tempDir, 30, 4, "Test/1.0");
        var svc = new ModelDownloadService(http, settings);

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            svc.DownloadAsync(_testModel, new Progress<DownloadProgress>(), CancellationToken.None));
    }

    [Fact]
    public async Task DownloadAsync_ReportsProgress()
    {
        var content = new byte[2 * 1024 * 1024]; // 2 MB, matches SizeBytes
        var http = StubHttp(content);
        var settings = new ModelDownloadSettings("https://example.com", _tempDir, 30, 4, "Test/1.0");
        var svc = new ModelDownloadService(http, settings);

        var progressReports = new List<DownloadProgress>();
        var progress = new Progress<DownloadProgress>(p => progressReports.Add(p));

        await svc.DownloadAsync(_testModel, progress, CancellationToken.None);

        await Task.Delay(100); // allow progress callbacks to flush
        Assert.NotEmpty(progressReports);
    }
}
