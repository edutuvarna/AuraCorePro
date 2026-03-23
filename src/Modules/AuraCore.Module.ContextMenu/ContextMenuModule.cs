using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using AuraCore.Application;
using AuraCore.Application.Interfaces.Modules;
using AuraCore.Domain.Enums;
using AuraCore.Module.ContextMenu.Models;

namespace AuraCore.Module.ContextMenu;

public sealed class ContextMenuModule : IOptimizationModule
{
    public string Id => "context-menu";
    public string DisplayName => "Context Menu Customizer";
    public OptimizationCategory Category => OptimizationCategory.ShellCustomization;
    public RiskLevel Risk => RiskLevel.Medium;

    public ContextMenuReport? LastReport { get; private set; }

    private const string ClassicMenuKey = @"Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\InprocServer32";
    private static bool IsWin11 => Environment.OSVersion.Version.Build >= 22000;

    public async Task<ScanResult> ScanAsync(ScanOptions options, CancellationToken ct = default)
    {
        var report = await Task.Run(() =>
        {
            var isClassic = IsWin11 && IsClassicContextMenuEnabled();
            var tweaks = BuildTweakList();
            return new ContextMenuReport
            {
                IsClassicMenuEnabled = isClassic,
                Tweaks = tweaks
            };
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

        for (int i = 0; i < plan.SelectedItemIds.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var tweakId = plan.SelectedItemIds[i];

            if (tweakId == "classic-menu-enable" || tweakId == "classic-menu-disable")
            {
                if (!IsWin11) continue; // Skip on Win10
                progress?.Report(new TaskProgress(Id, (double)(i + 1) / plan.SelectedItemIds.Count * 100,
                    tweakId == "classic-menu-enable" ? "Enabling classic context menu..." : "Restoring Windows 11 context menu..."));
                var ok = tweakId == "classic-menu-enable" ? EnableClassicMenu() : DisableClassicMenu();
                if (ok) applied++;
                continue;
            }

            var tweak = LastReport?.Tweaks.FirstOrDefault(t => t.Id == tweakId);
            if (tweak is null) continue;

            progress?.Report(new TaskProgress(Id, (double)(i + 1) / plan.SelectedItemIds.Count * 100,
                $"Applying: {tweak.Name}..."));
            if (ApplyTweak(tweak)) applied++;
        }

        if (applied > 0) await RestartExplorerShellAsync();
        return new OptimizationResult(Id, Guid.NewGuid().ToString(), true, applied, 0, DateTime.UtcNow - start);
    }

    public Task<bool> CanRollbackAsync(string operationId, CancellationToken ct = default) => Task.FromResult(true);
    public Task RollbackAsync(string operationId, CancellationToken ct = default) => Task.CompletedTask;

    private static List<ContextMenuTweak> BuildTweakList()
    {
        var tweaks = new List<ContextMenuTweak>();

        // Works on both Win10 and Win11
        tweaks.Add(CheckTweak("remove-give-access", "Remove 'Give access to'", "Removes the 'Give access to' sharing option", "Cleanup", "Safe",
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Shell Extensions\Blocked", "{f81e9010-6ea4-11ce-a7ff-00aa003ca9f6}", RegistryAction.SetDword));
        tweaks.Add(CheckTweak("remove-share", "Remove 'Share'", "Removes the Share flyout option", "Cleanup", "Safe",
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Shell Extensions\Blocked", "{e2bf9676-5f8f-435c-97eb-11607a5bedf7}", RegistryAction.SetDword));
        tweaks.Add(CheckTweak("remove-cast-to-device", "Remove 'Cast to Device'", "Removes media streaming option", "Cleanup", "Safe",
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Shell Extensions\Blocked", "{7AD84985-87B4-4a16-BE58-8B72A5B390F7}", RegistryAction.SetDword));
        tweaks.Add(CheckTweak("remove-troubleshoot", "Remove 'Troubleshoot compatibility'", "Removes compatibility troubleshooter", "Cleanup", "Safe",
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Shell Extensions\Blocked", "{1d27f844-3a1f-4410-85ac-14651078412d}", RegistryAction.SetDword));
        tweaks.Add(CheckTweak("remove-previous-versions", "Remove 'Restore previous versions'", "Removes the restore option", "Cleanup", "Safe",
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Shell Extensions\Blocked", "{596AB062-B4D2-4215-9F74-E9109B0A8153}", RegistryAction.SetDword));
        tweaks.Add(CheckTweak("add-cmd-here", "Add 'Open Command Prompt Here'", "Adds CMD to folder context menu", "Additions", "Safe",
            @"SOFTWARE\Classes\Directory\Background\shell\cmd", "", RegistryAction.CreateKey));
        tweaks.Add(CheckTweak("add-take-ownership", "Add 'Take Ownership'", "Adds a Take Ownership option for locked files", "Additions", "Caution",
            @"SOFTWARE\Classes\*\shell\TakeOwnership", "", RegistryAction.CreateKey));

        // Win11 only tweaks
        if (IsWin11)
        {
            tweaks.Add(CheckTweak("remove-edit-with-paint", "Remove 'Edit with Paint' (Win11)", "Removes Edit with Paint from image menus", "Win11", "Safe",
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Shell Extensions\Blocked", "{0C033BF7-2E97-4DE0-A9F2-6B604A37D4D3}", RegistryAction.SetDword));
            tweaks.Add(CheckTweak("remove-edit-with-photos", "Remove 'Edit with Photos' (Win11)", "Removes Edit with Photos from image menus", "Win11", "Safe",
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Shell Extensions\Blocked", "{FFE2A43C-56B9-4bf5-9A79-CC6D4285608A}", RegistryAction.SetDword));
            tweaks.Add(CheckTweak("remove-edit-with-clipchamp", "Remove 'Edit with Clipchamp' (Win11)", "Removes Edit with Clipchamp from video menus", "Win11", "Safe",
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Shell Extensions\Blocked", "{8AB635F8-9A67-4698-AB99-784AD929F3B4}", RegistryAction.SetDword));
        }

        return tweaks;
    }

    private static ContextMenuTweak CheckTweak(string id, string name, string desc, string cat, string risk,
        string regPath, string regValue, RegistryAction action)
    {
        bool isApplied = false;
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(regPath);
            if (action == RegistryAction.SetDword) isApplied = key?.GetValue(regValue) is not null;
            else if (action == RegistryAction.CreateKey) isApplied = key is not null;
        }
        catch { }
        return new ContextMenuTweak { Id = id, Name = name, Description = desc, Category = cat, Risk = risk,
            IsApplied = isApplied, RegistryPath = regPath, RegistryValue = regValue, Action = action };
    }

    private static bool ApplyTweak(ContextMenuTweak tweak)
    {
        try
        {
            if (tweak.IsApplied)
            {
                if (tweak.Action == RegistryAction.SetDword)
                { using var key = Registry.CurrentUser.OpenSubKey(tweak.RegistryPath, true); key?.DeleteValue(tweak.RegistryValue, false); }
                else if (tweak.Action == RegistryAction.CreateKey)
                    Registry.CurrentUser.DeleteSubKeyTree(tweak.RegistryPath, false);
            }
            else
            {
                if (tweak.Action == RegistryAction.SetDword)
                { using var key = Registry.CurrentUser.CreateSubKey(tweak.RegistryPath, true); key?.SetValue(tweak.RegistryValue, 0, RegistryValueKind.DWord); }
                else if (tweak.Action == RegistryAction.CreateKey)
                    Registry.CurrentUser.CreateSubKey(tweak.RegistryPath, true);
            }
            return true;
        }
        catch { return false; }
    }

    private static bool IsClassicContextMenuEnabled()
    { try { using var key = Registry.CurrentUser.OpenSubKey(ClassicMenuKey); return key is not null; } catch { return false; } }

    private static bool EnableClassicMenu()
    { try { using var key = Registry.CurrentUser.CreateSubKey(ClassicMenuKey, true); key?.SetValue("", "", RegistryValueKind.String); return true; } catch { return false; } }

    private static bool DisableClassicMenu()
    { try { Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}", false); return true; } catch { return false; } }

    private static async Task RestartExplorerShellAsync()
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

public static class ContextMenuRegistration
{
    public static IServiceCollection AddContextMenuModule(this IServiceCollection services)
        => services.AddSingleton<IOptimizationModule, ContextMenuModule>();
}
