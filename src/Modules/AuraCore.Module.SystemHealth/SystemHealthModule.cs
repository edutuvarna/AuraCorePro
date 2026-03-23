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
    public RiskLevel Risk => RiskLevel.None;

    public SystemHealthReport? LastReport { get; private set; }

    public async Task<ScanResult> ScanAsync(ScanOptions options, CancellationToken ct = default)
    {
        var report = await Task.Run(() => BuildReport(), ct);
        LastReport = report;

        var itemsFound = 4 + report.Drives.Count; // OS, CPU, RAM, processes + drives
        return new ScanResult(Id, true, itemsFound, 0);
    }

    public Task<OptimizationResult> OptimizeAsync(
        OptimizationPlan plan, IProgress<TaskProgress>? progress = null, CancellationToken ct = default)
    {
        // System Health is read-only — nothing to optimize
        return Task.FromResult(new OptimizationResult(Id, Guid.NewGuid().ToString(), true, 0, 0, TimeSpan.Zero));
    }

    public Task<bool> CanRollbackAsync(string operationId, CancellationToken ct = default)
        => Task.FromResult(false);

    public Task RollbackAsync(string operationId, CancellationToken ct = default)
        => Task.CompletedTask;

    private static SystemHealthReport BuildReport()
    {
        // OS
        var osName = RuntimeInformation.OSDescription;
        var osVer = Environment.OSVersion.Version.ToString();
        var arch = RuntimeInformation.OSArchitecture.ToString();
        var machine = Environment.MachineName;
        var uptime = TimeSpan.FromMilliseconds(Environment.TickCount64);

        // CPU
        var cpuName = Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") ?? "Unknown";
        try
        {
            using var searcher = new ManagementObjectSearcher("select Name from Win32_Processor");
            foreach (ManagementObject obj in searcher.Get())
            {
                cpuName = obj["Name"]?.ToString()?.Trim() ?? cpuName;
                break;
            }
        }
        catch { }

        // RAM via P/Invoke
        double totalRamGb = 0, availRamGb = 0;
        int ramPct = 0;
        var memInfo = new MEMORYSTATUSEX();
        if (GlobalMemoryStatusEx(ref memInfo))
        {
            totalRamGb = memInfo.ullTotalPhys / (1024.0 * 1024 * 1024);
            availRamGb = memInfo.ullAvailPhys / (1024.0 * 1024 * 1024);
            ramPct = (int)memInfo.dwMemoryLoad;
        }

        // Drives
        var drives = new List<DriveReport>();
        foreach (var d in DriveInfo.GetDrives().Where(d => d.IsReady))
        {
            var totalGb = d.TotalSize / (1024.0 * 1024 * 1024);
            var freeGb = d.AvailableFreeSpace / (1024.0 * 1024 * 1024);
            var usedPct = (int)((1.0 - freeGb / totalGb) * 100);
            drives.Add(new DriveReport(d.Name, d.VolumeLabel, d.DriveFormat, totalGb, freeGb, usedPct));
        }

        // Processes
        var procCount = Process.GetProcesses().Length;

        // GPU via WMI
        var gpus = new List<GpuReport>();
        try
        {
            using var gpuSearcher = new ManagementObjectSearcher("select Name, DriverVersion, AdapterRAM, VideoModeDescription from Win32_VideoController");
            foreach (ManagementObject obj in gpuSearcher.Get())
            {
                var adapterRam = obj["AdapterRAM"];
                var ramMb = adapterRam is uint ram ? ram / (1024 * 1024) : 0;
                var ramDisplay = ramMb > 1024 ? $"{ramMb / 1024.0:F1} GB" : $"{ramMb} MB";
                gpus.Add(new GpuReport
                {
                    Name = obj["Name"]?.ToString()?.Trim() ?? "Unknown",
                    DriverVersion = obj["DriverVersion"]?.ToString() ?? "",
                    VideoMemory = ramDisplay,
                    Resolution = obj["VideoModeDescription"]?.ToString() ?? ""
                });
            }
        }
        catch { }

        // Battery via WMI
        BatteryReport? battery = null;
        try
        {
            using var batSearcher = new ManagementObjectSearcher("select * from Win32_Battery");
            foreach (ManagementObject obj in batSearcher.Get())
            {
                var charge = obj["EstimatedChargeRemaining"];
                var status = obj["BatteryStatus"];
                var runtime = obj["EstimatedRunTime"];
                battery = new BatteryReport
                {
                    HasBattery = true,
                    ChargePercent = charge is ushort c ? c : 0,
                    Status = status switch
                    {
                        (ushort)1 => "Discharging",
                        (ushort)2 => "Plugged In",
                        (ushort)3 => "Fully Charged",
                        (ushort)4 => "Low",
                        (ushort)5 => "Critical",
                        _ => "Unknown"
                    },
                    EstimatedRuntime = runtime is uint rt && rt < 71582788
                        ? $"{rt / 60}h {rt % 60}m remaining"
                        : "Calculating..."
                };
                break;
            }
        }
        catch { }

        // Startup programs via registry
        var startups = new List<StartupEntry>();
        var startupPaths = new[]
        {
            (@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", "HKLM"),
            (@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", "HKCU"),
            (@"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Run", "HKLM"),
        };
        foreach (var (regPath, hive) in startupPaths)
        {
            try
            {
                var root = hive == "HKLM"
                    ? Microsoft.Win32.Registry.LocalMachine
                    : Microsoft.Win32.Registry.CurrentUser;
                using var key = root.OpenSubKey(regPath);
                if (key is null) continue;
                foreach (var name in key.GetValueNames())
                {
                    var cmd = key.GetValue(name)?.ToString() ?? "";
                    if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(cmd)) continue;
                    startups.Add(new StartupEntry
                    {
                        Name = name,
                        Command = cmd,
                        Location = $"{hive}\\{regPath}",
                        Impact = cmd.Contains("update", StringComparison.OrdinalIgnoreCase) ? "Low"
                            : cmd.Contains("helper", StringComparison.OrdinalIgnoreCase) ? "Low"
                            : "Medium"
                    });
                }
            }
            catch { }
        }

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
            OsName = osName,
            OsVersion = osVer,
            Architecture = arch,
            MachineName = machine,
            Uptime = uptime,
            ProcessorCount = Environment.ProcessorCount,
            ProcessorName = cpuName,
            TotalRamGb = totalRamGb,
            AvailableRamGb = availRamGb,
            RamUsagePercent = ramPct,
            Drives = drives,
            Gpus = gpus,
            Battery = battery,
            StartupPrograms = startups,
            RunningProcesses = procCount,
            HealthScore = score
        };
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
