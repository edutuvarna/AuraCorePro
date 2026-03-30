using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.DependencyInjection;
using AuraCore.Application;
using AuraCore.Application.Interfaces.Modules;
using AuraCore.Domain.Enums;
using AuraCore.Module.ProcessMonitor.Models;

namespace AuraCore.Module.ProcessMonitor;

public sealed class ProcessMonitorModule : IOptimizationModule
{
    public string Id          => "process-monitor";
    public string DisplayName => "Process Monitor";
    public OptimizationCategory Category => OptimizationCategory.ProcessManagement;
    public RiskLevel Risk     => RiskLevel.High;
    public SupportedPlatform Platform => SupportedPlatform.All;

    public ProcessReport? LastReport { get; private set; }

    // Cached data for fast refresh
    private Dictionary<int, (TimeSpan cpu, DateTime sampleTime)> _prevSample = new();
    private readonly Dictionary<int, string> _descCache = new();

    // P/Invoke for Suspend/Resume
    [DllImport("ntdll.dll", SetLastError = true)]
    private static extern int NtSuspendProcess(IntPtr processHandle);

    [DllImport("ntdll.dll", SetLastError = true)]
    private static extern int NtResumeProcess(IntPtr processHandle);

    public async Task<ScanResult> ScanAsync(ScanOptions options, CancellationToken ct = default)
    {
        var processes = new List<ProcessInfo>();
        bool hasPrev = _prevSample.Count > 0;

        await Task.Run(() =>
        {
            Dictionary<int, (TimeSpan cpu, DateTime sampleTime)> sample1;
            Dictionary<int, (TimeSpan cpu, DateTime sampleTime)> sample2;

            if (hasPrev)
            {
                // Fast path: reuse previous sample, no sleep needed
                sample1 = _prevSample;
                sample2 = TakeCpuSample();
            }
            else
            {
                // First scan: need two samples with delay
                sample1 = TakeCpuSample();
                Thread.Sleep(400);
                ct.ThrowIfCancellationRequested();
                sample2 = TakeCpuSample();
            }
            _prevSample = sample2;

            var allProcs = Process.GetProcesses();
            foreach (var p in allProcs)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var cpu = 0.0;
                    if (sample1.TryGetValue(p.Id, out var t1) && sample2.TryGetValue(p.Id, out var t2))
                    {
                        var elapsed = (t2.sampleTime - t1.sampleTime).TotalMilliseconds;
                        if (elapsed > 0)
                            cpu = (t2.cpu - t1.cpu).TotalMilliseconds / elapsed / Environment.ProcessorCount * 100;
                    }
                    cpu = Math.Max(0, Math.Min(100, cpu));

                    var prio = p.PriorityClass;
                    var (prioVal, prioLabel) = prio switch
                    {
                        ProcessPriorityClass.Idle         => (4,  "Idle"),
                        ProcessPriorityClass.BelowNormal  => (6,  "Below Normal"),
                        ProcessPriorityClass.Normal       => (8,  "Normal"),
                        ProcessPriorityClass.AboveNormal  => (10, "Above Normal"),
                        ProcessPriorityClass.High         => (13, "High"),
                        ProcessPriorityClass.RealTime     => (24, "Real Time"),
                        _                                 => (8,  "Normal")
                    };

                    // Cache descriptions - FileVersionInfo is expensive disk I/O
                    if (!_descCache.TryGetValue(p.Id, out var description))
                    {
                        description = "";
                        try
                        {
                            var fp = p.MainModule?.FileName ?? "";
                            if (!string.IsNullOrEmpty(fp))
                                description = FileVersionInfo.GetVersionInfo(fp).FileDescription ?? "";
                        }
                        catch { }
                        _descCache[p.Id] = description;
                    }

                    processes.Add(new ProcessInfo
                    {
                        Pid          = p.Id,
                        Name         = p.ProcessName,
                        Description  = description ?? "",
                        CpuPercent   = Math.Round(cpu, 1),
                        MemoryMb     = p.WorkingSet64 / 1024 / 1024,
                        Status       = "Running",
                        Priority     = prioVal,
                        PriorityLabel = prioLabel,
                        ThreadCount  = p.Threads.Count,
                        HandleCount  = p.HandleCount,
                        StartTime    = TryGetStartTime(p),
                    });
                }
                catch { }
                finally { p.Dispose(); }
            }
        }, ct);

        processes = processes.OrderByDescending(p => p.CpuPercent).ToList();
        LastReport = new ProcessReport { Processes = processes };
        return new ScanResult(Id, true, processes.Count, 0);
    }

    public async Task<OptimizationResult> OptimizeAsync(
        OptimizationPlan plan, IProgress<TaskProgress>? progress = null, CancellationToken ct = default)
    {
        int done = 0;
        var start = DateTime.UtcNow;
        var ids = plan.SelectedItemIds ?? new List<string>();

        await Task.Run(() =>
        {
            foreach (var id in ids)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var parts = id.Split(':');
                    var action = parts[0].ToLower();
                    if (!int.TryParse(parts.ElementAtOrDefault(1), out var pid)) continue;

                    using var proc = Process.GetProcessById(pid);
                    switch (action)
                    {
                        case "kill":
                            proc.Kill(entireProcessTree: false);
                            break;
                        case "killtree":
                            proc.Kill(entireProcessTree: true);
                            break;
                        case "priority":
                            var level = parts.ElementAtOrDefault(2) ?? "normal";
                            proc.PriorityClass = level.ToLower() switch
                            {
                                "idle"        => ProcessPriorityClass.Idle,
                                "belownormal" => ProcessPriorityClass.BelowNormal,
                                "abovenormal" => ProcessPriorityClass.AboveNormal,
                                "high"        => ProcessPriorityClass.High,
                                "realtime"    => ProcessPriorityClass.RealTime,
                                _             => ProcessPriorityClass.Normal
                            };
                            break;
                        case "suspend":
                            if (OperatingSystem.IsWindows())
                                NtSuspendProcess(proc.Handle);
                            else if (OperatingSystem.IsLinux())
                                Process.Start("kill", $"-STOP {pid}")?.WaitForExit(3000);
                            break;
                        case "resume":
                            if (OperatingSystem.IsWindows())
                                NtResumeProcess(proc.Handle);
                            else if (OperatingSystem.IsLinux())
                                Process.Start("kill", $"-CONT {pid}")?.WaitForExit(3000);
                            break;
                    }
                    done++;
                }
                catch { }
                progress?.Report(new TaskProgress(Id, done * 100.0 / Math.Max(ids.Count, 1), id));
            }
        }, ct);

        return new OptimizationResult(Id, Guid.NewGuid().ToString(), true, done, 0, DateTime.UtcNow - start);
    }

    public Task<bool> CanRollbackAsync(string opId, CancellationToken ct = default) => Task.FromResult(false);
    public Task RollbackAsync(string opId, CancellationToken ct = default) => Task.CompletedTask;

    private static Dictionary<int, (TimeSpan cpu, DateTime sampleTime)> TakeCpuSample()
    {
        var result = new Dictionary<int, (TimeSpan, DateTime)>();
        var now = DateTime.UtcNow;
        foreach (var p in Process.GetProcesses())
        {
            try { result[p.Id] = (p.TotalProcessorTime, now); }
            catch { }
            finally { p.Dispose(); }
        }
        return result;
    }

    private static DateTime TryGetStartTime(Process p)
    {
        try { return p.StartTime; } catch { return DateTime.MinValue; }
    }
}

public static class ProcessMonitorRegistration
{
    public static IServiceCollection AddProcessMonitorModule(this IServiceCollection services)
        => services.AddSingleton<IOptimizationModule, ProcessMonitorModule>();
}
