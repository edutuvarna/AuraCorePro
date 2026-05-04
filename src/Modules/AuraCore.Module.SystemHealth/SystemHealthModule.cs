using System.Diagnostics;
using System.Management;
using System.Runtime.InteropServices;
using Microsoft.Extensions.DependencyInjection;
using AuraCore.Application;
using AuraCore.Application.Interfaces.Modules;
using AuraCore.Domain.Enums;
using AuraCore.Module.SystemHealth.Models;

namespace AuraCore.Module.SystemHealth;

public sealed class SystemHealthModule : IOptimizationModule
{
    public string Id => "system-health";
    public string DisplayName => "System health analyzer";
    public OptimizationCategory Category => OptimizationCategory.SystemHealth;
    public RiskLevel Risk => RiskLevel.Low;
    public SupportedPlatform Platform => SupportedPlatform.All;

    public SystemHealthReport? LastReport { get; private set; }

    public async Task<ScanResult> ScanAsync(ScanOptions options, CancellationToken ct = default)
    {
        var report = await Task.Run(() => BuildReport(), ct);
        LastReport = report;

        var itemsFound = 4 + report.Drives.Count; // OS, CPU, RAM, processes + drives
        return new ScanResult(Id, true, itemsFound, 0);
    }

    public async Task<OptimizationResult> OptimizeAsync(
        OptimizationPlan plan,
        IProgress<TaskProgress>? progress = null,
        CancellationToken ct = default)
    {
        var start = DateTime.UtcNow;
        var operationId = Guid.NewGuid().ToString("N")[..8];
        int totalProcessed = 0;
        long totalFreed = 0;

        try
        {
            var items = plan.SelectedItemIds?.ToList() ?? new List<string>();
            int totalSteps = Math.Max(items.Count, 1);

            for (int i = 0; i < items.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                var itemId = items[i];
                double pct = (double)i / totalSteps * 100;
                progress?.Report(new TaskProgress(Id, pct, $"Processing {itemId}"));

                switch (itemId)
                {
                    case "clean-temp":
                        var (tempCount, tempBytes) = await CleanTempFilesAsync(ct);
                        totalProcessed += tempCount;
                        totalFreed += tempBytes;
                        break;

                    case "trim-logs":
                        var (logCount, logBytes) = await TrimLargeLogsAsync(ct);
                        totalProcessed += logCount;
                        totalFreed += logBytes;
                        break;

                    default:
                        // Unknown item ID — skip silently
                        break;
                }
            }

            progress?.Report(new TaskProgress(Id, 100, "Complete"));
            return new OptimizationResult(Id, operationId, true, totalProcessed, totalFreed, DateTime.UtcNow - start);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[{Id}] Optimize error: {ex.Message}");
            return new OptimizationResult(Id, operationId, false, totalProcessed, totalFreed, DateTime.UtcNow - start);
        }
    }

    private static async Task<(int processed, long freed)> CleanTempFilesAsync(CancellationToken ct)
    {
        var tempPath = Path.GetTempPath();
        var cutoff = DateTime.UtcNow.AddDays(-7);
        int count = 0;
        long bytes = 0;

        try
        {
            var files = Directory.EnumerateFiles(tempPath, "*", new System.IO.EnumerationOptions
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible = true,
                AttributesToSkip = FileAttributes.ReparsePoint
            });

            foreach (var f in files)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var info = new FileInfo(f);
                    if (info.LastWriteTimeUtc < cutoff && (info.Attributes & FileAttributes.ReadOnly) == 0)
                    {
                        long size = info.Length;
                        File.Delete(f);
                        count++;
                        bytes += size;
                    }
                }
                catch
                {
                    // Skip files that can't be accessed or deleted
                }
            }
        }
        catch (OperationCanceledException) { throw; }
        catch
        {
            // Swallow top-level enumeration errors
        }

        return await Task.FromResult((count, bytes));
    }

    private static async Task<(int processed, long freed)> TrimLargeLogsAsync(CancellationToken ct)
    {
        var logDirs = new List<string>();

        if (OperatingSystem.IsLinux())
        {
            logDirs.Add("/var/log");
        }
        else if (OperatingSystem.IsMacOS())
        {
            logDirs.Add("/var/log");
            logDirs.Add("/Library/Logs");
        }
        else if (OperatingSystem.IsWindows())
        {
            var systemRoot = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            logDirs.Add(Path.Combine(systemRoot, "Logs"));
        }

        int count = 0;
        long bytes = 0;
        const long maxSize = 100L * 1024 * 1024; // 100 MB

        foreach (var dir in logDirs)
        {
            if (!Directory.Exists(dir)) continue;

            try
            {
                var files = Directory.EnumerateFiles(dir, "*.log", new System.IO.EnumerationOptions
                {
                    RecurseSubdirectories = true,
                    IgnoreInaccessible = true,
                    AttributesToSkip = FileAttributes.ReparsePoint
                });

                foreach (var f in files)
                {
                    ct.ThrowIfCancellationRequested();
                    try
                    {
                        var info = new FileInfo(f);
                        if (info.Length > maxSize)
                        {
                            long originalSize = info.Length;
                            var archivePath = f + ".old";
                            // Delete old archive if exists
                            if (File.Exists(archivePath)) File.Delete(archivePath);
                            // Rename current to .old
                            File.Move(f, archivePath);
                            // Create empty replacement
                            File.Create(f).Dispose();
                            count++;
                            bytes += originalSize;
                        }
                    }
                    catch
                    {
                        // Skip files we can't process
                    }
                }
            }
            catch
            {
                // Skip directories we can't enumerate
            }
        }

        return await Task.FromResult((count, bytes));
    }

    public Task<bool> CanRollbackAsync(string operationId, CancellationToken ct = default)
        => Task.FromResult(false);

    public Task RollbackAsync(string operationId, CancellationToken ct = default)
        => Task.CompletedTask;

    private static SystemHealthReport BuildReport()
    {
        // OS — cross-platform
        var osName = RuntimeInformation.OSDescription;
        var osVer = Environment.OSVersion.Version.ToString();
        var arch = RuntimeInformation.OSArchitecture.ToString();
        var machine = Environment.MachineName;
        var uptime = TimeSpan.FromMilliseconds(Environment.TickCount64);

        // CPU
        var cpuName = GetCpuName();

        // RAM
        var (totalRamGb, availRamGb, ramPct) = GetMemoryInfo();

        // Drives — cross-platform via DriveInfo
        var drives = new List<DriveReport>();
        foreach (var d in DriveInfo.GetDrives().Where(d => d.IsReady))
        {
            try
            {
                // Phase 6.17.A: skip virtual filesystems — they're not user-facing
                // storage and reporting 0-byte capacity caused the -2147483648%
                // display bug ((int)((1 - 0/0) * 100) = (int)NaN = int.MinValue).
                if (IsVirtualFilesystem(d)) continue;

                var totalGb = d.TotalSize / (1024.0 * 1024 * 1024);

                // Phase 6.17.A: zero-capacity guard — even after the virtual-fs
                // filter, some ready drives can momentarily report TotalSize=0
                // (slow USB enumeration, ramdisks). Don't divide by zero.
                if (totalGb <= 0) continue;

                var freeGb  = d.AvailableFreeSpace / (1024.0 * 1024 * 1024);
                var usedPct = (int)Math.Clamp((1.0 - freeGb / totalGb) * 100, 0, 100);
                drives.Add(new DriveReport(d.Name, d.VolumeLabel, d.DriveFormat, totalGb, freeGb, usedPct));
            }
            catch { }
        }

        var procCount = Process.GetProcesses().Length;
        var gpus = GetGpuInfo();
        var battery = GetBatteryInfo();
        var startups = GetStartupEntries();

        // Health score
        var score = 100;
        if (ramPct > 90) score -= 30;
        else if (ramPct > 75) score -= 15;
        else if (ramPct > 60) score -= 5;
        foreach (var drive in drives)
        {
            if (drive.UsedPercent > 95) score -= 25;
            else if (drive.UsedPercent > 85) score -= 10;
        }
        score = Math.Max(0, score);

        return new SystemHealthReport
        {
            OsName = osName, OsVersion = osVer, Architecture = arch,
            MachineName = machine, Uptime = uptime,
            ProcessorCount = Environment.ProcessorCount,
            ProcessorName = cpuName,
            TotalRamGb = totalRamGb, AvailableRamGb = availRamGb, RamUsagePercent = ramPct,
            Drives = drives, Gpus = gpus, Battery = battery,
            StartupPrograms = startups, RunningProcesses = procCount, HealthScore = score
        };
    }

    private static string GetCpuName()
    {
        if (OperatingSystem.IsWindows())
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("select Name from Win32_Processor");
                foreach (ManagementObject obj in searcher.Get())
                    return obj["Name"]?.ToString()?.Trim() ?? "Unknown";
            }
            catch { }
            return Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") ?? "Unknown";
        }
        if (OperatingSystem.IsLinux())
        {
            try
            {
                foreach (var line in File.ReadLines("/proc/cpuinfo"))
                {
                    if (line.StartsWith("model name", StringComparison.OrdinalIgnoreCase))
                        return line.Split(':').ElementAtOrDefault(1)?.Trim() ?? "Unknown";
                }
            }
            catch { }
        }
        if (OperatingSystem.IsMacOS())
        {
            try
            {
                var psi = new ProcessStartInfo("sysctl", "-n machdep.cpu.brand_string")
                { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true };
                using var proc = Process.Start(psi);
                return proc?.StandardOutput.ReadToEnd().Trim() ?? "Unknown";
            }
            catch { }
        }
        return "Unknown";
    }

    private static (double totalGb, double availGb, int pct) GetMemoryInfo()
    {
        if (OperatingSystem.IsWindows())
        {
            var memInfo = new MEMORYSTATUSEX();
            if (GlobalMemoryStatusEx(ref memInfo))
                return (memInfo.ullTotalPhys / (1024.0 * 1024 * 1024),
                        memInfo.ullAvailPhys / (1024.0 * 1024 * 1024),
                        (int)memInfo.dwMemoryLoad);
        }
        if (OperatingSystem.IsLinux())
        {
            try
            {
                long totalKb = 0, availKb = 0;
                foreach (var line in File.ReadLines("/proc/meminfo"))
                {
                    if (line.StartsWith("MemTotal:"))
                        totalKb = ParseMemInfoKb(line);
                    else if (line.StartsWith("MemAvailable:"))
                        availKb = ParseMemInfoKb(line);
                }
                if (totalKb > 0)
                {
                    var totalGb = totalKb / (1024.0 * 1024);
                    var availGb = availKb / (1024.0 * 1024);
                    var pct = (int)((1.0 - availGb / totalGb) * 100);
                    return (totalGb, availGb, pct);
                }
            }
            catch { }
        }
        if (OperatingSystem.IsMacOS())
        {
            try
            {
                var psi = new ProcessStartInfo("sysctl", "-n hw.memsize")
                { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true };
                using var proc = Process.Start(psi);
                var output = proc?.StandardOutput.ReadToEnd().Trim();
                if (long.TryParse(output, out var bytes))
                {
                    var totalGb = bytes / (1024.0 * 1024 * 1024);
                    return (totalGb, totalGb * 0.5, 50); // macOS doesn't easily expose available
                }
            }
            catch { }
        }
        return (0, 0, 0);
    }

    private static long ParseMemInfoKb(string line)
    {
        // "MemTotal:       16384000 kB"
        var parts = line.Split(':', StringSplitOptions.TrimEntries);
        if (parts.Length < 2) return 0;
        var numStr = parts[1].Replace("kB", "").Trim();
        return long.TryParse(numStr, out var kb) ? kb : 0;
    }

    private static List<GpuReport> GetGpuInfo()
    {
        var gpus = new List<GpuReport>();
        if (OperatingSystem.IsWindows())
        {
            try
            {
                using var gpuSearcher = new ManagementObjectSearcher("select Name, DriverVersion, AdapterRAM, VideoModeDescription from Win32_VideoController");
                foreach (ManagementObject obj in gpuSearcher.Get())
                {
                    var adapterRam = obj["AdapterRAM"];
                    var ramMb = adapterRam is uint ram ? ram / (1024 * 1024) : 0;
                    gpus.Add(new GpuReport
                    {
                        Name = obj["Name"]?.ToString()?.Trim() ?? "Unknown",
                        DriverVersion = obj["DriverVersion"]?.ToString() ?? "",
                        VideoMemory = ramMb > 1024 ? $"{ramMb / 1024.0:F1} GB" : $"{ramMb} MB",
                        Resolution = obj["VideoModeDescription"]?.ToString() ?? ""
                    });
                }
            }
            catch { }
        }
        else if (OperatingSystem.IsLinux())
        {
            try
            {
                var psi = new ProcessStartInfo("lspci") { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true };
                using var proc = Process.Start(psi);
                if (proc != null)
                {
                    foreach (var line in proc.StandardOutput.ReadToEnd().Split('\n'))
                    {
                        if (line.Contains("VGA", StringComparison.OrdinalIgnoreCase) ||
                            line.Contains("3D controller", StringComparison.OrdinalIgnoreCase))
                        {
                            var name = line.Contains(':') ? line.Split(':').Last().Trim() : line.Trim();
                            gpus.Add(new GpuReport { Name = name, DriverVersion = "", VideoMemory = "", Resolution = "" });
                        }
                    }
                }
            }
            catch { }
        }
        return gpus;
    }

    private static BatteryReport? GetBatteryInfo()
    {
        if (OperatingSystem.IsWindows())
        {
            try
            {
                using var batSearcher = new ManagementObjectSearcher("select * from Win32_Battery");
                foreach (ManagementObject obj in batSearcher.Get())
                {
                    return new BatteryReport
                    {
                        HasBattery = true,
                        ChargePercent = obj["EstimatedChargeRemaining"] is ushort c ? c : 0,
                        Status = obj["BatteryStatus"] switch
                        {
                            (ushort)1 => "Discharging", (ushort)2 => "Plugged In",
                            (ushort)3 => "Fully Charged", (ushort)4 => "Low",
                            _ => "Unknown"
                        },
                        EstimatedRuntime = "N/A"
                    };
                }
            }
            catch { }
        }
        else if (OperatingSystem.IsLinux())
        {
            try
            {
                var batPath = "/sys/class/power_supply/BAT0";
                if (!Directory.Exists(batPath)) batPath = "/sys/class/power_supply/BAT1";
                if (Directory.Exists(batPath))
                {
                    var capacity = File.ReadAllText(Path.Combine(batPath, "capacity")).Trim();
                    var status = File.ReadAllText(Path.Combine(batPath, "status")).Trim();
                    return new BatteryReport
                    {
                        HasBattery = true,
                        ChargePercent = int.TryParse(capacity, out var c) ? c : 0,
                        Status = status,
                        EstimatedRuntime = "N/A"
                    };
                }
            }
            catch { }
        }
        return null;
    }

    private static List<StartupEntry> GetStartupEntries()
    {
        var startups = new List<StartupEntry>();
        if (OperatingSystem.IsWindows())
        {
            var paths = new[]
            {
                (@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", "HKLM"),
                (@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", "HKCU"),
            };
            foreach (var (regPath, hive) in paths)
            {
                try
                {
                    var root = hive == "HKLM" ? Microsoft.Win32.Registry.LocalMachine : Microsoft.Win32.Registry.CurrentUser;
                    using var key = root.OpenSubKey(regPath);
                    if (key is null) continue;
                    foreach (var name in key.GetValueNames())
                    {
                        var cmd = key.GetValue(name)?.ToString() ?? "";
                        if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(cmd))
                            startups.Add(new StartupEntry { Name = name, Command = cmd, Location = $"{hive}\\{regPath}", Impact = "Medium" });
                    }
                }
                catch { }
            }
        }
        else if (OperatingSystem.IsLinux())
        {
            // XDG autostart
            var autostartDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "autostart");
            if (Directory.Exists(autostartDir))
            {
                foreach (var file in Directory.GetFiles(autostartDir, "*.desktop"))
                {
                    try
                    {
                        var lines = File.ReadAllLines(file);
                        var name = lines.FirstOrDefault(l => l.StartsWith("Name="))?.Split('=', 2).ElementAtOrDefault(1) ?? Path.GetFileNameWithoutExtension(file);
                        var exec = lines.FirstOrDefault(l => l.StartsWith("Exec="))?.Split('=', 2).ElementAtOrDefault(1) ?? "";
                        startups.Add(new StartupEntry { Name = name, Command = exec, Location = "~/.config/autostart", Impact = "Low" });
                    }
                    catch { }
                }
            }
        }
        return startups;
    }

    /// <summary>
    /// Phase 6.17.A: virtual filesystems on Linux/macOS report 0-byte capacity
    /// and would underflow the percent calculation. Drop the well-known
    /// virtual ones; treat everything else (ext4, btrfs, xfs, ntfs, apfs,
    /// hfsplus, exfat, fat32, ...) as real storage.
    /// </summary>
    private static bool IsVirtualFilesystem(DriveInfo d)
    {
        var fmt = d.DriveFormat?.ToLowerInvariant() ?? string.Empty;
        return fmt is "tmpfs" or "devtmpfs" or "proc" or "sysfs" or "devpts"
                    or "securityfs" or "cgroup" or "cgroup2" or "pstore"
                    or "bpf" or "tracefs" or "debugfs" or "configfs"
                    or "fusectl" or "binfmt_misc" or "autofs" or "mqueue"
                    or "hugetlbfs" or "rpc_pipefs" or "nsfs";
    }

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

public static class SystemHealthRegistration
{
    public static IServiceCollection AddSystemHealthModule(this IServiceCollection services)
        => services.AddSingleton<IOptimizationModule, SystemHealthModule>();
}
