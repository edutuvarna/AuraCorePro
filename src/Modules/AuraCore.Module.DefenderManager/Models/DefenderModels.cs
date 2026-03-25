namespace AuraCore.Module.DefenderManager.Models;

public sealed class DefenderStatus
{
    public bool RealTimeProtection { get; set; }
    public bool CloudProtection { get; set; }
    public bool TamperProtection { get; set; }
    public bool BehaviorMonitoring { get; set; }
    public bool NetworkProtection { get; set; }
    public bool PotentiallyUnwantedApps { get; set; }
    public bool FirewallDomain { get; set; }
    public bool FirewallPrivate { get; set; }
    public bool FirewallPublic { get; set; }
    public string AntivirusSignatureVersion { get; set; } = "";
    public DateTimeOffset AntivirusSignatureLastUpdated { get; set; }
    public string EngineVersion { get; set; } = "";
    public string ProductVersion { get; set; } = "";
    public bool IsAdmin { get; set; }
    public string? Error { get; set; }

    public int SignatureAgeDays => (int)(DateTimeOffset.Now - AntivirusSignatureLastUpdated).TotalDays;
    public bool SignaturesOutdated => SignatureAgeDays > 3;

    public int EnabledCount
    {
        get
        {
            int c = 0;
            if (RealTimeProtection) c++;
            if (CloudProtection) c++;
            if (TamperProtection) c++;
            if (BehaviorMonitoring) c++;
            if (NetworkProtection) c++;
            if (PotentiallyUnwantedApps) c++;
            return c;
        }
    }

    public string ProtectionLevel => EnabledCount switch
    {
        >= 5 => "Excellent",
        >= 3 => "Good",
        >= 1 => "Partial",
        _ => "At Risk"
    };
}

public sealed class ThreatInfo
{
    public string ThreatName { get; set; } = "";
    public string Severity { get; set; } = "";
    public string Status { get; set; } = "";
    public string Path { get; set; } = "";
    public DateTimeOffset DetectedAt { get; set; }
}

public sealed class ExclusionInfo
{
    public string Type { get; set; } = ""; // Path, Extension, Process
    public string Value { get; set; } = "";
}

public sealed class ScanProgress
{
    public bool IsRunning { get; set; }
    public string ScanType { get; set; } = "";
    public string StatusMessage { get; set; } = "";
}
