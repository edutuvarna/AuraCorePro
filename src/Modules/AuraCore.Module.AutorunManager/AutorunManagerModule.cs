using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using AuraCore.Application;
using AuraCore.Application.Interfaces.Modules;
using AuraCore.Domain.Enums;
using AuraCore.Module.AutorunManager.Models;

namespace AuraCore.Module.AutorunManager;

public sealed class AutorunManagerModule : IOptimizationModule
{
    public string Id          => "autorun-manager";
    public string DisplayName => "Autorun Manager";
    public OptimizationCategory Category => OptimizationCategory.AutorunManagement;
    public RiskLevel Risk     => RiskLevel.High;

    public AutorunReport? LastReport { get; private set; }

    public async Task<ScanResult> ScanAsync(ScanOptions options, CancellationToken ct = default)
    {
        var entries = new List<AutorunEntry>();
        await Task.Run(() =>
        {
            // --- Registry Run keys ---
            ReadRunKey(entries, RegistryHive.CurrentUser,
                @"Software\Microsoft\Windows\CurrentVersion\Run", "HKCU\\Run");
            ReadRunKey(entries, RegistryHive.CurrentUser,
                @"Software\Microsoft\Windows\CurrentVersion\RunOnce", "HKCU\\RunOnce");
            ReadRunKey(entries, RegistryHive.LocalMachine,
                @"Software\Microsoft\Windows\CurrentVersion\Run", "HKLM\\Run");
            ReadRunKey(entries, RegistryHive.LocalMachine,
                @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Run", "HKLM\\Run (x86)");

            // --- Disabled registry entries (MSConfig-style) ---
            ReadDisabledEntries(entries);

            // --- Startup folders ---
            ReadStartupFolder(entries,
                Environment.GetFolderPath(Environment.SpecialFolder.Startup), "User Startup Folder");
            ReadStartupFolder(entries,
                Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup), "Common Startup Folder");
        }, ct);

        // Assign risk levels
        foreach (var e in entries)
            e.RiskLevel = ClassifyRisk(e);

        LastReport = new AutorunReport { Entries = entries };
        return new ScanResult(Id, true, entries.Count, 0);
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
                    if (id.StartsWith("disable:", StringComparison.OrdinalIgnoreCase))
                        DisableEntry(id[8..]);
                    else if (id.StartsWith("enable:", StringComparison.OrdinalIgnoreCase))
                        EnableEntry(id[7..]);
                    else if (id.StartsWith("delete:", StringComparison.OrdinalIgnoreCase))
                        DeleteEntry(id[7..]);
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

    // ── Scan helpers ─────────────────────────────────────────

    private static void ReadRunKey(List<AutorunEntry> list, RegistryHive hive, string subKey, string location)
    {
        try
        {
            using var root = hive == RegistryHive.LocalMachine
                ? RegistryKey.OpenBaseKey(Microsoft.Win32.RegistryHive.LocalMachine, RegistryView.Registry64)
                : RegistryKey.OpenBaseKey(Microsoft.Win32.RegistryHive.CurrentUser, RegistryView.Registry64);
            using var key = root.OpenSubKey(subKey, writable: false);
            if (key is null) return;

            foreach (var name in key.GetValueNames())
            {
                var cmd = key.GetValue(name)?.ToString() ?? "";
                var enabled = !cmd.StartsWith(">");
                list.Add(new AutorunEntry
                {
                    Name              = name,
                    Command           = enabled ? cmd : cmd[1..],
                    Location          = location,
                    Type              = AutorunType.Registry,
                    IsEnabled         = enabled,
                    RegistryHive      = hive.ToString(),
                    RegistrySubKey    = subKey,
                    RegistryValueName = name,
                    FilePath          = ExtractExePath(cmd)
                });
            }
        }
        catch { }
    }

    private static void ReadDisabledEntries(List<AutorunEntry> list)
    {
        // MSConfig stores disabled startup entries here
        var msConfigPath = @"Software\Microsoft\Shared Tools\MSConfig\startupreg";
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(msConfigPath);
            if (key is null) return;
            foreach (var sub in key.GetSubKeyNames())
            {
                using var entry = key.OpenSubKey(sub);
                var cmd = entry?.GetValue("command")?.ToString() ?? "";
                if (string.IsNullOrEmpty(cmd)) continue;
                // Don't duplicate if already found as enabled
                if (list.Any(e => e.Name.Equals(sub, StringComparison.OrdinalIgnoreCase))) continue;
                list.Add(new AutorunEntry
                {
                    Name     = sub, Command = cmd, Location = "HKLM\\Run (disabled by MSConfig)",
                    Type     = AutorunType.Registry, IsEnabled = false,
                    FilePath = ExtractExePath(cmd)
                });
            }
        }
        catch { }
    }

    private static void ReadStartupFolder(List<AutorunEntry> list, string folder, string location)
    {
        if (!Directory.Exists(folder)) return;
        foreach (var file in Directory.EnumerateFiles(folder, "*.lnk"))
        {
            list.Add(new AutorunEntry
            {
                Name     = Path.GetFileNameWithoutExtension(file),
                Command  = file,
                Location = location,
                Type     = AutorunType.StartupFolder,
                IsEnabled = true,
                FilePath  = file
            });
        }
    }

    // ── Action helpers ────────────────────────────────────────

    private void DisableEntry(string name)
    {
        var entry = LastReport?.Entries.FirstOrDefault(e => e.Name == name && e.IsEnabled);
        if (entry is null) return;

        if (entry.Type == AutorunType.Registry)
        {
            using var hive = entry.RegistryHive == "LocalMachine"
                ? RegistryKey.OpenBaseKey(Microsoft.Win32.RegistryHive.LocalMachine, RegistryView.Registry64)
                : RegistryKey.OpenBaseKey(Microsoft.Win32.RegistryHive.CurrentUser, RegistryView.Registry64);
            using var key = hive.OpenSubKey(entry.RegistrySubKey, writable: true);
            if (key is null) return;
            var val = key.GetValue(entry.RegistryValueName)?.ToString() ?? "";
            key.SetValue(entry.RegistryValueName, ">" + val);
            entry.IsEnabled = false;
        }
        else if (entry.Type == AutorunType.StartupFolder && File.Exists(entry.FilePath))
        {
            var disabled = entry.FilePath + ".disabled";
            File.Move(entry.FilePath, disabled, overwrite: true);
            entry.IsEnabled = false;
        }
    }

    private void EnableEntry(string name)
    {
        var entry = LastReport?.Entries.FirstOrDefault(e => e.Name == name && !e.IsEnabled);
        if (entry is null) return;

        if (entry.Type == AutorunType.Registry)
        {
            using var hive = entry.RegistryHive == "LocalMachine"
                ? RegistryKey.OpenBaseKey(Microsoft.Win32.RegistryHive.LocalMachine, RegistryView.Registry64)
                : RegistryKey.OpenBaseKey(Microsoft.Win32.RegistryHive.CurrentUser, RegistryView.Registry64);
            using var key = hive.OpenSubKey(entry.RegistrySubKey, writable: true);
            if (key is null) return;
            var val = key.GetValue(entry.RegistryValueName)?.ToString() ?? "";
            if (val.StartsWith(">")) key.SetValue(entry.RegistryValueName, val[1..]);
            entry.IsEnabled = true;
        }
    }

    private void DeleteEntry(string name)
    {
        var entry = LastReport?.Entries.FirstOrDefault(e => e.Name == name);
        if (entry is null) return;

        if (entry.Type == AutorunType.Registry)
        {
            using var hive = entry.RegistryHive == "LocalMachine"
                ? RegistryKey.OpenBaseKey(Microsoft.Win32.RegistryHive.LocalMachine, RegistryView.Registry64)
                : RegistryKey.OpenBaseKey(Microsoft.Win32.RegistryHive.CurrentUser, RegistryView.Registry64);
            using var key = hive.OpenSubKey(entry.RegistrySubKey, writable: true);
            key?.DeleteValue(entry.RegistryValueName, throwOnMissingValue: false);
        }
        else if (entry.Type == AutorunType.StartupFolder && File.Exists(entry.FilePath))
        {
            File.Delete(entry.FilePath);
        }

        LastReport?.Entries.Remove(entry);
    }

    // ── Utilities ─────────────────────────────────────────────

    private static string ExtractExePath(string cmd)
    {
        if (string.IsNullOrWhiteSpace(cmd)) return "";
        cmd = cmd.TrimStart('>').Trim();
        if (cmd.StartsWith('"'))
        {
            var end = cmd.IndexOf('"', 1);
            return end > 0 ? cmd[1..end] : cmd;
        }
        var space = cmd.IndexOf(' ');
        return space > 0 ? cmd[..space] : cmd;
    }

    private static string ClassifyRisk(AutorunEntry e)
    {
        var name = e.Name.ToLowerInvariant();
        var cmd  = e.Command.ToLowerInvariant();

        // Known safe publishers / names
        var safePatterns = new[] { "microsoft", "windows", "nvidia", "amd", "intel", "realtek", "logitech", "steam", "discord", "spotify" };
        if (safePatterns.Any(p => name.Contains(p) || cmd.Contains(p)))
            return "Safe";

        // Suspicious: temp folders, no publisher
        if (cmd.Contains(@"\temp\") || cmd.Contains(@"\tmp\") || cmd.Contains("%temp%"))
            return "High";
        if (cmd.Contains(@"\appdata\roaming\") && !cmd.Contains("microsoft"))
            return "Medium";

        return "Low";
    }
}

public static class AutorunManagerRegistration
{
    public static IServiceCollection AddAutorunManagerModule(this IServiceCollection services)
        => services.AddSingleton<IOptimizationModule, AutorunManagerModule>();
}
