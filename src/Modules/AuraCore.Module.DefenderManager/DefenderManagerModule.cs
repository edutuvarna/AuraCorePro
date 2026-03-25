using Microsoft.Extensions.DependencyInjection;
using AuraCore.Application;
using AuraCore.Application.Interfaces.Modules;
using AuraCore.Domain.Enums;
using AuraCore.Module.DefenderManager.Models;
using System.Diagnostics;

namespace AuraCore.Module.DefenderManager;

/// <summary>
/// Windows Defender Manager - Monitor and control Windows Security features.
/// Uses PowerShell cmdlets (Get-MpComputerStatus, Get-MpThreat, etc.)
/// </summary>
public sealed class DefenderManagerModule : IOptimizationModule
{
    public string Id => "defender-manager";
    public string DisplayName => "Defender Manager";
    public OptimizationCategory Category => OptimizationCategory.SystemHealth;
    public RiskLevel Risk => RiskLevel.Low;

    public DefenderStatus? LastStatus { get; private set; }
    public List<ThreatInfo> LastThreats { get; private set; } = new();
    public List<ExclusionInfo> LastExclusions { get; private set; } = new();

    /// <summary>Scan = Gather Defender status</summary>
    public async Task<ScanResult> ScanAsync(ScanOptions options, CancellationToken ct = default)
    {
        LastStatus = await GetDefenderStatusAsync(ct);
        LastThreats = await GetThreatHistoryAsync(ct);
        LastExclusions = await GetExclusionsAsync(ct);

        var issues = 0;
        if (LastStatus != null)
        {
            if (!LastStatus.RealTimeProtection) issues++;
            if (!LastStatus.CloudProtection) issues++;
            if (!LastStatus.BehaviorMonitoring) issues++;
            if (LastStatus.SignaturesOutdated) issues++;
            if (!LastStatus.FirewallDomain || !LastStatus.FirewallPrivate || !LastStatus.FirewallPublic) issues++;
        }

        return new ScanResult(Id, true, issues, 0);
    }

    /// <summary>Optimize = Update definitions</summary>
    public async Task<OptimizationResult> OptimizeAsync(
        OptimizationPlan plan, IProgress<TaskProgress>? progress = null, CancellationToken ct = default)
    {
        var start = DateTime.UtcNow;
        progress?.Report(new TaskProgress(Id, 10, "Updating virus definitions..."));

        var success = await RunPowerShellAsync("Update-MpSignature", ct);

        progress?.Report(new TaskProgress(Id, 100, success ? "Definitions updated" : "Update failed"));
        return new OptimizationResult(Id, Guid.NewGuid().ToString(), success, success ? 1 : 0, 0, DateTime.UtcNow - start);
    }

    public Task<bool> CanRollbackAsync(string operationId, CancellationToken ct = default)
        => Task.FromResult(false);

    public Task RollbackAsync(string operationId, CancellationToken ct = default)
        => Task.CompletedTask;

    // ── Public actions for the UI ──

    public async Task<bool> StartQuickScanAsync(CancellationToken ct = default)
        => await RunPowerShellAsync("Start-MpScan -ScanType QuickScan", ct);

    public async Task<bool> StartFullScanAsync(CancellationToken ct = default)
        => await RunPowerShellAsync("Start-MpScan -ScanType FullScan", ct);

    public async Task<bool> UpdateDefinitionsAsync(CancellationToken ct = default)
        => await RunPowerShellAsync("Update-MpSignature", ct);

    public async Task<bool> SetProtectionAsync(string feature, bool enabled, CancellationToken ct = default)
    {
        var value = enabled ? "$true" : "$false";
        var cmd = feature switch
        {
            "RealTime" => $"Set-MpPreference -DisableRealtimeMonitoring {(enabled ? "$false" : "$true")}",
            "CloudProtection" => $"Set-MpPreference -MAPSReporting {(enabled ? "Advanced" : "Disabled")}",
            "BehaviorMonitoring" => $"Set-MpPreference -DisableBehaviorMonitoring {(enabled ? "$false" : "$true")}",
            "PUA" => $"Set-MpPreference -PUAProtection {(enabled ? "1" : "0")}",
            _ => ""
        };
        if (string.IsNullOrEmpty(cmd)) return false;
        return await RunPowerShellAsync(cmd, ct);
    }

    public async Task<bool> AddExclusionAsync(string type, string value, CancellationToken ct = default)
    {
        var cmd = type switch
        {
            "Path" => $"Add-MpPreference -ExclusionPath '{value}'",
            "Extension" => $"Add-MpPreference -ExclusionExtension '{value}'",
            "Process" => $"Add-MpPreference -ExclusionProcess '{value}'",
            _ => ""
        };
        if (string.IsNullOrEmpty(cmd)) return false;
        return await RunPowerShellAsync(cmd, ct);
    }

    public async Task<bool> RemoveExclusionAsync(string type, string value, CancellationToken ct = default)
    {
        var cmd = type switch
        {
            "Path" => $"Remove-MpPreference -ExclusionPath '{value}'",
            "Extension" => $"Remove-MpPreference -ExclusionExtension '{value}'",
            "Process" => $"Remove-MpPreference -ExclusionProcess '{value}'",
            _ => ""
        };
        if (string.IsNullOrEmpty(cmd)) return false;
        return await RunPowerShellAsync(cmd, ct);
    }

    // ── Internal helpers ──

    private static async Task<DefenderStatus> GetDefenderStatusAsync(CancellationToken ct)
    {
        var status = new DefenderStatus();
        try
        {
            var isAdmin = new System.Security.Principal.WindowsPrincipal(
                System.Security.Principal.WindowsIdentity.GetCurrent())
                .IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
            status.IsAdmin = isAdmin;

            var output = await RunPowerShellOutputAsync(
                "Get-MpComputerStatus | Select-Object " +
                "RealTimeProtectionEnabled,IoavProtectionEnabled,BehaviorMonitorEnabled," +
                "AntivirusSignatureVersion,AntivirusSignatureLastUpdated," +
                "AMEngineVersion,AMProductVersion,IsTamperProtected," +
                "MAPSReporting,PUAProtection | ConvertTo-Json", ct);

            if (string.IsNullOrWhiteSpace(output))
            {
                status.Error = "Could not read Defender status";
                return status;
            }

            var doc = System.Text.Json.JsonDocument.Parse(output);
            var root = doc.RootElement;

            status.RealTimeProtection = GetBool(root, "RealTimeProtectionEnabled");
            status.CloudProtection = GetBool(root, "IoavProtectionEnabled");
            status.BehaviorMonitoring = GetBool(root, "BehaviorMonitorEnabled");
            status.TamperProtection = GetBool(root, "IsTamperProtected");
            status.AntivirusSignatureVersion = GetString(root, "AntivirusSignatureVersion");
            status.EngineVersion = GetString(root, "AMEngineVersion");
            status.ProductVersion = GetString(root, "AMProductVersion");

            var mapsVal = GetInt(root, "MAPSReporting");
            status.CloudProtection = mapsVal > 0;
            status.PotentiallyUnwantedApps = GetInt(root, "PUAProtection") > 0;

            if (root.TryGetProperty("AntivirusSignatureLastUpdated", out var sigDate))
            {
                if (sigDate.ValueKind == System.Text.Json.JsonValueKind.String &&
                    DateTimeOffset.TryParse(sigDate.GetString(), out var dt))
                    status.AntivirusSignatureLastUpdated = dt;
            }

            // Firewall
            var fwOutput = await RunPowerShellOutputAsync(
                "Get-NetFirewallProfile | Select-Object Name,Enabled | ConvertTo-Json", ct);
            if (!string.IsNullOrWhiteSpace(fwOutput))
            {
                var fwDoc = System.Text.Json.JsonDocument.Parse(fwOutput);
                if (fwDoc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    foreach (var item in fwDoc.RootElement.EnumerateArray())
                    {
                        var name = GetString(item, "Name").ToLower();
                        var enabled = GetBool(item, "Enabled");
                        if (name.Contains("domain")) status.FirewallDomain = enabled;
                        else if (name.Contains("private")) status.FirewallPrivate = enabled;
                        else if (name.Contains("public")) status.FirewallPublic = enabled;
                    }
                }
            }

            // Network Protection
            var npOutput = await RunPowerShellOutputAsync(
                "(Get-MpPreference).EnableNetworkProtection", ct);
            status.NetworkProtection = npOutput?.Trim() == "1";
        }
        catch (Exception ex)
        {
            status.Error = ex.Message;
        }

        return status;
    }

    private static async Task<List<ThreatInfo>> GetThreatHistoryAsync(CancellationToken ct)
    {
        var threats = new List<ThreatInfo>();
        try
        {
            var output = await RunPowerShellOutputAsync(
                "Get-MpThreatDetection | Select-Object -First 20 " +
                "ThreatID,ProcessName,DomainUser,InitialDetectionTime,Resources | ConvertTo-Json", ct);

            if (string.IsNullOrWhiteSpace(output) || output.Trim() == "null") return threats;

            var doc = System.Text.Json.JsonDocument.Parse(output);
            var items = doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Array
                ? doc.RootElement.EnumerateArray().ToList()
                : new List<System.Text.Json.JsonElement> { doc.RootElement };

            // Get threat names
            var nameOutput = await RunPowerShellOutputAsync(
                "Get-MpThreat | Select-Object -First 20 ThreatID,ThreatName,SeverityID,IsActive | ConvertTo-Json", ct);

            var nameMap = new Dictionary<long, (string name, string severity, bool active)>();
            if (!string.IsNullOrWhiteSpace(nameOutput) && nameOutput.Trim() != "null")
            {
                var nameDoc = System.Text.Json.JsonDocument.Parse(nameOutput);
                var nameItems = nameDoc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Array
                    ? nameDoc.RootElement.EnumerateArray().ToList()
                    : new List<System.Text.Json.JsonElement> { nameDoc.RootElement };

                foreach (var ni in nameItems)
                {
                    var tid = GetLong(ni, "ThreatID");
                    var tname = GetString(ni, "ThreatName");
                    var sev = GetInt(ni, "SeverityID") switch
                    {
                        1 => "Low", 2 => "Medium", 4 => "High", 5 => "Severe", _ => "Unknown"
                    };
                    var active = GetBool(ni, "IsActive");
                    if (tid != 0) nameMap[tid] = (tname, sev, active);
                }
            }

            foreach (var item in items)
            {
                var tid = GetLong(item, "ThreatID");
                (string name, string severity, bool active) info = nameMap.TryGetValue(tid, out var n) ? n : ("Unknown Threat", "Unknown", false);

                var resources = "";
                if (item.TryGetProperty("Resources", out var resProp) &&
                    resProp.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    var first = resProp.EnumerateArray().FirstOrDefault();
                    if (first.ValueKind == System.Text.Json.JsonValueKind.String)
                        resources = first.GetString() ?? "";
                }

                var detected = DateTimeOffset.MinValue;
                if (item.TryGetProperty("InitialDetectionTime", out var dtProp) &&
                    dtProp.ValueKind == System.Text.Json.JsonValueKind.String &&
                    DateTimeOffset.TryParse(dtProp.GetString(), out var parsedDt))
                    detected = parsedDt;

                threats.Add(new ThreatInfo
                {
                    ThreatName = info.name,
                    Severity = info.severity,
                    Status = info.active ? "Active" : "Resolved",
                    Path = resources,
                    DetectedAt = detected
                });
            }
        }
        catch { }
        return threats;
    }

    private static async Task<List<ExclusionInfo>> GetExclusionsAsync(CancellationToken ct)
    {
        var exclusions = new List<ExclusionInfo>();
        try
        {
            var output = await RunPowerShellOutputAsync(
                "Get-MpPreference | Select-Object ExclusionPath,ExclusionExtension,ExclusionProcess | ConvertTo-Json", ct);

            if (string.IsNullOrWhiteSpace(output)) return exclusions;

            var doc = System.Text.Json.JsonDocument.Parse(output);
            var root = doc.RootElement;

            AddExclusions(root, "ExclusionPath", "Path", exclusions);
            AddExclusions(root, "ExclusionExtension", "Extension", exclusions);
            AddExclusions(root, "ExclusionProcess", "Process", exclusions);
        }
        catch { }
        return exclusions;
    }

    private static void AddExclusions(System.Text.Json.JsonElement root, string prop, string type, List<ExclusionInfo> list)
    {
        if (!root.TryGetProperty(prop, out var arr)) return;
        if (arr.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
            foreach (var item in arr.EnumerateArray())
            {
                var val = item.GetString();
                if (!string.IsNullOrWhiteSpace(val))
                    list.Add(new ExclusionInfo { Type = type, Value = val });
            }
        }
        else if (arr.ValueKind == System.Text.Json.JsonValueKind.String)
        {
            var val = arr.GetString();
            if (!string.IsNullOrWhiteSpace(val))
                list.Add(new ExclusionInfo { Type = type, Value = val });
        }
    }

    // ── PowerShell execution ──

    private static async Task<bool> RunPowerShellAsync(string command, CancellationToken ct)
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
            if (proc is null) return false;

            await proc.WaitForExitAsync(ct);
            return proc.ExitCode == 0;
        }
        catch { return false; }
    }

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

    private static bool GetBool(System.Text.Json.JsonElement el, string prop)
    {
        if (!el.TryGetProperty(prop, out var v)) return false;
        return v.ValueKind == System.Text.Json.JsonValueKind.True ||
               (v.ValueKind == System.Text.Json.JsonValueKind.Number && v.GetInt32() != 0);
    }

    private static string GetString(System.Text.Json.JsonElement el, string prop)
    {
        if (!el.TryGetProperty(prop, out var v)) return "";
        return v.GetString() ?? "";
    }

    private static int GetInt(System.Text.Json.JsonElement el, string prop)
    {
        if (!el.TryGetProperty(prop, out var v)) return 0;
        if (v.ValueKind == System.Text.Json.JsonValueKind.Number) return v.GetInt32();
        return 0;
    }

    private static long GetLong(System.Text.Json.JsonElement el, string prop)
    {
        if (!el.TryGetProperty(prop, out var v)) return 0;
        if (v.ValueKind == System.Text.Json.JsonValueKind.Number) return v.GetInt64();
        return 0;
    }
}

public static class DefenderManagerRegistration
{
    public static IServiceCollection AddDefenderManagerModule(
        this IServiceCollection services)
        => services.AddSingleton<IOptimizationModule, DefenderManagerModule>();
}
