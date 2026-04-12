namespace AuraCore.Module.LaunchAgentManager.Models;

public enum LaunchAgentLocation
{
    UserAgent,       // ~/Library/LaunchAgents/
    SystemUserAgent, // /Library/LaunchAgents/
    SystemDaemon,    // /Library/LaunchDaemons/
    AppleAgent       // /System/Library/LaunchAgents/ (read-only)
}

public sealed record LaunchAgentInfo(
    string Label,
    string PlistPath,
    LaunchAgentLocation Location,
    bool IsLoaded,
    bool IsBloatware,
    string? Recommendation);
