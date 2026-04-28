using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;
using AuraCore.Application;
using AuraCore.Application.Interfaces.Modules;

namespace AuraCore.Desktop.Services.Scheduler;

public sealed class BackgroundScheduler : IDisposable
{
    private readonly IServiceProvider _services;
    private readonly DispatcherQueue _dispatcher;
    private System.Threading.Timer? _timer;
    private List<ScheduleEntry> _schedules;
    private readonly List<string> _log = new();
    private bool _isRunning;

    public event Action<string>? OnTaskCompleted;
    public IReadOnlyList<string> Log => _log;
    public bool IsRunning => _isRunning;

    public BackgroundScheduler(IServiceProvider services, DispatcherQueue dispatcher)
    {
        _services = services;
        _dispatcher = dispatcher;
        _schedules = ScheduleStore.Load();
    }

    public void Start()
    {
        if (_isRunning) return;
        _isRunning = true;
        // Check every 60 seconds if any scheduled task is due
        _timer = new System.Threading.Timer(CheckSchedules, null, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(60));
        AddLog("Scheduler started");
    }

    public void Stop()
    {
        _timer?.Dispose();
        _timer = null;
        _isRunning = false;
        AddLog("Scheduler stopped");
    }

    public void Reload()
    {
        _schedules = ScheduleStore.Load();
        AddLog("Schedules reloaded");
    }

    private async void CheckSchedules(object? state)
    {
        foreach (var schedule in _schedules.Where(s => s.Enabled))
        {
            var interval = schedule.Interval.ToTimeSpan();
            var lastRun = schedule.LastRun ?? DateTimeOffset.MinValue;

            if (DateTimeOffset.UtcNow - lastRun < interval) continue;

            // Check if idle (simplified: CPU < 30%)
            if (schedule.OnlyWhenIdle)
            {
                // Simple idle check — if there's been recent user input, skip
                var idleTime = GetIdleTime();
                if (idleTime < TimeSpan.FromMinutes(2)) continue;
            }

            await RunScheduledTaskAsync(schedule);
        }
    }

    private async Task RunScheduledTaskAsync(ScheduleEntry schedule)
    {
        try
        {
            var modules = _services.GetServices<IOptimizationModule>();
            var module = modules.FirstOrDefault(m => m.Id == schedule.ModuleId);
            if (module is null) return;

            AddLog($"Running: {schedule.ModuleName}...");

            if (schedule.ModuleId == "ram-optimizer")
            {
                var result = await module.OptimizeAsync(new OptimizationPlan(schedule.ModuleId, new List<string>()), null);
                schedule.LastResult = $"Freed {FormatBytes(result.BytesFreed)} from {result.ItemsProcessed} processes";
            }
            else
            {
                var result = await module.ScanAsync(new ScanOptions());
                schedule.LastResult = $"Found {result.ItemsFound} items ({FormatBytes(result.EstimatedBytesFreed)})";

                // Auto-clean for junk cleaner (only Safe categories)
                if (schedule.ModuleId == "junk-cleaner" && result.ItemsFound > 0)
                {
                    var cleanResult = await module.OptimizeAsync(
                        new OptimizationPlan(schedule.ModuleId, new List<string>()), null);
                    schedule.LastResult = $"Cleaned {FormatBytes(cleanResult.BytesFreed)}";
                }

                // Auto-fix safe registry issues
                if (schedule.ModuleId == "registry-optimizer" && result.ItemsFound > 0)
                {
                    AddLog($"  Auto-fixing safe registry issues...");
                    var fixResult = await module.OptimizeAsync(
                        new OptimizationPlan(schedule.ModuleId, new List<string> { "safe-only" }), null);
                    schedule.LastResult = $"Found {result.ItemsFound} issues, fixed {fixResult.ItemsProcessed} safe entries";
                }
            }

            schedule.LastRun = DateTimeOffset.UtcNow;
            ScheduleStore.Save(_schedules);

            AddLog($"Completed: {schedule.ModuleName} — {schedule.LastResult}");

            _dispatcher.TryEnqueue(() =>
            {
                OnTaskCompleted?.Invoke($"{schedule.ModuleName}: {schedule.LastResult}");
                NotificationService.Instance.Post(
                    $"Scheduled: {schedule.ModuleName}",
                    schedule.LastResult ?? "Task completed",
                    NotificationType.Success,
                    schedule.ModuleId);
            });
        }
        catch (Exception ex)
        {
            schedule.LastResult = $"Error: {ex.Message}";
            AddLog($"Failed: {schedule.ModuleName} — {ex.Message}");
            _dispatcher.TryEnqueue(() =>
            {
                NotificationService.Instance.Post(
                    $"Scheduled: {schedule.ModuleName}",
                    $"Failed: {ex.Message}",
                    NotificationType.Error,
                    schedule.ModuleId);
            });
        }
    }

    private void AddLog(string message)
    {
        var entry = $"[{DateTime.Now:HH:mm:ss}] {message}";
        _log.Insert(0, entry);
        if (_log.Count > 50) _log.RemoveAt(_log.Count - 1);
    }

    private static TimeSpan GetIdleTime()
    {
        // Phase 6.16 Linux platform guard — GetLastInputInfo is a user32.dll P/Invoke that throws
        // EntryPointNotFoundException on non-Windows. AuraCore.Desktop currently targets
        // net8.0-windows10.0.19041.0 so this never triggers in the WinUI app, but the guard makes
        // the helper safe to lift cross-platform if BackgroundScheduler ever moves to Avalonia.
        // CheckSchedules runs on a 60-second timer in production — a per-tick crash would be
        // catastrophic, hence the early-return rather than try/catch.
        // No automated test: GetIdleTime is private and Tests.Unit cannot reference the WinUI csproj.
        // Manual verification path: build + run the Avalonia equivalent on Linux once BackgroundScheduler
        // is lifted (Phase 6.16 Wave C consideration).
        if (!OperatingSystem.IsWindows()) return TimeSpan.Zero;

        var info = new LASTINPUTINFO { cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<LASTINPUTINFO>() };
        if (GetLastInputInfo(ref info))
        {
            var idleTicks = (uint)Environment.TickCount - info.dwTime;
            return TimeSpan.FromMilliseconds(idleTicks);
        }
        return TimeSpan.Zero;
    }

    private static string FormatBytes(long b) => b switch
    {
        < 1024 * 1024 => $"{b / 1024.0:F0} KB",
        < 1024L * 1024 * 1024 => $"{b / (1024.0 * 1024):F1} MB",
        _ => $"{b / (1024.0 * 1024 * 1024):F2} GB"
    };

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct LASTINPUTINFO { public uint cbSize; public uint dwTime; }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool GetLastInputInfo(ref LASTINPUTINFO info);

    public void Dispose() => Stop();
}
