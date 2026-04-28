using System.Diagnostics;
using System.Runtime.Versioning;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using AuraCore.Application;
using AuraCore.Application.Interfaces.Modules;
using AuraCore.Domain.Enums;
using AuraCore.Module.ExplorerTweaks.Models;

namespace AuraCore.Module.ExplorerTweaks;

// Phase 6.16.F: Windows-only module — uses Microsoft.Win32.Registry which throws
// PlatformNotSupportedException on Linux/macOS. Runtime IsWindows() guards remain
// as defense-in-depth; class attribute makes the contract explicit to CA1416.
[SupportedOSPlatform("windows")]
public sealed class ExplorerTweaksModule : IOptimizationModule
{
    public string Id => "explorer-tweaks";
    public string DisplayName => "Explorer Tweaks";
    public OptimizationCategory Category => OptimizationCategory.ShellCustomization;
    public RiskLevel Risk => RiskLevel.Low;

    public ExplorerReport? LastReport { get; private set; }

    private const string AdvancedPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";
    private const string ExplorerPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer";
    private const string CabinetState = @"Software\Microsoft\Windows\CurrentVersion\Explorer\CabinetState";

    public async Task<ScanResult> ScanAsync(ScanOptions options, CancellationToken ct = default)
    {
        var report = await Task.Run(() =>
        {
            var tweaks = new List<ExplorerTweak>
            {
                ReadTweak("show-extensions", "Show File Extensions", "Always show file extensions (e.g. .txt, .exe) — prevents malware disguised as documents", "Security", "Safe",
                    AdvancedPath, "HideFileExt", 0, 1),
                ReadTweak("show-hidden", "Show Hidden Files", "Display hidden files and folders in Explorer", "Visibility", "Safe",
                    AdvancedPath, "Hidden", 1, 2),
                ReadTweak("show-super-hidden", "Show Protected System Files", "Show Windows system files (normally hidden for safety)", "Visibility", "Caution",
                    AdvancedPath, "ShowSuperHidden", 1, 0),
                ReadTweak("show-full-path", "Show Full Path in Title Bar", "Display the complete folder path in the Explorer title bar", "Navigation", "Safe",
                    CabinetState, "FullPathAddress", 1, 0),
                ReadTweak("open-to-this-pc", "Open Explorer to 'This PC'", "Start Explorer on 'This PC' instead of 'Quick Access/Home'", "Navigation", "Safe",
                    AdvancedPath, "LaunchTo", 1, 2),
                ReadTweak("expand-to-folder", "Auto-Expand to Current Folder", "Automatically expand the navigation pane to the current folder", "Navigation", "Safe",
                    AdvancedPath, "NavPaneExpandToCurrentFolder", 1, 0),
                ReadTweak("show-checkboxes", "Show Selection Checkboxes", "Display checkboxes next to files for easier multi-selection", "Usability", "Safe",
                    AdvancedPath, "AutoCheckSelect", 1, 0),
                ReadTweak("disable-recent", "Disable Recent Files in Quick Access", "Stop showing recently opened files in Quick Access", "Privacy", "Safe",
                    ExplorerPath, "ShowRecent", 0, 1),
                ReadTweak("disable-frequent", "Disable Frequent Folders in Quick Access", "Stop showing frequently used folders in Quick Access", "Privacy", "Safe",
                    ExplorerPath, "ShowFrequent", 0, 1),
                ReadTweak("compact-view", "Use Compact Spacing", "Reduce spacing between items in Explorer (more files visible)", "Appearance", "Safe",
                    AdvancedPath, "UseCompactMode", 1, 0),
                ReadTweak("show-merge-conflicts", "Show Folder Merge Conflicts", "Always show confirmation when merging folders with same name", "Safety", "Safe",
                    AdvancedPath, "HideMergeConflicts", 0, 1),
            };

            return new ExplorerReport { Tweaks = tweaks };
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

    private static ExplorerTweak ReadTweak(string id, string name, string desc, string cat, string risk,
        string regPath, string valueName, int enabledVal, int disabledVal)
    {
        bool isApplied = false;
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(regPath);
            var val = key?.GetValue(valueName);
            if (val is int intVal) isApplied = intVal == enabledVal;
        }
        catch { }

        return new ExplorerTweak
        {
            Id = id, Name = name, Description = desc, Category = cat, Risk = risk,
            IsApplied = isApplied, RegistryPath = regPath, ValueName = valueName,
            EnabledValue = enabledVal, DisabledValue = disabledVal
        };
    }

    private static async Task RestartExplorerAsync()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/c taskkill /f /im explorer.exe & start explorer.exe",
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true
            };
            using var proc = Process.Start(psi);
            if (proc is not null) await proc.WaitForExitAsync();
        }
        catch { }
    }
}

// Phase 6.16.F: DI registration extension marked Windows-only because it instantiates
// ExplorerTweaksModule. Callers must guard with OperatingSystem.IsWindows() before calling.
[SupportedOSPlatform("windows")]
public static class ExplorerTweaksRegistration
{
    public static IServiceCollection AddExplorerTweaksModule(
        this IServiceCollection services)
        => services.AddSingleton<IOptimizationModule, ExplorerTweaksModule>();
}
