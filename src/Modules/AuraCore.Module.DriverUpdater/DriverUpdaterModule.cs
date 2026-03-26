using Microsoft.Extensions.DependencyInjection;
using AuraCore.Application;
using AuraCore.Application.Interfaces.Modules;
using AuraCore.Domain.Enums;
using AuraCore.Module.DriverUpdater.Models;
using System.Diagnostics;
using System.Text.Json;

namespace AuraCore.Module.DriverUpdater;

/// <summary>
/// Driver Updater - Scan, analyze, and backup system drivers.
/// Uses WMI (Win32_PnPSignedDriver) for reliable driver enumeration.
/// Supports: driver scan, age analysis, problem detection, backup, Windows Update check.
/// </summary>
public sealed class DriverUpdaterModule : IOptimizationModule
{
    public string Id => "driver-updater";
    public string DisplayName => "Driver Updater";
    public OptimizationCategory Category => OptimizationCategory.SystemHealth;
    public RiskLevel Risk => RiskLevel.Low;

    public DriverScanReport? LastReport { get; private set; }

    public async Task<ScanResult> ScanAsync(ScanOptions options, CancellationToken ct = default)
    {
        var drivers = await ScanDriversAsync(ct);
        LastReport = new DriverScanReport { Drivers = drivers, ScanTime = DateTimeOffset.UtcNow };
        return new ScanResult(Id, true, LastReport.OutdatedCount + LastReport.ProblemCount, 0);
    }

    public Task<OptimizationResult> OptimizeAsync(
        OptimizationPlan plan, IProgress<TaskProgress>? progress = null, CancellationToken ct = default)
    {
        // Driver updates can't be automated safely - we provide info only
        return Task.FromResult(new OptimizationResult(Id, "", true, 0, 0, TimeSpan.Zero));
    }

    public Task<bool> CanRollbackAsync(string operationId, CancellationToken ct = default)
        => Task.FromResult(false);

    public Task RollbackAsync(string operationId, CancellationToken ct = default)
        => Task.CompletedTask;

    // ═══════════════════════════════════════════════════════════
    // Driver Scanning
    // ═══════════════════════════════════════════════════════════

    /// <summary>Scan all signed drivers via WMI</summary>
    public async Task<List<DriverInfo>> ScanDriversAsync(CancellationToken ct = default)
    {
        var drivers = new List<DriverInfo>();

        try
        {
            var output = await RunPowerShellOutputAsync(
                "Get-CimInstance Win32_PnPSignedDriver | Where-Object { $_.DeviceName -ne $null } | " +
                "Select-Object DeviceName,DeviceClass,Manufacturer,DriverVersion,DriverDate," +
                "InfName,HardwareID,Status,ConfigManagerErrorCode | ConvertTo-Json -Depth 2", ct);

            if (string.IsNullOrWhiteSpace(output)) return drivers;

            var doc = JsonDocument.Parse(output);
            var elements = doc.RootElement.ValueKind == JsonValueKind.Array
                ? doc.RootElement.EnumerateArray().ToList()
                : new List<JsonElement> { doc.RootElement };

            foreach (var el in elements)
            {
                var name = GetStr(el, "DeviceName");
                if (string.IsNullOrWhiteSpace(name)) continue;

                var driverDate = DateTimeOffset.MinValue;
                if (el.TryGetProperty("DriverDate", out var dateProp))
                {
                    if (dateProp.ValueKind == JsonValueKind.String &&
                        DateTimeOffset.TryParse(dateProp.GetString(), out var parsed))
                        driverDate = parsed;
                }

                var hwId = "";
                if (el.TryGetProperty("HardwareID", out var hwProp))
                {
                    if (hwProp.ValueKind == JsonValueKind.String)
                        hwId = hwProp.GetString() ?? "";
                    else if (hwProp.ValueKind == JsonValueKind.Array)
                    {
                        var first = hwProp.EnumerateArray().FirstOrDefault();
                        if (first.ValueKind == JsonValueKind.String)
                            hwId = first.GetString() ?? "";
                    }
                }

                var errorCode = 0;
                if (el.TryGetProperty("ConfigManagerErrorCode", out var errProp) &&
                    errProp.ValueKind == JsonValueKind.Number)
                    errorCode = errProp.GetInt32();

                drivers.Add(new DriverInfo
                {
                    DeviceName = name,
                    DeviceClass = GetStr(el, "DeviceClass"),
                    Manufacturer = GetStr(el, "Manufacturer"),
                    DriverVersion = GetStr(el, "DriverVersion"),
                    DriverDate = driverDate,
                    InfName = GetStr(el, "InfName"),
                    HardwareId = hwId,
                    Status = GetStr(el, "Status"),
                    HasProblem = errorCode != 0,
                    ProblemCode = errorCode
                });
            }

            // Sort: problems first, then by age (oldest first)
            drivers = drivers
                .OrderByDescending(d => d.HasProblem)
                .ThenByDescending(d => d.AgeDays)
                .ToList();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Driver scan error: {ex.Message}");
        }

        return drivers;
    }

    // ═══════════════════════════════════════════════════════════
    // Driver Backup
    // ═══════════════════════════════════════════════════════════

    /// <summary>Export all third-party drivers to a backup folder</summary>
    public async Task<DriverBackupResult> BackupDriversAsync(string backupPath, CancellationToken ct = default)
    {
        var result = new DriverBackupResult { BackupPath = backupPath };

        try
        {
            Directory.CreateDirectory(backupPath);

            // PnPUtil /export-driver exports third-party drivers
            var output = await RunPowerShellOutputAsync(
                $"$count = 0; " +
                $"Get-WindowsDriver -Online -All | Where-Object {{ $_.OriginalFileName -notlike '*windows*' }} | " +
                $"ForEach-Object {{ " +
                $"  try {{ " +
                $"    $dest = Join-Path '{backupPath}' $_.Driver; " +
                $"    New-Item -ItemType Directory -Path $dest -Force | Out-Null; " +
                $"    pnputil /export-driver $_.Driver $dest 2>$null | Out-Null; " +
                $"    $count++; " +
                $"  }} catch {{ }} " +
                $"}}; " +
                $"$size = (Get-ChildItem '{backupPath}' -Recurse -File | Measure-Object Length -Sum).Sum; " +
                $"@{{ count=$count; size=$size }} | ConvertTo-Json", ct);

            if (!string.IsNullOrWhiteSpace(output))
            {
                try
                {
                    var doc = JsonDocument.Parse(output);
                    result.DriversExported = doc.RootElement.TryGetProperty("count", out var c) ? c.GetInt32() : 0;
                    result.SizeBytes = doc.RootElement.TryGetProperty("size", out var s) ? s.GetInt64() : 0;
                }
                catch { }
            }

            result.Success = result.DriversExported > 0;
        }
        catch (Exception ex)
        {
            result.Error = ex.Message;
        }

        return result;
    }

    // ═══════════════════════════════════════════════════════════
    // Windows Update Driver Check
    // ═══════════════════════════════════════════════════════════

    /// <summary>Check Windows Update for available driver updates</summary>
    public async Task<List<string>> CheckWindowsUpdateDriversAsync(CancellationToken ct = default)
    {
        var updates = new List<string>();

        try
        {
            var output = await RunPowerShellOutputAsync(
                "$session = New-Object -ComObject Microsoft.Update.Session; " +
                "$searcher = $session.CreateUpdateSearcher(); " +
                "try { " +
                "  $results = $searcher.Search('IsInstalled=0 AND Type=''Driver'''); " +
                "  $results.Updates | ForEach-Object { $_.Title } " +
                "} catch { 'ERROR: ' + $_.Exception.Message }", ct);

            if (!string.IsNullOrWhiteSpace(output))
            {
                foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                {
                    var trimmed = line.Trim();
                    if (!string.IsNullOrWhiteSpace(trimmed) && !trimmed.StartsWith("ERROR:"))
                        updates.Add(trimmed);
                }
            }
        }
        catch { }

        return updates;
    }

    /// <summary>Open Windows Update settings</summary>
    public async Task<bool> OpenWindowsUpdateAsync(CancellationToken ct = default)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "ms-settings:windowsupdate",
                UseShellExecute = true
            });
            return true;
        }
        catch { return false; }
    }

    /// <summary>Open Device Manager</summary>
    public async Task<bool> OpenDeviceManagerAsync(CancellationToken ct = default)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "devmgmt.msc",
                UseShellExecute = true
            });
            return true;
        }
        catch { return false; }
    }

    // ── Helpers ──

    private static async Task<string?> RunPowerShellOutputAsync(string command, CancellationToken ct)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"{command}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var proc = Process.Start(psi);
            if (proc is null) return null;

            var output = await proc.StandardOutput.ReadToEndAsync(ct);
            await proc.WaitForExitAsync(ct);
            return output;
        }
        catch { return null; }
    }

    private static string GetStr(JsonElement el, string prop)
    {
        if (!el.TryGetProperty(prop, out var v)) return "";
        return v.ValueKind == JsonValueKind.String ? (v.GetString() ?? "") : "";
    }
}

public static class DriverUpdaterRegistration
{
    public static IServiceCollection AddDriverUpdaterModule(
        this IServiceCollection services)
        => services.AddSingleton<IOptimizationModule, DriverUpdaterModule>();
}
