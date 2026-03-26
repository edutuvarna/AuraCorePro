using Microsoft.Extensions.DependencyInjection;
using AuraCore.Application;
using AuraCore.Application.Interfaces.Modules;
using AuraCore.Domain.Enums;
using AuraCore.Module.BatteryOptimizer.Models;
using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AuraCore.Module.BatteryOptimizer;

/// <summary>
/// Battery Optimizer - Monitor battery health, manage power plans, and optimize battery life.
/// Uses WMI (BatteryStaticData, BatteryStatus, BatteryFullChargedCapacity) and powercfg.
/// </summary>
public sealed class BatteryOptimizerModule : IOptimizationModule
{
    public string Id => "battery-optimizer";
    public string DisplayName => "Battery Optimizer";
    public OptimizationCategory Category => OptimizationCategory.SystemHealth;
    public RiskLevel Risk => RiskLevel.Low;

    public BatteryStatus? LastStatus { get; private set; }
    public List<PowerPlanInfo> LastPowerPlans { get; private set; } = new();
    public List<PowerDrainApp> LastDrainApps { get; private set; } = new();

    public async Task<ScanResult> ScanAsync(ScanOptions options, CancellationToken ct = default)
    {
        LastStatus = await GetBatteryStatusAsync(ct);
        LastPowerPlans = await GetPowerPlansAsync(ct);
        LastDrainApps = await GetPowerDrainAppsAsync(ct);

        var issues = 0;
        if (LastStatus is not null)
        {
            if (LastStatus.HealthPercent < 50) issues++;
            if (LastStatus.WearPercent > 30) issues++;
        }
        return new ScanResult(Id, true, issues, 0);
    }

    public Task<OptimizationResult> OptimizeAsync(
        OptimizationPlan plan, IProgress<TaskProgress>? progress = null, CancellationToken ct = default)
        => Task.FromResult(new OptimizationResult(Id, "", true, 0, 0, TimeSpan.Zero));

    public Task<bool> CanRollbackAsync(string operationId, CancellationToken ct = default) => Task.FromResult(false);
    public Task RollbackAsync(string operationId, CancellationToken ct = default) => Task.CompletedTask;

    // ═══════════════════════════════════════════════════════════
    // Battery Status
    // ═══════════════════════════════════════════════════════════

    public async Task<BatteryStatus> GetBatteryStatusAsync(CancellationToken ct = default)
    {
        var status = new BatteryStatus();
        try
        {
            // Basic battery info via WMI
            var output = await RunPS(
                "$b = Get-CimInstance Win32_Battery -ErrorAction SilentlyContinue; " +
                "if ($b) { @{ " +
                "  hasBattery=$true; " +
                "  percent=[int]$b.EstimatedChargeRemaining; " +
                "  statusCode=[int]$b.BatteryStatus; " +
                "  name=$b.Name; " +
                "  chemistry=[int]$b.Chemistry; " +
                "  estMinutes=[int]$b.EstimatedRunTime; " +
                "  statusText=$b.Status " +
                "} | ConvertTo-Json } else { '{\"hasBattery\":false}' }", ct);

            if (!string.IsNullOrWhiteSpace(output))
            {
                var doc = JsonDocument.Parse(output.Trim());
                var root = doc.RootElement;
                status.HasBattery = GetBool(root, "hasBattery");
                if (!status.HasBattery) return status;

                status.ChargePercent = GetInt(root, "percent");
                status.BatteryName = GetStr(root, "name");
                var statusCode = GetInt(root, "statusCode");
                // 1=Discharging, 2=AC, 3=FullCharged, 4=Low, 5=Critical, 6=Charging
                status.IsCharging = statusCode == 6 || statusCode == 2;
                status.IsOnAC = statusCode == 2 || statusCode == 3 || statusCode == 6;
                status.ChargeStatus = statusCode switch
                {
                    1 => "Discharging", 2 => "On AC", 3 => "Fully Charged",
                    4 => "Low", 5 => "Critical", 6 => "Charging",
                    _ => "Unknown"
                };
                status.Chemistry = GetInt(root, "chemistry") switch
                {
                    1 => "Other", 2 => "Unknown", 3 => "Lead Acid",
                    4 => "Nickel Cadmium", 5 => "Nickel Metal Hydride",
                    6 => "Lithium-ion", 7 => "Zinc Air", 8 => "Lithium Polymer",
                    _ => "Unknown"
                };
                var estMin = GetInt(root, "estMinutes");
                if (estMin > 0 && estMin < 71582)
                    status.EstimatedRemaining = TimeSpan.FromMinutes(estMin);
            }

            // Capacity via WMI root\WMI
            var capOutput = await RunPS(
                "try { " +
                "  $design = (Get-CimInstance -Namespace root/WMI -ClassName BatteryStaticData -ErrorAction Stop).DesignedCapacity; " +
                "  $full = (Get-CimInstance -Namespace root/WMI -ClassName BatteryFullChargedCapacity -ErrorAction Stop).FullChargedCapacity; " +
                "  $cycles = (Get-CimInstance -Namespace root/WMI -ClassName BatteryStaticData -ErrorAction Stop).CycleCount; " +
                "  $sn = (Get-CimInstance -Namespace root/WMI -ClassName BatteryStaticData -ErrorAction Stop).SerialNumber; " +
                "  $mfr = (Get-CimInstance -Namespace root/WMI -ClassName BatteryStaticData -ErrorAction Stop).ManufactureName; " +
                "  @{ design=[int]$design; full=[int]$full; cycles=[int]$cycles; sn=$sn; mfr=$mfr } | ConvertTo-Json " +
                "} catch { '{}' }", ct);

            if (!string.IsNullOrWhiteSpace(capOutput) && capOutput.Trim() != "{}")
            {
                try
                {
                    var capDoc = JsonDocument.Parse(capOutput.Trim());
                    var capRoot = capDoc.RootElement;
                    status.DesignCapacityMWh = GetInt(capRoot, "design");
                    status.FullChargeCapacityMWh = GetInt(capRoot, "full");
                    status.CycleCount = GetInt(capRoot, "cycles");
                    status.SerialNumber = GetStr(capRoot, "sn");
                    status.Manufacturer = GetStr(capRoot, "mfr");
                }
                catch { }
            }
        }
        catch (Exception ex) { status.Error = ex.Message; }

        LastStatus = status;
        return status;
    }

    // ═══════════════════════════════════════════════════════════
    // Power Plans
    // ═══════════════════════════════════════════════════════════

    public async Task<List<PowerPlanInfo>> GetPowerPlansAsync(CancellationToken ct = default)
    {
        var plans = new List<PowerPlanInfo>();
        try
        {
            var output = await RunPS("powercfg /list", ct);
            if (string.IsNullOrWhiteSpace(output)) return plans;

            // Parse powercfg output
            foreach (var line in output.Split('\n'))
            {
                var match = Regex.Match(line, @"GUID:\s*([0-9a-f-]+)\s+\((.+?)\)(\s*\*)?");
                if (!match.Success) continue;
                plans.Add(new PowerPlanInfo
                {
                    PlanId = Guid.Parse(match.Groups[1].Value),
                    Name = match.Groups[2].Value.Trim(),
                    IsActive = match.Groups[3].Success
                });
            }
        }
        catch { }

        LastPowerPlans = plans;
        return plans;
    }

    /// <summary>Switch to a specific power plan</summary>
    public async Task<bool> SetPowerPlanAsync(Guid planId, CancellationToken ct = default)
        => await RunPSBool($"powercfg /setactive {planId}", ct);

    /// <summary>Create a custom battery saver plan</summary>
    public async Task<bool> EnableBatterySaverAsync(CancellationToken ct = default)
    {
        // Use the built-in Power Saver plan
        var plans = await GetPowerPlansAsync(ct);
        var saver = plans.FirstOrDefault(p => p.Name.Contains("Power saver", StringComparison.OrdinalIgnoreCase)
            || p.Name.Contains("Enerji tasarrufu", StringComparison.OrdinalIgnoreCase));
        if (saver is not null)
            return await SetPowerPlanAsync(saver.PlanId, ct);

        // Fallback: known GUID for Power Saver
        return await RunPSBool("powercfg /setactive a1841308-3541-4fab-bc81-f71556f20b4a", ct);
    }

    // ═══════════════════════════════════════════════════════════
    // Power Drain Analysis
    // ═══════════════════════════════════════════════════════════

    public async Task<List<PowerDrainApp>> GetPowerDrainAppsAsync(CancellationToken ct = default)
    {
        var apps = new List<PowerDrainApp>();
        try
        {
            var output = await RunPS(
                "Get-Process | Where-Object { $_.CPU -gt 0 -and $_.ProcessName -ne 'Idle' } | " +
                "Sort-Object CPU -Descending | Select-Object -First 15 ProcessName, " +
                "@{N='CpuSec';E={[math]::Round($_.CPU,1)}}, " +
                "@{N='MemMB';E={[math]::Round($_.WorkingSet64/1MB,0)}} | " +
                "ConvertTo-Json", ct);

            if (string.IsNullOrWhiteSpace(output)) return apps;

            var doc = JsonDocument.Parse(output);
            var elements = doc.RootElement.ValueKind == JsonValueKind.Array
                ? doc.RootElement.EnumerateArray().ToList()
                : new List<JsonElement> { doc.RootElement };

            foreach (var el in elements)
            {
                var name = GetStr(el, "ProcessName");
                var cpuSec = el.TryGetProperty("CpuSec", out var cpuProp)
                    ? cpuProp.GetDouble() : 0;
                var memMB = GetInt(el, "MemMB");

                var impact = cpuSec > 300 || memMB > 500 ? "High"
                    : cpuSec > 60 || memMB > 200 ? "Medium"
                    : "Low";

                apps.Add(new PowerDrainApp
                {
                    Name = name,
                    CpuPercent = cpuSec,
                    WorkingSetMB = memMB,
                    Impact = impact
                });
            }
        }
        catch { }

        LastDrainApps = apps;
        return apps;
    }

    // ═══════════════════════════════════════════════════════════
    // Battery Report
    // ═══════════════════════════════════════════════════════════

    /// <summary>Generate Windows battery report HTML</summary>
    public async Task<BatteryReportResult> GenerateBatteryReportAsync(CancellationToken ct = default)
    {
        var reportPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            "AuraCore-BatteryReport.html");

        try
        {
            var ok = await RunPSBool($"powercfg /batteryreport /output \"{reportPath}\"", ct);
            if (ok && File.Exists(reportPath))
            {
                // Open in browser
                Process.Start(new ProcessStartInfo { FileName = reportPath, UseShellExecute = true });
                return new BatteryReportResult { ReportPath = reportPath, Success = true };
            }
            return new BatteryReportResult { Success = false, Error = "Report generation failed" };
        }
        catch (Exception ex)
        {
            return new BatteryReportResult { Success = false, Error = ex.Message };
        }
    }

    /// <summary>Open Windows power settings</summary>
    public void OpenPowerSettings()
    {
        try { Process.Start(new ProcessStartInfo { FileName = "ms-settings:powersleep", UseShellExecute = true }); }
        catch { }
    }

    // ── Helpers ──

    private static async Task<string?> RunPS(string command, CancellationToken ct)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"{command}\"",
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true
            };
            using var proc = Process.Start(psi);
            if (proc is null) return null;
            var output = await proc.StandardOutput.ReadToEndAsync(ct);
            await proc.WaitForExitAsync(ct);
            return output;
        }
        catch { return null; }
    }

    private static async Task<bool> RunPSBool(string command, CancellationToken ct)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"{command}\"",
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true
            };
            using var proc = Process.Start(psi);
            if (proc is null) return false;
            await proc.WaitForExitAsync(ct);
            return proc.ExitCode == 0;
        }
        catch { return false; }
    }

    private static bool GetBool(JsonElement el, string p) =>
        el.TryGetProperty(p, out var v) && v.ValueKind == JsonValueKind.True;
    private static string GetStr(JsonElement el, string p) =>
        el.TryGetProperty(p, out var v) && v.ValueKind == JsonValueKind.String ? (v.GetString() ?? "") : "";
    private static int GetInt(JsonElement el, string p) =>
        el.TryGetProperty(p, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : 0;
}

public static class BatteryOptimizerRegistration
{
    public static IServiceCollection AddBatteryOptimizerModule(
        this IServiceCollection services)
        => services.AddSingleton<IOptimizationModule, BatteryOptimizerModule>();
}
