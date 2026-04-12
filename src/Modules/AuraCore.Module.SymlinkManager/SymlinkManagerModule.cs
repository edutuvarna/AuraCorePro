using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using AuraCore.Application;
using AuraCore.Application.Interfaces.Modules;
using AuraCore.Domain.Enums;
using AuraCore.Module.SymlinkManager.Models;

namespace AuraCore.Module.SymlinkManager;

public sealed class SymlinkManagerModule : IOptimizationModule
{
    public string Id => "symlink-manager";
    public string DisplayName => "Symlink Manager";
    public OptimizationCategory Category => OptimizationCategory.ShellCustomization;
    public RiskLevel Risk => RiskLevel.Medium;
    public SupportedPlatform Platform => SupportedPlatform.Linux;

    public SymlinkReport? LastReport { get; private set; }

    private static readonly string[] DefaultScanDirs =
    {
        "/usr/local/bin",
        // Home-relative — will be prefixed with HOME in Scan
        "~/.local/bin",
        "/usr/bin",
        "/etc/systemd/system",
    };

    // Only these directories are eligible for broken-symlink removal
    private static readonly string[] MutableDirs =
    {
        "/usr/local/bin",
        "~/.local/bin",
    };

    public async Task<ScanResult> ScanAsync(ScanOptions options, CancellationToken ct = default)
    {
        if (!OperatingSystem.IsLinux())
            return new ScanResult(Id, false, 0, 0, $"{DisplayName} is Linux-only.");

        try
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var symlinks = new List<SymlinkInfo>();
            var scannedDirs = new List<string>();

            foreach (var dir in DefaultScanDirs)
            {
                ct.ThrowIfCancellationRequested();
                var resolvedDir = dir.StartsWith('~') && !string.IsNullOrEmpty(home)
                    ? Path.Combine(home, dir.TrimStart('~', '/'))
                    : dir;

                if (!Directory.Exists(resolvedDir)) continue;

                scannedDirs.Add(resolvedDir);
                symlinks.AddRange(ScanDirectory(resolvedDir, ct));
            }

            var broken = symlinks.Count(s => s.Status == SymlinkStatus.Broken);
            var circular = symlinks.Count(s => s.Status == SymlinkStatus.CircularRef);

            var report = new SymlinkReport(
                Symlinks: symlinks,
                TotalCount: symlinks.Count,
                BrokenCount: broken,
                CircularCount: circular,
                ScannedDirectories: scannedDirs,
                IsAvailable: true);

            LastReport = report;
            return await Task.FromResult(new ScanResult(Id, true, broken + circular, 0));
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            Debug.WriteLine($"[{Id}] Scan error: {ex.Message}");
            return new ScanResult(Id, false, 0, 0, ex.Message);
        }
    }

    public async Task<OptimizationResult> OptimizeAsync(
        OptimizationPlan plan,
        IProgress<TaskProgress>? progress = null,
        CancellationToken ct = default)
    {
        var start = DateTime.UtcNow;
        var operationId = Guid.NewGuid().ToString("N")[..8];
        int processed = 0;

        if (!OperatingSystem.IsLinux())
            return new OptimizationResult(Id, operationId, false, 0, 0, TimeSpan.Zero);

        try
        {
            // Ensure LastReport populated
            if (LastReport == null)
                await ScanAsync(new ScanOptions(), ct);
            if (LastReport == null || !LastReport.IsAvailable)
                return new OptimizationResult(Id, operationId, false, 0, 0, DateTime.UtcNow - start);

            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var mutableSet = new HashSet<string>(MutableDirs.Select(d =>
                d.StartsWith('~') && !string.IsNullOrEmpty(home)
                    ? Path.Combine(home, d.TrimStart('~', '/'))
                    : d));

            var brokenMap = LastReport.Symlinks
                .Where(s => s.Status == SymlinkStatus.Broken || s.Status == SymlinkStatus.CircularRef)
                .ToDictionary(s => s.Path, s => s, StringComparer.Ordinal);

            var items = plan.SelectedItemIds?.ToList() ?? new List<string>();
            int totalSteps = Math.Max(items.Count, 1);

            for (int i = 0; i < items.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                var itemId = items[i];
                progress?.Report(new TaskProgress(Id, (double)i / totalSteps * 100, $"Processing {itemId}..."));

                if (itemId == "remove-all-broken")
                {
                    foreach (var entry in brokenMap.Values)
                    {
                        ct.ThrowIfCancellationRequested();
                        if (TryRemoveBroken(entry, mutableSet)) processed++;
                    }
                }
                else if (itemId.StartsWith("remove-broken:", StringComparison.Ordinal))
                {
                    var path = itemId["remove-broken:".Length..];
                    if (brokenMap.TryGetValue(path, out var entry) && TryRemoveBroken(entry, mutableSet))
                        processed++;
                }
            }

            progress?.Report(new TaskProgress(Id, 100, "Complete"));
            return new OptimizationResult(Id, operationId, true, processed, 0, DateTime.UtcNow - start);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            Debug.WriteLine($"[{Id}] Optimize error: {ex.Message}");
            return new OptimizationResult(Id, operationId, false, processed, 0, DateTime.UtcNow - start);
        }
    }

    public Task<bool> CanRollbackAsync(string operationId, CancellationToken ct = default) => Task.FromResult(false);
    public Task RollbackAsync(string operationId, CancellationToken ct = default) => Task.CompletedTask;

    // ---- Helpers ----

    private static List<SymlinkInfo> ScanDirectory(string dir, CancellationToken ct)
    {
        var list = new List<SymlinkInfo>();
        try
        {
            var entries = Directory.EnumerateFileSystemEntries(dir, "*", SearchOption.TopDirectoryOnly);
            foreach (var entry in entries)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var info = new FileInfo(entry);
                    if ((info.Attributes & FileAttributes.ReparsePoint) == 0) continue; // not a symlink

                    var linkTarget = info.LinkTarget;
                    var status = ClassifySymlink(entry, linkTarget);

                    list.Add(new SymlinkInfo(entry, linkTarget, status));
                }
                catch { /* skip inaccessible entries */ }
            }
        }
        catch { /* skip inaccessible dirs */ }
        return list;
    }

    private static SymlinkStatus ClassifySymlink(string linkPath, string? linkTarget)
    {
        if (string.IsNullOrEmpty(linkTarget)) return SymlinkStatus.Broken;

        // Resolve target (may be relative)
        string resolvedTarget;
        if (Path.IsPathRooted(linkTarget))
            resolvedTarget = linkTarget;
        else
        {
            var parent = Path.GetDirectoryName(linkPath);
            resolvedTarget = parent != null ? Path.Combine(parent, linkTarget) : linkTarget;
        }

        // Detect circular self-reference
        try
        {
            if (string.Equals(Path.GetFullPath(resolvedTarget), Path.GetFullPath(linkPath), StringComparison.Ordinal))
                return SymlinkStatus.CircularRef;
        }
        catch { /* fallthrough */ }

        try
        {
            if (File.Exists(resolvedTarget) || Directory.Exists(resolvedTarget))
                return SymlinkStatus.Valid;
        }
        catch { /* fallthrough */ }

        return SymlinkStatus.Broken;
    }

    private static bool TryRemoveBroken(SymlinkInfo info, HashSet<string> mutableDirs)
    {
        // Only remove from mutable directories (safety — never touch /usr/bin etc.)
        var parent = Path.GetDirectoryName(info.Path);
        if (parent == null || !mutableDirs.Contains(parent)) return false;

        try
        {
            File.Delete(info.Path);
            return true;
        }
        catch
        {
            return false;
        }
    }
}

public static class SymlinkManagerRegistration
{
    public static IServiceCollection AddSymlinkManagerModule(this IServiceCollection services)
        => services.AddSingleton<IOptimizationModule, SymlinkManagerModule>();
}
