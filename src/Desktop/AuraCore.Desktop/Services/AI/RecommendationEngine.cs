using System.Runtime.InteropServices;
using Microsoft.Extensions.DependencyInjection;
using AuraCore.Application;
using AuraCore.Application.Interfaces.Modules;

namespace AuraCore.Desktop.Services.AI;

public sealed record Recommendation
{
    public string Id { get; init; } = "";
    public string Title { get; init; } = "";
    public string Description { get; init; } = "";
    public string ModuleId { get; init; } = "";
    public string ActionLabel { get; init; } = "Fix";
    public RecommendationPriority Priority { get; init; }
    public string Category { get; init; } = "";
    public string Icon { get; init; } = "E7BA";
}

public enum RecommendationPriority
{
    Low,
    Medium,
    High,
    Critical
}

public sealed class RecommendationEngine
{
    private readonly IServiceProvider _services;
    public RecommendationEngine(IServiceProvider services) => _services = services;

    public async Task<List<Recommendation>> AnalyzeAsync(CancellationToken ct = default)
    {
        var recs = new List<Recommendation>();

        await Task.Run(() =>
        {
            AnalyzeMemory(recs);
            AnalyzeDisk(recs);
            AnalyzeProcesses(recs);
            AnalyzeStartup(recs);
            AnalyzeSystem(recs);
        }, ct);

        // Sort by priority (Critical first)
        recs.Sort((a, b) => b.Priority.CompareTo(a.Priority));
        return recs;
    }

    private static void AnalyzeMemory(List<Recommendation> recs)
    {
        var mem = new NativeMemory.MEMORYSTATUSEX();
        if (!NativeMemory.GlobalMemoryStatusEx(ref mem)) return;

        var usedPct = (int)mem.dwMemoryLoad;
        var availGb = mem.ullAvailPhys / (1024.0 * 1024 * 1024);
        var totalGb = mem.ullTotalPhys / (1024.0 * 1024 * 1024);

        if (usedPct >= 90)
        {
            recs.Add(new Recommendation
            {
                Id = "ram-critical", Title = "RAM Usage Critical",
                Description = $"Your system is using {usedPct}% of RAM ({totalGb - availGb:F1} / {totalGb:F1} GB). Applications may become unresponsive. Run RAM Optimizer immediately.",
                ModuleId = "ram-optimizer", ActionLabel = "Optimize RAM",
                Priority = RecommendationPriority.Critical, Category = "Performance", Icon = "E7F7"
            });
        }
        else if (usedPct >= 75)
        {
            recs.Add(new Recommendation
            {
                Id = "ram-high", Title = "High Memory Usage",
                Description = $"RAM is at {usedPct}% ({availGb:F1} GB free). Consider closing unused apps or running the RAM optimizer.",
                ModuleId = "ram-optimizer", ActionLabel = "Optimize RAM",
                Priority = RecommendationPriority.Medium, Category = "Performance", Icon = "E7F7"
            });
        }

        if (totalGb < 8)
        {
            recs.Add(new Recommendation
            {
                Id = "ram-low-total", Title = "Low System RAM",
                Description = $"Your system has only {totalGb:F1} GB of RAM. For better performance, consider upgrading to at least 16 GB.",
                ModuleId = "", ActionLabel = "",
                Priority = RecommendationPriority.Low, Category = "Hardware", Icon = "E7F7"
            });
        }
    }

    private static void AnalyzeDisk(List<Recommendation> recs)
    {
        try
        {
            var c = new DriveInfo("C");
            if (!c.IsReady) return;

            var totalGb = c.TotalSize / (1024.0 * 1024 * 1024);
            var freeGb = c.AvailableFreeSpace / (1024.0 * 1024 * 1024);
            var freePct = freeGb / totalGb * 100;

            if (freePct < 5)
            {
                recs.Add(new Recommendation
                {
                    Id = "disk-critical", Title = "Disk Space Critical",
                    Description = $"Only {freeGb:F1} GB free on C: ({freePct:F0}%). Windows may stop working properly. Clean junk files and enable storage compression immediately.",
                    ModuleId = "junk-cleaner", ActionLabel = "Clean Junk",
                    Priority = RecommendationPriority.Critical, Category = "Storage", Icon = "EDA2"
                });
            }
            else if (freePct < 15)
            {
                recs.Add(new Recommendation
                {
                    Id = "disk-low", Title = "Low Disk Space",
                    Description = $"{freeGb:F1} GB free on C: ({freePct:F0}%). Run junk cleaner to free space, or enable storage compression to reclaim 2-5 GB.",
                    ModuleId = "junk-cleaner", ActionLabel = "Clean Junk",
                    Priority = RecommendationPriority.High, Category = "Storage", Icon = "EDA2"
                });
            }

            if (freePct < 25 && freePct >= 5)
            {
                recs.Add(new Recommendation
                {
                    Id = "disk-compress", Title = "Enable Storage Compression",
                    Description = "You can save 2-5 GB by compressing Windows system files with CompactOS. No performance impact on modern SSDs.",
                    ModuleId = "storage-compression", ActionLabel = "Open Compression",
                    Priority = RecommendationPriority.Medium, Category = "Storage", Icon = "EDA2"
                });
            }
        }
        catch { }
    }

    private static void AnalyzeProcesses(List<Recommendation> recs)
    {
        try
        {
            var processes = System.Diagnostics.Process.GetProcesses();
            int totalProcs = processes.Length;
            long totalMemMb = 0;
            int heavyProcs = 0;

            foreach (var p in processes)
            {
                try
                {
                    var memMb = p.WorkingSet64 / (1024 * 1024);
                    totalMemMb += memMb;
                    if (memMb > 500) heavyProcs++;
                }
                catch { }
                finally { try { p.Dispose(); } catch { } }
            }

            if (totalProcs > 200)
            {
                recs.Add(new Recommendation
                {
                    Id = "proc-many", Title = "Too Many Running Processes",
                    Description = $"{totalProcs} processes are running. This can slow down your system. Check for unnecessary startup programs and bloatware.",
                    ModuleId = "bloatware-removal", ActionLabel = "Remove Bloatware",
                    Priority = RecommendationPriority.Medium, Category = "Performance", Icon = "EA99"
                });
            }

            if (heavyProcs > 5)
            {
                recs.Add(new Recommendation
                {
                    Id = "proc-heavy", Title = "Memory-Heavy Processes",
                    Description = $"{heavyProcs} processes are using over 500 MB each. Use RAM Optimizer to see which ones and free memory.",
                    ModuleId = "ram-optimizer", ActionLabel = "View Processes",
                    Priority = RecommendationPriority.Medium, Category = "Performance", Icon = "E7F7"
                });
            }
        }
        catch { }
    }

    private static void AnalyzeStartup(List<Recommendation> recs)
    {
        // Check uptime — if too long, suggest restart
        var uptime = TimeSpan.FromMilliseconds(Environment.TickCount64);
        if (uptime.TotalDays > 7)
        {
            recs.Add(new Recommendation
            {
                Id = "uptime-long", Title = "Restart Your PC",
                Description = $"Your PC has been running for {(int)uptime.TotalDays} days. Restarting clears memory leaks and applies pending updates.",
                ModuleId = "", ActionLabel = "",
                Priority = RecommendationPriority.Medium, Category = "Maintenance", Icon = "E777"
            });
        }
    }

    private static void AnalyzeSystem(List<Recommendation> recs)
    {
        // Temp folder size check
        try
        {
            var tempPath = Path.GetTempPath();
            if (Directory.Exists(tempPath))
            {
                long totalSize = 0;
                foreach (var f in Directory.EnumerateFiles(tempPath, "*.*", new EnumerationOptions { IgnoreInaccessible = true, RecurseSubdirectories = true }))
                {
                    try { totalSize += new FileInfo(f).Length; } catch { }
                }
                var sizeMb = totalSize / (1024.0 * 1024);
                if (sizeMb > 500)
                {
                    recs.Add(new Recommendation
                    {
                        Id = "temp-large", Title = "Large Temp Folder",
                        Description = $"Your temp folder is {sizeMb:F0} MB. Run Junk Cleaner to safely remove temporary files.",
                        ModuleId = "junk-cleaner", ActionLabel = "Clean Temp",
                        Priority = RecommendationPriority.Medium, Category = "Cleanup", Icon = "E74D"
                    });
                }
            }
        }
        catch { }

        // File extensions hidden check (security recommendation)
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced");
            var hideExt = key?.GetValue("HideFileExt");
            if (hideExt is int val && val == 1)
            {
                recs.Add(new Recommendation
                {
                    Id = "security-ext", Title = "Show File Extensions",
                    Description = "File extensions are hidden. This is a security risk — malware can disguise itself as documents (e.g., invoice.pdf.exe). Enable 'Show File Extensions' in Explorer Tweaks.",
                    ModuleId = "explorer-tweaks", ActionLabel = "Fix in Explorer",
                    Priority = RecommendationPriority.High, Category = "Security", Icon = "E72E"
                });
            }
        }
        catch { }

        // Schedule recommendation
        recs.Add(new Recommendation
        {
            Id = "schedule-tip", Title = "Enable Auto-Scheduling",
            Description = "Set up automatic junk cleaning and RAM optimization to keep your system fast without manual effort.",
            ModuleId = "scheduler", ActionLabel = "Set Up",
            Priority = RecommendationPriority.Low, Category = "Tip", Icon = "E823"
        });
    }
}
