namespace AuraCore.Module.SystemdManager.Models;

public sealed record SystemdServiceInfo(
    string Unit,
    string Load,
    string Active,
    string Sub,
    string Description,
    bool IsEnabled,
    bool IsFailed,
    string? Recommendation);
