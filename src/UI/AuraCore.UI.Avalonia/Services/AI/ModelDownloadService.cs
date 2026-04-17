using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace AuraCore.UI.Avalonia.Services.AI;

public sealed class ModelDownloadService : IModelDownloadService
{
    private const long SIZE_TOLERANCE_BYTES = 1024 * 1024; // ±1 MB per spec §6.2

    private readonly HttpClient _http;
    private readonly ModelDownloadSettings _settings;

    public ModelDownloadService(HttpClient http, ModelDownloadSettings settings)
    {
        _http = http;
        _settings = settings;

        // Ensure User-Agent is set (spec Risk R1 — Bot Fight Mode bypass)
        if (!_http.DefaultRequestHeaders.UserAgent.Any())
        {
            _http.DefaultRequestHeaders.UserAgent.ParseAdd(_settings.UserAgent);
        }
    }

    public async Task<FileInfo> DownloadAsync(
        ModelDescriptor model,
        IProgress<DownloadProgress> progress,
        CancellationToken ct)
    {
        Directory.CreateDirectory(_settings.InstallDirectory);

        var url = $"{_settings.BaseUrl.TrimEnd('/')}/{model.Filename}";
        var finalPath = Path.Combine(_settings.InstallDirectory, model.Filename);
        var tempPath = finalPath + ".download";

        // Clean up any stale temp file from previous failed attempt
        if (File.Exists(tempPath)) File.Delete(tempPath);

        try
        {
            using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct)
                .ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? model.SizeBytes;
            long receivedBytes = 0;
            var stopwatch = Stopwatch.StartNew();

            await using var networkStream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            await using var fileStream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);

            var buffer = new byte[_settings.BufferKb * 1024];
            int bytesRead;
            var lastReport = TimeSpan.Zero;

            while ((bytesRead = await networkStream.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), ct).ConfigureAwait(false);
                receivedBytes += bytesRead;

                // Throttle progress reporting to ~10/sec to avoid UI spam
                var now = stopwatch.Elapsed;
                if ((now - lastReport).TotalMilliseconds >= 100)
                {
                    var bps = receivedBytes / Math.Max(0.001, now.TotalSeconds);
                    TimeSpan? eta = null;
                    if (bps > 0 && totalBytes > receivedBytes)
                        eta = TimeSpan.FromSeconds((totalBytes - receivedBytes) / bps);

                    progress.Report(new DownloadProgress(receivedBytes, totalBytes, bps, eta));
                    lastReport = now;
                }
            }

            await fileStream.FlushAsync(ct).ConfigureAwait(false);
            fileStream.Close();

            // Final progress report
            progress.Report(new DownloadProgress(receivedBytes, totalBytes, 0, TimeSpan.Zero));

            // Size verification per spec §6.2
            var actualSize = new FileInfo(tempPath).Length;
            if (Math.Abs(actualSize - model.SizeBytes) > SIZE_TOLERANCE_BYTES)
            {
                File.Delete(tempPath);
                throw new ModelSizeMismatchException(model.SizeBytes, actualSize);
            }

            // Atomic rename .download → .gguf
            if (File.Exists(finalPath)) File.Delete(finalPath);
            File.Move(tempPath, finalPath);

            return new FileInfo(finalPath);
        }
        catch
        {
            // Any failure (cancellation, http error, size mismatch) → clean up temp file
            try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
            throw;
        }
    }
}
