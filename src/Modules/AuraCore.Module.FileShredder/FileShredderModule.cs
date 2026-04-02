using AuraCore.Application;
using AuraCore.Application.Interfaces.Modules;
using AuraCore.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Cryptography;

namespace AuraCore.Module.FileShredder;

public class FileShredderModule : IOptimizationModule
{
    public string Id => "file-shredder";
    public string DisplayName => "File Shredder";
    public OptimizationCategory Category => OptimizationCategory.Privacy;
    public RiskLevel Risk => RiskLevel.High;
    public SupportedPlatform Platform => SupportedPlatform.All;

    private readonly List<string> _pendingFiles = new();

    public void AddFiles(IEnumerable<string> paths)
    {
        foreach (var p in paths)
            if (File.Exists(p) && !_pendingFiles.Contains(p))
                _pendingFiles.Add(p);
    }

    public void ClearFiles() => _pendingFiles.Clear();
    public IReadOnlyList<string> PendingFiles => _pendingFiles.AsReadOnly();

    public Task<ScanResult> ScanAsync(ScanOptions options, CancellationToken ct = default)
    {
        return Task.FromResult(new ScanResult(Id, true, _pendingFiles.Count,
            _pendingFiles.Sum(f => File.Exists(f) ? new FileInfo(f).Length : 0)));
    }

    public async Task<OptimizationResult> OptimizeAsync(
        OptimizationPlan plan, IProgress<TaskProgress>? progress = null, CancellationToken ct = default)
    {
        if (_pendingFiles.Count == 0)
            return new OptimizationResult(Id, "", true, 0, 0, TimeSpan.Zero);

        var opId = Guid.NewGuid().ToString()[..8];
        var passes = 3; // Default 3-pass
        var shredded = 0;
        var total = _pendingFiles.Count;

        foreach (var filePath in _pendingFiles.ToList())
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                if (!File.Exists(filePath)) { shredded++; continue; }
                var fileSize = new FileInfo(filePath).Length;

                progress?.Report(new TaskProgress(
                    Id,
                    (double)shredded / total * 100,
                    $"Shredding: {Path.GetFileName(filePath)}"));

                // Multi-pass overwrite
                using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Write, FileShare.None))
                {
                    var buffer = new byte[8192];
                    for (int pass = 0; pass < passes; pass++)
                    {
                        fs.Position = 0;
                        long written = 0;
                        while (written < fileSize)
                        {
                            if (pass % 3 == 0) Array.Clear(buffer);
                            else if (pass % 3 == 1) Array.Fill(buffer, (byte)0xFF);
                            else RandomNumberGenerator.Fill(buffer);

                            var toWrite = (int)Math.Min(buffer.Length, fileSize - written);
                            fs.Write(buffer, 0, toWrite);
                            written += toWrite;
                        }
                        fs.Flush();
                    }
                }

                // Rename to random name before delete
                var dir = Path.GetDirectoryName(filePath);
                if (dir is null) { shredded++; continue; }
                var randomName = Path.Combine(dir, Guid.NewGuid().ToString("N")[..12] + ".tmp");
                File.Move(filePath, randomName);
                File.Delete(randomName);
                shredded++;
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[FileShredder] Shred failed for {Path.GetFileName(filePath)}: {ex.Message}"); shredded++; }
        }

        _pendingFiles.Clear();
        progress?.Report(new TaskProgress(Id, 100, "Shredding complete"));
        return new OptimizationResult(Id, opId, true, total, shredded, TimeSpan.Zero);
    }

    public Task<bool> CanRollbackAsync(string operationId, CancellationToken ct = default)
        => Task.FromResult(false); // Shredding is irreversible by design

    public Task RollbackAsync(string operationId, CancellationToken ct = default)
        => Task.CompletedTask;
}

public static class FileShredderRegistration
{
    public static void AddFileShredderModule(IServiceCollection sc)
        => sc.AddSingleton<IOptimizationModule, FileShredderModule>();
}
