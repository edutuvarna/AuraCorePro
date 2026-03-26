namespace AuraCore.Module.DefenderManager.Models;

public sealed class DefenderStatus
{
    public bool IsAdmin { get; set; }
    public bool RealTimeProtection { get; set; }
    public bool CloudProtection { get; set; }
    public bool BehaviorMonitoring { get; set; }
    public bool TamperProtection { get; set; }
    public bool PotentiallyUnwantedApps { get; set; }
    public bool NetworkProtection { get; set; }
    public bool FirewallDomain { get; set; }
    public bool FirewallPrivate { get; set; }
    public bool FirewallPublic { get; set; }
    public string AntivirusSignatureVersion { get; set; } = "";
    public DateTimeOffset? AntivirusSignatureLastUpdated { get; set; }
    public string EngineVersion { get; set; } = "";
    public string ProductVersion { get; set; } = "";
    public string? Error { get; set; }

    public bool SignaturesOutdated =>
        AntivirusSignatureLastUpdated.HasValue &&
        AntivirusSignatureLastUpdated.Value < DateTimeOffset.UtcNow.AddDays(-3);

    /// <summary>Count of enabled protection features (out of 6)</summary>
    public int EnabledCount
    {
        get
        {
            int count = 0;
            if (RealTimeProtection) count++;
            if (CloudProtection) count++;
            if (BehaviorMonitoring) count++;
            if (PotentiallyUnwantedApps) count++;
            if (NetworkProtection) count++;
            if (TamperProtection) count++;
            return count;
        }
    }

    public string OverallStatus
    {
        get
        {
            if (Error != null) return "Unknown";
            int score = 0;
            if (RealTimeProtection) score++;
            if (CloudProtection) score++;
            if (BehaviorMonitoring) score++;
            if (FirewallDomain && FirewallPrivate && FirewallPublic) score++;
            if (!SignaturesOutdated) score++;
            return score switch
            {
                5 => "Excellent",
                4 => "Good",
                >= 2 => "Partial",
                _ => "At Risk"
            };
        }
    }

    /// <summary>Alias for OverallStatus (used by UI)</summary>
    public string ProtectionLevel => OverallStatus;
}

public sealed class ThreatInfo
{
    public string ThreatName { get; set; } = "";
    public string Severity { get; set; } = "Unknown";
    public string Status { get; set; } = "Unknown";
    public string Path { get; set; } = "";
    public DateTimeOffset DetectedAt { get; set; }
}

public sealed class ExclusionInfo
{
    public string Type { get; set; } = "";
    public string Value { get; set; } = "";
}

// ═══════════════════════════════════════════════════════════
// Scheduled Scan Info (NEW)
// ═══════════════════════════════════════════════════════════
public sealed class ScheduledScanInfo
{
    /// <summary>0=Daily, 1=Sun, 2=Mon, ..., 7=Sat, 8=Never</summary>
    public int ScheduleDay { get; set; } = 8;
    public string ScheduleDayName { get; set; } = "Never";
    public string ScheduleTime { get; set; } = "02:00";
    public TimeSpan ScheduleTimeSpan { get; set; } = TimeSpan.FromHours(2);
    public string QuickScanTime { get; set; } = "";

    /// <summary>"Quick Scan" or "Full Scan"</summary>
    public string ScanType { get; set; } = "Quick Scan";

    public bool CheckSignaturesBeforeScan { get; set; } = true;
    public bool ScanOnlyIfIdle { get; set; } = true;

    /// <summary>CPU load limit during scan (1-100%)</summary>
    public int CpuLoadLimit { get; set; } = 50;

    public string? Error { get; set; }

    public string Summary
    {
        get
        {
            if (ScheduleDay == 8) return "No scheduled scan configured";
            return $"{ScanType} every {ScheduleDayName} at {ScheduleTime} (CPU limit: {CpuLoadLimit}%)";
        }
    }
}

// ═══════════════════════════════════════════════════════════
// Quarantine Item (NEW)
// ═══════════════════════════════════════════════════════════
public sealed class QuarantineItem
{
    public long ThreatId { get; set; }
    public string ThreatName { get; set; } = "";
    public string Severity { get; set; } = "Unknown";
    public bool IsActive { get; set; }
    public bool DidExecute { get; set; }
    public string Action { get; set; } = "";

    /// <summary>"Active", "Quarantined", or "Resolved"</summary>
    public string Status { get; set; } = "Unknown";

    public List<string> Resources { get; set; } = new();
    public DateTimeOffset DetectedAt { get; set; }

    public string ResourceSummary => Resources.Count switch
    {
        0 => "No files",
        1 => Resources[0].Length > 60 ? "..." + Resources[0][^55..] : Resources[0],
        _ => $"{Resources.Count} files affected"
    };
}
