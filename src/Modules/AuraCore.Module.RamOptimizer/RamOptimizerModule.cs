using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using AuraCore.Application;
using AuraCore.Application.Interfaces.Modules;
using AuraCore.Domain.Enums;
using AuraCore.Module.RamOptimizer.Models;

namespace AuraCore.Module.RamOptimizer;

/// <summary>Persisted whitelist/blacklist configuration.</summary>
public sealed class RamOptimizerConfig
{
    public List<string> Whitelist { get; set; } = new(); // never optimize
    public List<string> Blacklist { get; set; } = new(); // always optimize first
}

public sealed class RamOptimizerModule : IOptimizationModule
{
    public string Id => "ram-optimizer";
    public string DisplayName => "RAM Optimizer";
    public OptimizationCategory Category => OptimizationCategory.MemoryOptimization;
    public RiskLevel Risk => RiskLevel.Medium;
    public SupportedPlatform Platform => SupportedPlatform.All;

    public RamReport? LastReport { get; private set; }

    /// <summary>Loaded whitelist/blacklist config.</summary>
    public RamOptimizerConfig Config { get; private set; } = new();

    private static readonly string ConfigDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AuraCorePro");
    private static readonly string ConfigPath = Path.Combine(ConfigDir, "ram_optimizer.json");

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

    public RamOptimizerModule()
    {
        LoadConfig();
    }

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

        var whitelistSet = new HashSet<string>(Config.Whitelist, StringComparer.OrdinalIgnoreCase);
        var blacklistSet = new HashSet<string>(Config.Blacklist, StringComparer.OrdinalIgnoreCase);

        var processes = Process.GetProcesses();
        // Sort: blacklisted first so they are always optimized
        var sorted = processes.OrderByDescending(p =>
        {
            try { return blacklistSet.Contains(p.ProcessName) ? 1 : 0; } catch { return 0; }
        }).ToArray();
        int total = sorted.Length;
        int processed = 0;

        await Task.Run(() =>
        {
            foreach (var proc in sorted)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var name = proc.ProcessName.ToLowerInvariant();

                    // Skip essential system processes
                    if (EssentialProcesses.Contains(name)) continue;

                    // Skip our own process
                    if (proc.Id == Environment.ProcessId) continue;

                    // Skip whitelisted processes
                    if (whitelistSet.Contains(name)) continue;

                    var beforeMem = proc.WorkingSet64;

                    // EmptyWorkingSet trims the working set — pages move to standby list
                    // Windows only — Linux uses different approach
                    if (OperatingSystem.IsWindows() && EmptyWorkingSet(proc.Handle))
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
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[RamOptimizer] EmptyWorkingSet failed: {ex.Message}"); }
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

    /// <summary>Aggressive boost: EmptyWorkingSet on ALL non-essential processes regardless of size.</summary>
    public async Task<OptimizationResult> BoostOptimizeAsync(
        IProgress<TaskProgress>? progress = null, CancellationToken ct = default)
    {
        var start = DateTime.UtcNow;
        long freedBytes = 0;
        int optimized = 0;

        var whitelistSet = new HashSet<string>(Config.Whitelist, StringComparer.OrdinalIgnoreCase);
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
                    if (EssentialProcesses.Contains(name)) continue;
                    if (proc.Id == Environment.ProcessId) continue;
                    if (whitelistSet.Contains(name)) continue;

                    var beforeMem = proc.WorkingSet64;
                    if (OperatingSystem.IsWindows() && EmptyWorkingSet(proc.Handle))
                    {
                        proc.Refresh();
                        var afterMem = proc.WorkingSet64;
                        var freed = beforeMem - afterMem;
                        if (freed > 0) { freedBytes += freed; optimized++; }
                    }
                }
                catch { }
                finally { try { proc.Dispose(); } catch { } }
                processed++;
                progress?.Report(new TaskProgress(Id,
                    total > 0 ? (double)processed / total * 100 : 100,
                    $"Boost: {processed}/{total} processes"));
            }
        }, ct);

        return new OptimizationResult(Id, Guid.NewGuid().ToString(), true, optimized, freedBytes, DateTime.UtcNow - start);
    }

    /// <summary>Check if a process name is whitelisted (never optimize).</summary>
    public bool IsWhitelisted(string processName) =>
        Config.Whitelist.Contains(processName, StringComparer.OrdinalIgnoreCase);

    /// <summary>Check if a process name is blacklisted (always optimize first).</summary>
    public bool IsBlacklisted(string processName) =>
        Config.Blacklist.Contains(processName, StringComparer.OrdinalIgnoreCase);

    /// <summary>Toggle whitelist status for a process.</summary>
    public void ToggleWhitelist(string processName)
    {
        var idx = Config.Whitelist.FindIndex(n => n.Equals(processName, StringComparison.OrdinalIgnoreCase));
        if (idx >= 0) Config.Whitelist.RemoveAt(idx);
        else { Config.Whitelist.Add(processName); RemoveFromBlacklist(processName); }
        SaveConfig();
    }

    /// <summary>Toggle blacklist status for a process.</summary>
    public void ToggleBlacklist(string processName)
    {
        var idx = Config.Blacklist.FindIndex(n => n.Equals(processName, StringComparison.OrdinalIgnoreCase));
        if (idx >= 0) Config.Blacklist.RemoveAt(idx);
        else { Config.Blacklist.Add(processName); RemoveFromWhitelist(processName); }
        SaveConfig();
    }

    private void RemoveFromWhitelist(string name) =>
        Config.Whitelist.RemoveAll(n => n.Equals(name, StringComparison.OrdinalIgnoreCase));
    private void RemoveFromBlacklist(string name) =>
        Config.Blacklist.RemoveAll(n => n.Equals(name, StringComparison.OrdinalIgnoreCase));

    public void LoadConfig()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                Config = JsonSerializer.Deserialize<RamOptimizerConfig>(json) ?? new();
            }
        }
        catch { Config = new(); }
    }

    public void SaveConfig()
    {
        try
        {
            Directory.CreateDirectory(ConfigDir);
            var json = JsonSerializer.Serialize(Config, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ConfigPath, json);
        }
        catch { }
    }

    public Task<bool> CanRollbackAsync(string operationId, CancellationToken ct = default)
        => Task.FromResult(false); // Memory optimization is naturally reversible — apps reclaim memory as needed

    public Task RollbackAsync(string operationId, CancellationToken ct = default)
        => Task.CompletedTask;

    private RamReport BuildReport()
    {
        double totalGb = 0, usedGb = 0, availGb = 0;
        int usagePct = 0;

        if (OperatingSystem.IsWindows())
        {
            var memInfo = new MEMORYSTATUSEX();
            GlobalMemoryStatusEx(ref memInfo);
            totalGb = memInfo.ullTotalPhys / (1024.0 * 1024 * 1024);
            availGb = memInfo.ullAvailPhys / (1024.0 * 1024 * 1024);
            usedGb = totalGb - availGb;
            usagePct = (int)memInfo.dwMemoryLoad;
        }
        else if (OperatingSystem.IsLinux())
        {
            try
            {
                long totalKb = 0, availKb = 0;
                foreach (var line in File.ReadLines("/proc/meminfo"))
                {
                    if (line.StartsWith("MemTotal:"))
                        totalKb = ParseKb(line);
                    else if (line.StartsWith("MemAvailable:"))
                        availKb = ParseKb(line);
                }
                totalGb = totalKb / (1024.0 * 1024);
                availGb = availKb / (1024.0 * 1024);
                usedGb = totalGb - availGb;
                usagePct = totalGb > 0 ? (int)((usedGb / totalGb) * 100) : 0;
            }
            catch { }
        }

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
            UsagePercent = usagePct,
            TopProcesses = procList.Take(50).ToList(),
            TotalReclaimableBytes = reclaimable
        };
    }

    private static long ParseKb(string line)
    {
        var parts = line.Split(':', StringSplitOptions.TrimEntries);
        if (parts.Length < 2) return 0;
        var numStr = parts[1].Replace("kB", "").Trim();
        return long.TryParse(numStr, out var kb) ? kb : 0;
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
