using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using AuraCore.Application;
using AuraCore.Application.Interfaces.Modules;
using AuraCore.Domain.Enums;
using AuraCore.Module.RegistryOptimizer.Models;

namespace AuraCore.Module.RegistryOptimizer;

public sealed class RegistryOptimizerModule : IOptimizationModule
{
    public string Id => "registry-optimizer";
    public string DisplayName => "Registry Optimizer";
    public OptimizationCategory Category => OptimizationCategory.RegistryOptimization;
    public RiskLevel Risk => RiskLevel.High;

    public RegistryScanReport? LastReport { get; private set; }

    public async Task<ScanResult> ScanAsync(ScanOptions options, CancellationToken ct = default)
    {
        // Phase 6.16 Linux platform guard — Microsoft.Win32.Registry throws PlatformNotSupportedException on non-Windows.
        if (!OperatingSystem.IsWindows())
            return new ScanResult(Id, true, 0, 0);

        var issues = new List<RegistryIssue>();

        await Task.Run(() =>
        {
            ScanOrphanedUninstallEntries(issues, ct);
            ScanBrokenFileAssociations(issues, ct);
            ScanInvalidSharedDlls(issues, ct);
            ScanObsoleteMuiCache(issues, ct);
            ScanStaleAppPaths(issues, ct);
            ScanEmptyKeys(issues, ct);
        }, ct);

        LastReport = new RegistryScanReport { Issues = issues };
        return new ScanResult(Id, true, issues.Count, 0);
    }

    public async Task<OptimizationResult> OptimizeAsync(
        OptimizationPlan plan, IProgress<TaskProgress>? progress = null, CancellationToken ct = default)
    {
        // Phase 6.16 Linux platform guard — Registry writes throw PlatformNotSupportedException on non-Windows.
        if (!OperatingSystem.IsWindows())
            return new OptimizationResult(Id, "", true, 0, 0, TimeSpan.Zero);

        if (plan.SelectedItemIds.Count == 0 || LastReport is null)
            return new OptimizationResult(Id, "", false, 0, 0, TimeSpan.Zero);

        var start = DateTime.UtcNow;

        // Step 1: Create backup
        progress?.Report(new TaskProgress(Id, 5, "Creating registry backup..."));
        var backupPath = await CreateBackupAsync();
        LastReport.BackupPath = backupPath;

        // Step 2: Fix selected issues
        int fixed_count = 0;
        int total = plan.SelectedItemIds.Count;

        for (int i = 0; i < total; i++)
        {
            ct.ThrowIfCancellationRequested();
            var issueId = plan.SelectedItemIds[i];
            var issue = LastReport.Issues.FirstOrDefault(x => x.Id == issueId);
            if (issue is null) continue;

            progress?.Report(new TaskProgress(Id,
                5 + (double)(i + 1) / total * 90,
                $"Fixing: {issue.Category} — {issue.Description}"));

            if (FixIssue(issue))
                fixed_count++;
        }

        progress?.Report(new TaskProgress(Id, 100, "Registry optimization complete"));

        return new OptimizationResult(Id, Guid.NewGuid().ToString(), true, fixed_count, 0, DateTime.UtcNow - start);
    }

    public Task<bool> CanRollbackAsync(string operationId, CancellationToken ct = default)
        => Task.FromResult(LastReport?.BackupPath is not null);

    public async Task RollbackAsync(string operationId, CancellationToken ct = default)
    {
        if (LastReport?.BackupPath is not null && File.Exists(LastReport.BackupPath))
            await RestoreBackupAsync(LastReport.BackupPath);
    }

    // ── SCANNERS ─────────────────────────────────────────────

    private static void ScanOrphanedUninstallEntries(List<RegistryIssue> issues, CancellationToken ct)
    {
        var paths = new[]
        {
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
            @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
        };

        foreach (var basePath in paths)
        {
            try
            {
                using var root = Registry.LocalMachine.OpenSubKey(basePath);
                if (root is null) continue;

                foreach (var subName in root.GetSubKeyNames())
                {
                    ct.ThrowIfCancellationRequested();
                    try
                    {
                        using var sub = root.OpenSubKey(subName);
                        if (sub is null) continue;

                        var installLoc = sub.GetValue("InstallLocation") as string;
                        var displayName = sub.GetValue("DisplayName") as string;

                        // If install location is specified but doesn't exist
                        if (!string.IsNullOrWhiteSpace(installLoc) && !Directory.Exists(installLoc))
                        {
                            issues.Add(new RegistryIssue
                            {
                                Id = $"uninstall-{subName}",
                                Category = "Orphaned Uninstall Entry",
                                Description = displayName ?? subName,
                                KeyPath = $"HKLM\\{basePath}\\{subName}",
                                Risk = "Caution",
                                Detail = $"Install location no longer exists: {installLoc}",
                                Type = IssueType.OrphanedUninstallEntry
                            });
                        }
                    }
                    catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[registry-optimizer] Error: {ex.Message}"); }
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[registry-optimizer] Error: {ex.Message}"); }
        }
    }

    private static void ScanBrokenFileAssociations(List<RegistryIssue> issues, CancellationToken ct)
    {
        try
        {
            using var root = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts");
            if (root is null) return;

            foreach (var ext in root.GetSubKeyNames())
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    using var choice = root.OpenSubKey($@"{ext}\UserChoice");
                    if (choice is null) continue;

                    var progId = choice.GetValue("ProgID") as string;
                    if (string.IsNullOrEmpty(progId)) continue;

                    // Check if the ProgID actually exists in HKCR
                    using var progKey = Registry.ClassesRoot.OpenSubKey(progId);
                    if (progKey is null)
                    {
                        issues.Add(new RegistryIssue
                        {
                            Id = $"fileassoc-{ext}",
                            Category = "Broken File Association",
                            Description = $"{ext} → {progId}",
                            KeyPath = $"HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\FileExts\\{ext}",
                            Risk = "Safe",
                            Detail = $"File type {ext} points to missing handler: {progId}",
                            Type = IssueType.BrokenFileAssociation
                        });
                    }
                }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[registry-optimizer] Error: {ex.Message}"); }
            }
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[registry-optimizer] Error: {ex.Message}"); }
    }

    private static void ScanInvalidSharedDlls(List<RegistryIssue> issues, CancellationToken ct)
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\SharedDLLs");
            if (key is null) return;

            int count = 0;
            foreach (var valueName in key.GetValueNames())
            {
                ct.ThrowIfCancellationRequested();
                if (count > 50) break; // Limit scan scope

                if (!string.IsNullOrEmpty(valueName) && !File.Exists(valueName))
                {
                    issues.Add(new RegistryIssue
                    {
                        Id = $"shareddll-{count++}",
                        Category = "Invalid Shared DLL",
                        Description = Path.GetFileName(valueName),
                        KeyPath = @"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\SharedDLLs",
                        ValueName = valueName,
                        Risk = "Caution",
                        Detail = $"Referenced DLL no longer exists: {valueName}",
                        Type = IssueType.InvalidSharedDll
                    });
                }
                count++;
            }
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[registry-optimizer] Error: {ex.Message}"); }
    }

    private static void ScanObsoleteMuiCache(List<RegistryIssue> issues, CancellationToken ct)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Classes\Local Settings\Software\Microsoft\Windows\Shell\MuiCache");
            if (key is null) return;

            int count = 0;
            foreach (var valueName in key.GetValueNames())
            {
                ct.ThrowIfCancellationRequested();
                if (count > 30) break;

                // MuiCache values reference .exe paths
                if (valueName.EndsWith(".FriendlyAppName", StringComparison.OrdinalIgnoreCase))
                {
                    var exePath = valueName.Replace(".FriendlyAppName", "");
                    if (!string.IsNullOrEmpty(exePath) && !File.Exists(exePath))
                    {
                        issues.Add(new RegistryIssue
                        {
                            Id = $"muicache-{count}",
                            Category = "Obsolete MUI Cache",
                            Description = Path.GetFileName(exePath),
                            KeyPath = @"HKCU\...\MuiCache",
                            ValueName = valueName,
                            Risk = "Safe",
                            Detail = $"Cached name for uninstalled app: {exePath}",
                            Type = IssueType.ObsoleteMuiCache
                        });
                    }
                }
                count++;
            }
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[registry-optimizer] Error: {ex.Message}"); }
    }

    private static void ScanStaleAppPaths(List<RegistryIssue> issues, CancellationToken ct)
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths");
            if (key is null) return;

            foreach (var subName in key.GetSubKeyNames())
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    using var sub = key.OpenSubKey(subName);
                    var defVal = sub?.GetValue("") as string;
                    if (!string.IsNullOrEmpty(defVal))
                    {
                        var cleanPath = defVal.Trim('"');
                        if (!File.Exists(cleanPath) && !cleanPath.Contains('%'))
                        {
                            issues.Add(new RegistryIssue
                            {
                                Id = $"apppath-{subName}",
                                Category = "Stale App Path",
                                Description = subName,
                                KeyPath = $@"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\{subName}",
                                Risk = "Safe",
                                Detail = $"App path points to missing executable: {cleanPath}",
                                Type = IssueType.StaleAppPath
                            });
                        }
                    }
                }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[registry-optimizer] Error: {ex.Message}"); }
            }
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[registry-optimizer] Error: {ex.Message}"); }
    }

    private static void ScanEmptyKeys(List<RegistryIssue> issues, CancellationToken ct)
    {
        // Only scan user-space keys — safe to clean
        var targets = new[]
        {
            @"Software\Classes",
            @"Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts"
        };

        foreach (var path in targets)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(path);
                if (key is null) continue;

                int count = 0;
                foreach (var subName in key.GetSubKeyNames())
                {
                    ct.ThrowIfCancellationRequested();
                    if (count > 20) break;

                    try
                    {
                        using var sub = key.OpenSubKey(subName);
                        if (sub is not null && sub.SubKeyCount == 0 && sub.ValueCount == 0)
                        {
                            issues.Add(new RegistryIssue
                            {
                                Id = $"empty-{path.GetHashCode()}-{subName}",
                                Category = "Empty Registry Key",
                                Description = subName,
                                KeyPath = $"HKCU\\{path}\\{subName}",
                                Risk = "Safe",
                                Detail = "Empty key with no values or subkeys — leftover from uninstalled software",
                                Type = IssueType.EmptyRegistryKey
                            });
                            count++;
                        }
                    }
                    catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[registry-optimizer] Error: {ex.Message}"); }
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[registry-optimizer] Error: {ex.Message}"); }
        }
    }

    // ── FIX LOGIC ────────────────────────────────────────────

    private static bool FixIssue(RegistryIssue issue)
    {
        try
        {
            switch (issue.Type)
            {
                case IssueType.BrokenFileAssociation:
                case IssueType.EmptyRegistryKey:
                    // Delete the user-space key
                    Registry.CurrentUser.DeleteSubKeyTree(
                        issue.KeyPath.Replace("HKCU\\", ""), false);
                    return true;

                case IssueType.ObsoleteMuiCache:
                    // Delete the specific value
                    using (var key = Registry.CurrentUser.OpenSubKey(
                        @"Software\Classes\Local Settings\Software\Microsoft\Windows\Shell\MuiCache", true))
                    {
                        key?.DeleteValue(issue.ValueName ?? "", false);
                    }
                    return true;

                case IssueType.OrphanedUninstallEntry:
                case IssueType.StaleAppPath:
                case IssueType.InvalidSharedDll:
                    try
                    {
                        // Attempt HKLM fix - requires elevation
                        if (issue.Type == IssueType.InvalidSharedDll)
                        {
                            // SharedDLLs: delete the value (DLL path) from the key
                            using var sharedKey = Registry.LocalMachine.OpenSubKey(
                                @"SOFTWARE\Microsoft\Windows\CurrentVersion\SharedDLLs", writable: true);
                            if (sharedKey != null)
                            {
                                sharedKey.DeleteValue(issue.ValueName ?? "", throwOnMissingValue: false);
                                return true;
                            }
                        }
                        else
                        {
                            // OrphanedUninstallEntry / StaleAppPath: delete the subkey tree
                            var subPath = issue.KeyPath.Replace(@"HKLM\", "");
                            Registry.LocalMachine.DeleteSubKeyTree(subPath, throwOnMissingSubKey: false);
                            return true;
                        }
                    }
                    catch (UnauthorizedAccessException) { /* Needs admin - skip */ }
                    catch (System.Security.SecurityException) { /* Needs admin - skip */ }
                    catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[registry-optimizer] Error: {ex.Message}"); }
                    return false;

                default:
                    return false;
            }
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[registry-optimizer] Error: {ex.Message}"); return false; }
    }

    // ── BACKUP ───────────────────────────────────────────────

    private static async Task<string> CreateBackupAsync()
    {
        var backupDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AuraCorePro", "RegistryBackups");
        Directory.CreateDirectory(backupDir);

        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var backupFile = Path.Combine(backupDir, $"registry_backup_{timestamp}.reg");

        try
        {
            // Export HKCU (user registry — what we modify)
            var psi = new ProcessStartInfo
            {
                FileName = "reg.exe",
                Arguments = $"export HKCU \"{backupFile}\" /y",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true
            };
            using var proc = Process.Start(psi);
            if (proc is not null) await proc.WaitForExitAsync();
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[registry-optimizer] Error: {ex.Message}"); }

        return backupFile;
    }

    private static async Task RestoreBackupAsync(string path)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "reg.exe",
                Arguments = $"import \"{path}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true
            };
            using var proc = Process.Start(psi);
            if (proc is not null) await proc.WaitForExitAsync();
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[registry-optimizer] Error: {ex.Message}"); }
    }
}

public static class RegistryOptimizerRegistration
{
    public static IServiceCollection AddRegistryOptimizerModule(
        this IServiceCollection services)
        => services.AddSingleton<IOptimizationModule, RegistryOptimizerModule>();
}
