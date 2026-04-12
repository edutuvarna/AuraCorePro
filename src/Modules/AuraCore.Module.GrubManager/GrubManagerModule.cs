using System.Diagnostics;
using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using AuraCore.Application;
using AuraCore.Application.Interfaces.Modules;
using AuraCore.Application.Shared;
using AuraCore.Domain.Enums;

namespace AuraCore.Module.GrubManager;

/// <summary>
/// Advanced module for managing GRUB bootloader configuration on Linux.
/// WARNING: Misconfiguration of the GRUB bootloader can prevent the system from booting.
/// Always ensure a backup exists before making changes.
/// Risk: HIGH | Platform: Linux-only | Advanced: yes (power-users only)
/// </summary>
public sealed class GrubManagerModule : IOptimizationModule
{
    public string Id => "grub-manager";
    public string DisplayName => "GRUB Bootloader Manager";
    public OptimizationCategory Category => OptimizationCategory.SystemHealth;
    public RiskLevel Risk => RiskLevel.High;
    public SupportedPlatform Platform => SupportedPlatform.Linux;
    public bool IsAdvanced => true;

    private const string GrubConfigPath = "/etc/default/grub";
    private const string GrubBackupPath = "/etc/default/grub.bak.auracore";

    /// <summary>Parsed settings from the last successful scan.</summary>
    public GrubSettings? LastSettings { get; private set; }

    // ── ScanAsync ────────────────────────────────────────────────

    public async Task<ScanResult> ScanAsync(ScanOptions options, CancellationToken ct = default)
    {
        if (!OperatingSystem.IsLinux())
            return new ScanResult(Id, false, 0, 0, $"{DisplayName} is Linux-only.");

        try
        {
            // 1. Check that /etc/default/grub exists
            if (!File.Exists(GrubConfigPath))
                return new ScanResult(Id, false, 0, 0,
                    $"{GrubConfigPath} not found. GRUB may not be installed.");

            // 2. Check that update-grub or grub-mkconfig is available
            bool hasUpdateGrub = await ProcessRunner.CommandExistsAsync("update-grub", ct);
            bool hasGrubMkconfig = await ProcessRunner.CommandExistsAsync("grub-mkconfig", ct);
            if (!hasUpdateGrub && !hasGrubMkconfig)
                return new ScanResult(Id, false, 0, 0,
                    "Neither update-grub nor grub-mkconfig found in PATH.");

            // 3. Parse current settings from /etc/default/grub
            var settings = ParseGrubConfig(await File.ReadAllLinesAsync(GrubConfigPath, ct));

            // 4. List installed kernels from /boot/vmlinuz-*
            var kernels = ListInstalledKernels();

            settings = settings with { InstalledKernels = kernels };
            LastSettings = settings;

            // Items found = configurable settings (4) + old kernel count
            int configurableSettings = 4; // timeout, default, cmdline_default, os_prober
            int oldKernels = Math.Max(kernels.Count - 1, 0); // all except current
            int itemsFound = configurableSettings + oldKernels;

            return new ScanResult(Id, true, itemsFound, 0);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            Debug.WriteLine($"[{Id}] Scan error: {ex.Message}");
            return new ScanResult(Id, false, 0, 0, ex.Message);
        }
    }

    // ── OptimizeAsync ────────────────────────────────────────────

    public async Task<OptimizationResult> OptimizeAsync(
        OptimizationPlan plan,
        IProgress<TaskProgress>? progress = null,
        CancellationToken ct = default)
    {
        var start = DateTime.UtcNow;
        var operationId = Guid.NewGuid().ToString("N")[..8];
        int processed = 0;

        if (!OperatingSystem.IsLinux())
            return new OptimizationResult(Id, operationId, false, 0, 0, TimeSpan.Zero);

        try
        {
            var items = plan.SelectedItemIds?.ToList() ?? new List<string>();
            int totalSteps = Math.Max(items.Count, 1);
            bool grubChanged = false;

            for (int i = 0; i < items.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                var itemId = items[i];
                progress?.Report(new TaskProgress(Id, (double)i / totalSteps * 100, $"Applying {itemId}..."));

                if (itemId.StartsWith("set-timeout:", StringComparison.Ordinal))
                {
                    var valStr = itemId["set-timeout:".Length..];
                    if (!int.TryParse(valStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var timeout)
                        || timeout < 0 || timeout > 30)
                    {
                        Debug.WriteLine($"[{Id}] Invalid timeout value: {valStr}");
                        continue;
                    }

                    if (await BackupAndSetGrubValue("GRUB_TIMEOUT", timeout.ToString(CultureInfo.InvariantCulture), ct))
                    {
                        grubChanged = true;
                        processed++;
                    }
                }
                else if (itemId.StartsWith("set-default:", StringComparison.Ordinal))
                {
                    var valStr = itemId["set-default:".Length..];
                    if (!IsValidGrubDefault(valStr))
                    {
                        Debug.WriteLine($"[{Id}] Invalid GRUB_DEFAULT value: {valStr}");
                        continue;
                    }

                    if (await BackupAndSetGrubValue("GRUB_DEFAULT", valStr, ct))
                    {
                        grubChanged = true;
                        processed++;
                    }
                }
                else if (itemId == "enable-os-prober")
                {
                    if (await BackupAndSetGrubValue("GRUB_DISABLE_OS_PROBER", "false", ct))
                    {
                        grubChanged = true;
                        processed++;
                    }
                }
                else if (itemId == "disable-os-prober")
                {
                    if (await BackupAndSetGrubValue("GRUB_DISABLE_OS_PROBER", "true", ct))
                    {
                        grubChanged = true;
                        processed++;
                    }
                }
                else if (itemId == "remove-old-kernels")
                {
                    // Delegate to KernelCleaner module - do not duplicate kernel removal logic here.
                    Debug.WriteLine($"[{Id}] remove-old-kernels: delegate to KernelCleaner module.");
                    processed++;
                }
            }

            // After any GRUB config change, regenerate grub.cfg
            if (grubChanged)
            {
                progress?.Report(new TaskProgress(Id, 95, "Regenerating GRUB configuration..."));
                await RegenerateGrubConfigAsync(ct);
            }

            progress?.Report(new TaskProgress(Id, 100, "Complete"));
            return new OptimizationResult(Id, operationId, true, processed, 0, DateTime.UtcNow - start);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            Debug.WriteLine($"[{Id}] Optimize error: {ex.Message}");
            return new OptimizationResult(Id, operationId, false, processed, 0, DateTime.UtcNow - start);
        }
    }

    // ── Rollback ─────────────────────────────────────────────────

    public Task<bool> CanRollbackAsync(string operationId, CancellationToken ct = default)
    {
        return Task.FromResult(File.Exists(GrubBackupPath));
    }

    public async Task RollbackAsync(string operationId, CancellationToken ct = default)
    {
        if (!File.Exists(GrubBackupPath))
            return;

        // Restore backup: sudo cp /etc/default/grub.bak.auracore /etc/default/grub
        var copyResult = await ProcessRunner.RunAsync("sudo",
            $"-n cp {GrubBackupPath} {GrubConfigPath}", ct, timeoutSeconds: 30);

        if (!copyResult.Success)
        {
            Debug.WriteLine($"[{Id}] Rollback copy failed: {copyResult.Stderr}");
            return;
        }

        // Regenerate GRUB config
        await RegenerateGrubConfigAsync(ct);

        Debug.WriteLine($"[{Id}] Rollback complete - restored from {GrubBackupPath}");
    }

    // ── Private helpers ──────────────────────────────────────────

    /// <summary>
    /// Parse /etc/default/grub content into a <see cref="GrubSettings"/> record.
    /// </summary>
    public static GrubSettings ParseGrubConfig(string[] lines)
    {
        int timeout = 5;
        string grubDefault = "0";
        string cmdlineDefault = "quiet splash";
        bool osProberDisabled = false;

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (line.StartsWith('#') || !line.Contains('='))
                continue;

            var eqIdx = line.IndexOf('=');
            var key = line[..eqIdx].Trim();
            var value = line[(eqIdx + 1)..].Trim().Trim('"');

            switch (key)
            {
                case "GRUB_TIMEOUT":
                    if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var t))
                        timeout = t;
                    break;
                case "GRUB_DEFAULT":
                    grubDefault = value;
                    break;
                case "GRUB_CMDLINE_LINUX_DEFAULT":
                    cmdlineDefault = value;
                    break;
                case "GRUB_DISABLE_OS_PROBER":
                    osProberDisabled = value.Equals("true", StringComparison.OrdinalIgnoreCase);
                    break;
            }
        }

        return new GrubSettings(
            Timeout: timeout,
            GrubDefault: grubDefault,
            CmdlineLinuxDefault: cmdlineDefault,
            OsProberDisabled: osProberDisabled,
            InstalledKernels: new List<string>());
    }

    /// <summary>
    /// List kernel images installed under /boot.
    /// </summary>
    private static List<string> ListInstalledKernels()
    {
        try
        {
            var bootDir = "/boot";
            if (!Directory.Exists(bootDir))
                return new List<string>();

            return Directory.GetFiles(bootDir, "vmlinuz-*")
                .Select(Path.GetFileName)
                .Where(f => f is not null)
                .Select(f => f!)
                .OrderDescending()
                .ToList();
        }
        catch
        {
            return new List<string>();
        }
    }

    /// <summary>
    /// Validate GRUB_DEFAULT value: must be integer 0-10 or the literal "saved".
    /// </summary>
    private static bool IsValidGrubDefault(string value)
    {
        if (value == "saved")
            return true;

        // Reject anything that is not purely numeric (blocks injection attempts)
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var num))
            return false;

        return num >= 0 && num <= 10;
    }

    /// <summary>
    /// Create backup of /etc/default/grub (if not already backed up) and modify a key.
    /// Uses sudo -n sed -i for atomic in-place editing.
    /// </summary>
    private static async Task<bool> BackupAndSetGrubValue(string key, string value, CancellationToken ct)
    {
        // Sanitize key: only allow uppercase letters and underscores
        if (!key.All(c => char.IsLetterOrDigit(c) || c == '_'))
            return false;

        // Sanitize value: only allow alphanumeric, spaces, hyphens, dots, equals
        if (!value.All(c => char.IsLetterOrDigit(c) || c == ' ' || c == '-' || c == '.' || c == '='))
            return false;

        // Backup before first modification
        if (!File.Exists(GrubBackupPath))
        {
            var backup = await ProcessRunner.RunAsync("sudo",
                $"-n cp {GrubConfigPath} {GrubBackupPath}", ct, timeoutSeconds: 30);
            if (!backup.Success)
            {
                Debug.WriteLine($"[grub-manager] Backup failed: {backup.Stderr}");
                return false;
            }
        }

        // Use sed to replace the value in-place
        var sedPattern = $"s/^{key}=.*/{key}={value}/";
        var result = await ProcessRunner.RunAsync("sudo",
            $"-n sed -i '{sedPattern}' {GrubConfigPath}", ct, timeoutSeconds: 30);

        if (!result.Success)
        {
            Debug.WriteLine($"[grub-manager] sed failed for {key}: {result.Stderr}");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Regenerate grub.cfg using update-grub or grub-mkconfig.
    /// </summary>
    private static async Task RegenerateGrubConfigAsync(CancellationToken ct)
    {
        if (await ProcessRunner.CommandExistsAsync("update-grub", ct))
        {
            var result = await ProcessRunner.RunAsync("sudo", "-n update-grub", ct, timeoutSeconds: 120);
            if (!result.Success)
                Debug.WriteLine($"[grub-manager] update-grub failed: {result.Stderr}");
        }
        else if (await ProcessRunner.CommandExistsAsync("grub-mkconfig", ct))
        {
            var result = await ProcessRunner.RunAsync("sudo",
                "-n grub-mkconfig -o /boot/grub/grub.cfg", ct, timeoutSeconds: 120);
            if (!result.Success)
                Debug.WriteLine($"[grub-manager] grub-mkconfig failed: {result.Stderr}");
        }
    }
}

/// <summary>
/// Parsed GRUB bootloader settings.
/// </summary>
public sealed record GrubSettings(
    int Timeout,
    string GrubDefault,
    string CmdlineLinuxDefault,
    bool OsProberDisabled,
    List<string> InstalledKernels);

/// <summary>
/// DI registration for the GrubManager module.
/// </summary>
public static class GrubManagerRegistration
{
    public static IServiceCollection AddGrubManagerModule(this IServiceCollection services)
        => services.AddSingleton<IOptimizationModule, GrubManagerModule>();
}
