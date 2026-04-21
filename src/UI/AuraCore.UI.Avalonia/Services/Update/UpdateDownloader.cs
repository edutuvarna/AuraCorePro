using System.Diagnostics;
using System.Security.Cryptography;

namespace AuraCore.UI.Avalonia.Services.Update;

public sealed class UpdateDownloader : IUpdateDownloader
{
    private readonly HttpClient _http;

    public UpdateDownloader(HttpClient http) => _http = http;

    public async Task<string> DownloadAsync(AvailableUpdate update, IProgress<double> progress, CancellationToken ct)
    {
        var filename = Path.GetFileName(new Uri(update.DownloadUrl).AbsolutePath);
        var path = Path.Combine(Path.GetTempPath(), filename);

        try
        {
            using var resp = await _http.GetAsync(update.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, ct);
            resp.EnsureSuccessStatusCode();
            var total = resp.Content.Headers.ContentLength ?? 0L;

            await using (var fs = File.Create(path))
            await using (var net = await resp.Content.ReadAsStreamAsync(ct))
            {
                var buf = new byte[81920];
                long read = 0;
                int n;
                while ((n = await net.ReadAsync(buf, ct)) > 0)
                {
                    await fs.WriteAsync(buf.AsMemory(0, n), ct);
                    read += n;
                    if (total > 0) progress.Report((double)read / total);
                }
                progress.Report(1.0);
            }

            // Verify SHA256
            await using (var fs = File.OpenRead(path))
            {
                using var sha = SHA256.Create();
                var hash = Convert.ToHexString(await sha.ComputeHashAsync(fs, ct)).ToLowerInvariant();
                if (!string.Equals(hash, update.Sha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException($"SHA256 mismatch: expected {update.Sha256}, got {hash}");
            }

            return path;
        }
        catch
        {
            if (File.Exists(path)) { try { File.Delete(path); } catch { /* ignore */ } }
            throw;
        }
    }

    public void InstallAndExit(string installerPath)
    {
        Process.Start(new ProcessStartInfo { FileName = installerPath, UseShellExecute = true });
        Environment.Exit(0);
    }
}
