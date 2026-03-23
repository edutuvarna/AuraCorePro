using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.DependencyInjection;
using AuraCore.Application;
using AuraCore.Application.Interfaces.Modules;
using AuraCore.Domain.Enums;
using AuraCore.Module.RamOptimizer.Models;

namespace AuraCore.Module.RamOptimizer;

public sealed class RamOptimizerModule : IOptimizationModule
{
    public string Id => "ram-optimizer";
    public string DisplayName => "RAM Optimizer";
    public OptimizationCategory Category => OptimizationCategory.MemoryOptimization;
    public RiskLevel Risk => RiskLevel.Medium;

    public RamReport? LastReport { get; private set; }

    // System-critical processes that should never be killed
    private static readonly HashSet<string> EssentialProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "system", "idle", "smss", "csrss", "wininit", "winlogon", "services",
        "lsass", "lsaiso", "svchost", "fontdrvhost", "dwm", "explorer",
        "taskhostw", "sihost", "ctfmon", "conhost", "runtimebroker",
        "securityhealthservice", "securityhealthsystray", "msmpeng",
        "nissrv", "registry", "memorycompression", "systemsettings"
    };

    // Known browser processes
    private static readonly HashSet<string> BrowserProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "chrome", "msedge", "firefox", "opera", "brave", "vivaldi", "arc"
    };

    // Known heavy background apps
    private static readonly HashSet<string> BackgroundProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "onedrive", "teams", "slack", "discord", "spotify", "steam",
        "epicgameslauncher", "goggalaxy", "dropbox", "googledrivesync",
        "adobenotificationclient", "ccxprocess", "adobeipcbroker",
        "skypeapp", "yourphone", "gamebar", "gamebarpresencewriter",
        "searchhost", "startmenuexperiencehost", "textinputhost",
        "widgetservice", "widgets"
    };

    public async Task<ScanResult> ScanAsync(ScanOptions options, CancellationToken ct = default)
    {
        var report = await Task.Run(() => BuildReport(), ct);
        LastReport = report;
        return new ScanResult(Id, true, report.TopProcesses.Count, report.TotalReclaimableBytes);
    }

    public async Task<OptimizationResult> OptimizeAsync(
        OptimizationPlan plan, IProgress<TaskProgress>? progress = null, CancellationToken ct = default)
    {
        var start = DateTime.UtcNow;
        long freedBytes = 0;
        int optimized = 0;

        var processes = Process.GetProcesses();
        int total = processes.Length;
        int processed = 0;

        await Task.Run(() =>
        {
            foreach (var proc in processes)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var name = proc.ProcessName.ToLowerInvariant();

                    // Skip essential system processes
                    if (EssentialProcesses.Contains(name)) continue;

                    // Skip our own process
                    if (proc.Id == Environment.ProcessId) continue;

                    var beforeMem = proc.WorkingSet64;

                    // EmptyWorkingSet trims the working set — pages move to standby list
                    // They come back instantly if needed, so this is safe
                    if (EmptyWorkingSet(proc.Handle))
                    {
                        proc.Refresh();
                        var afterMem = proc.WorkingSet64;
                        var freed = beforeMem - afterMem;
                        if (freed > 0)
                        {
                            freedBytes += freed;
                            optimized++;
                        }
                    }
                }
                catch { }
                finally
                {
                    try { proc.Dispose(); } catch { }
                }

                processed++;
                progress?.Report(new TaskProgress(Id,
                    total > 0 ? (double)processed / total * 100 : 100,
                    $"Optimizing: {processed}/{total} processes"));
            }
        }, ct);

        return new OptimizationResult(Id, Guid.NewGuid().ToString(), true, optimized, freedBytes, DateTime.UtcNow - start);
    }

    public Task<bool> CanRollbackAsync(string operationId, CancellationToken ct = default)
        => Task.FromResult(false); // Memory optimization is naturally reversible — apps reclaim memory as needed

    public Task RollbackAsync(string operationId, CancellationToken ct = default)
        => Task.CompletedTask;

    private RamReport BuildReport()
    {
        // Get memory stats
        var memInfo = new MEMORYSTATUSEX();
        GlobalMemoryStatusEx(ref memInfo);

        var totalGb = memInfo.ullTotalPhys / (1024.0 * 1024 * 1024);
        var availGb = memInfo.ullAvailPhys / (1024.0 * 1024 * 1024);
        var usedGb = totalGb - availGb;

        // Get all processes sorted by working set
        var procList = new List<ProcessMemoryInfo>();
        long reclaimable = 0;

        foreach (var proc in Process.GetProcesses())
        {
            try
            {
                var name = proc.ProcessName;
                var nameLower = name.ToLowerInvariant();
                var ws = proc.WorkingSet64;
                var priv = proc.PrivateMemorySize64;
                var essential = EssentialProcesses.Contains(nameLower);

                var category = essential ? "System" :
                    BrowserProcesses.Contains(nameLower) ? "Browser" :
                    BackgroundProcesses.Contains(nameLower) ? "Background" :
                    name.StartsWith("svchost", StringComparison.OrdinalIgnoreCase) ? "Service" :
                    "Application";

                // Non-essential processes with >20MB working set are reclaimable
                if (!essential && ws > 20 * 1024 * 1024)
                    reclaimable += ws / 4; // Estimate ~25% is reclaimable via EmptyWorkingSet

                procList.Add(new ProcessMemoryInfo
                {
                    Pid = proc.Id,
                    Name = name,
                    WorkingSetBytes = ws,
                    PrivateBytes = priv,
                    Category = category,
                    IsEssential = essential
                });
            }
            catch { }
            finally
            {
                try { proc.Dispose(); } catch { }
            }
        }

        // Sort by working set descending, take top 50
        procList.Sort((a, b) => b.WorkingSetBytes.CompareTo(a.WorkingSetBytes));

        return new RamReport
        {
            TotalGb = totalGb,
            UsedGb = usedGb,
            AvailableGb = availGb,
            UsagePercent = (int)memInfo.dwMemoryLoad,
            TopProcesses = procList.Take(50).ToList(),
            TotalReclaimableBytes = reclaimable
        };
    }

    [DllImport("psapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EmptyWorkingSet(IntPtr hProcess);

    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORYSTATUSEX
    {
        internal uint dwLength;
        internal uint dwMemoryLoad;
        internal ulong ullTotalPhys;
        internal ulong ullAvailPhys;
        internal ulong ullTotalPageFile;
        internal ulong ullAvailPageFile;
        internal ulong ullTotalVirtual;
        internal ulong ullAvailVirtual;
        internal ulong ullAvailExtendedVirtual;
        public MEMORYSTATUSEX() { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>(); }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);
}

public static class RamOptimizerRegistration
{
    public static IServiceCollection AddRamOptimizerModule(
        this IServiceCollection services)
        => services.AddSingleton<IOptimizationModule, RamOptimizerModule>();
}
