using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using AuraCore.Application;
using AuraCore.Application.Interfaces.Modules;
using AuraCore.Domain.Enums;
using AuraCore.Module.TaskbarTweaks.Models;

namespace AuraCore.Module.TaskbarTweaks;

public sealed class TaskbarTweaksModule : IOptimizationModule
{
    public string Id => "taskbar-tweaks";
    public string DisplayName => "Taskbar Tweaks";
    public OptimizationCategory Category => OptimizationCategory.ShellCustomization;
    public RiskLevel Risk => RiskLevel.Low;

    public TaskbarReport? LastReport { get; private set; }

    private const string AdvancedPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";
    private const string SearchPath = @"Software\Microsoft\Windows\CurrentVersion\Search";
    private const string PoliciesExplorer = @"Software\Microsoft\Windows\CurrentVersion\Policies\Explorer";
    private static bool IsWin11 => Environment.OSVersion.Version.Build >= 22000;

    public async Task<ScanResult> ScanAsync(ScanOptions options, CancellationToken ct = default)
    {
        var report = await Task.Run(() =>
        {
            var tweaks = new List<TaskbarTweak>();

            // Both Win10 and Win11
            tweaks.Add(ReadTweak("hide-search", "Hide Search Box", "Remove the search box/icon from the taskbar", "Declutter", "Safe",
                SearchPath, "SearchboxTaskbarMode", 0, 2));
            tweaks.Add(ReadTweak("hide-task-view", "Hide Task View Button", "Remove the Task View button", "Declutter", "Safe",
                AdvancedPath, "ShowTaskViewButton", 0, 1));
            tweaks.Add(ReadTweak("show-seconds", "Show Seconds in Clock", "Display seconds in the taskbar clock", "Additions", "Safe",
                AdvancedPath, "ShowSecondsInSystemClock", 1, 0));
            tweaks.Add(ReadTweak("never-combine", "Never Combine Taskbar Buttons", "Show each window separately", "Behavior", "Safe",
                AdvancedPath, "TaskbarGlomLevel", 2, 0));

            // Win10 only
            if (!IsWin11)
            {
                tweaks.Add(ReadTweak("small-icons-10", "Small Taskbar Icons", "Use smaller icons on the taskbar", "Appearance", "Safe",
                    AdvancedPath, "TaskbarSmallIcons", 1, 0));
                tweaks.Add(ReadTweak("hide-cortana", "Hide Cortana Button", "Remove the Cortana button", "Declutter", "Safe",
                    AdvancedPath, "ShowCortanaButton", 0, 1));
                tweaks.Add(ReadTweak("hide-people", "Hide People Button", "Remove the People button", "Declutter", "Safe",
                    AdvancedPath, "People", 0, 1));
            }

            // Win11 only
            if (IsWin11)
            {
                tweaks.Add(ReadTweak("hide-widgets", "Hide Widgets Button (Win11)", "Remove the Widgets panel button", "Declutter", "Safe",
                    AdvancedPath, "TaskbarDa", 0, 1));
                tweaks.Add(ReadTweak("hide-chat", "Hide Chat Button (Win11)", "Remove the Teams Chat button", "Declutter", "Safe",
                    AdvancedPath, "TaskbarMn", 0, 1));
                tweaks.Add(ReadTweak("hide-copilot", "Hide Copilot Button (Win11)", "Remove the Copilot button", "Declutter", "Safe",
                    AdvancedPath, "ShowCopilotButton", 0, 1));
                tweaks.Add(ReadTweak("left-align", "Left-Align Taskbar (Win11)", "Move taskbar icons to the left like Windows 10", "Appearance", "Safe",
                    AdvancedPath, "TaskbarAl", 0, 1));
                tweaks.Add(ReadTweak("small-icons-11", "Small Taskbar Icons (Win11)", "Use smaller icons to fit more items", "Appearance", "Safe",
                    AdvancedPath, "TaskbarSi", 0, 1));
            }

            return new TaskbarReport { Tweaks = tweaks };
        }, ct);

        LastReport = report;
        return new ScanResult(Id, true, report.Tweaks.Count, 0);
    }

    public async Task<OptimizationResult> OptimizeAsync(
        OptimizationPlan plan, IProgress<TaskProgress>? progress = null, CancellationToken ct = default)
    {
        if (plan.SelectedItemIds.Count == 0)
            return new OptimizationResult(Id, "", false, 0, 0, TimeSpan.Zero);

        var start = DateTime.UtcNow;
        int applied = 0;

        foreach (var tweakId in plan.SelectedItemIds)
        {
            var tweak = LastReport?.Tweaks.FirstOrDefault(t => t.Id == tweakId);
            if (tweak is null) continue;
            progress?.Report(new TaskProgress(Id, 50, $"Applying: {tweak.Name}..."));
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(tweak.RegistryPath, true);
                var targetValue = tweak.IsApplied ? tweak.DisabledValue : tweak.EnabledValue;
                key?.SetValue(tweak.ValueName, targetValue, RegistryValueKind.DWord);
                applied++;
            }
            catch { }
        }

        if (applied > 0)
        {
            progress?.Report(new TaskProgress(Id, 90, "Restarting Explorer..."));
            await RestartExplorerAsync();
        }

        return new OptimizationResult(Id, Guid.NewGuid().ToString(), true, applied, 0, DateTime.UtcNow - start);
    }

    public Task<bool> CanRollbackAsync(string operationId, CancellationToken ct = default) => Task.FromResult(true);
    public Task RollbackAsync(string operationId, CancellationToken ct = default) => Task.CompletedTask;

    private static TaskbarTweak ReadTweak(string id, string name, string desc, string cat, string risk,
        string regPath, string valueName, int enabledVal, int disabledVal)
    {
        bool isApplied = false;
        try { using var key = Registry.CurrentUser.OpenSubKey(regPath); var val = key?.GetValue(valueName); if (val is int intVal) isApplied = intVal == enabledVal; }
        catch { }
        return new TaskbarTweak { Id = id, Name = name, Description = desc, Category = cat, Risk = risk,
            IsApplied = isApplied, RegistryPath = regPath, ValueName = valueName, EnabledValue = enabledVal, DisabledValue = disabledVal };
    }

    private static async Task RestartExplorerAsync()
    {
        try
        {
            var psi = new ProcessStartInfo { FileName = "cmd.exe", Arguments = "/c taskkill /f /im explorer.exe & start explorer.exe",
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true };
            using var proc = Process.Start(psi);
            if (proc is not null) await proc.WaitForExitAsync();
        }
        catch { }
    }
}

public static class TaskbarTweaksRegistration
{
    public static IServiceCollection AddTaskbarTweaksModule(this IServiceCollection services)
        => services.AddSingleton<IOptimizationModule, TaskbarTweaksModule>();
}
