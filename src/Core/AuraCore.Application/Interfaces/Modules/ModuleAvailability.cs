using AuraCore.Domain.Enums;

namespace AuraCore.Application.Interfaces.Modules;

public enum AvailabilityCategory
{
    Available,
    WrongPlatform,
    HelperNotRunning,
    ToolNotInstalled,
    FeatureDisabled,
    BackendUnreachable,
}

/// <summary>
/// Phase 6.16: rich result type for module runtime availability checks.
/// Used by NavigationService to decide whether to render the module view
/// or a full-page UnavailableModuleView with actionable diagnostic.
/// </summary>
public sealed record ModuleAvailability(
    bool IsAvailable,
    AvailabilityCategory Category,
    string? Reason,
    string? RemediationCommand)
{
    public static ModuleAvailability Available { get; } =
        new(true, AvailabilityCategory.Available, null, null);

    public static ModuleAvailability WrongPlatform(SupportedPlatform supports) =>
        new(false, AvailabilityCategory.WrongPlatform,
            $"This module supports {supports} only.", null);

    public static ModuleAvailability HelperNotRunning(string remediationCommand) =>
        new(false, AvailabilityCategory.HelperNotRunning,
            "Privilege helper (auracore-privhelper) not detected.", remediationCommand);

    public static ModuleAvailability ToolNotInstalled(string toolName, string? remediationCommand) =>
        new(false, AvailabilityCategory.ToolNotInstalled,
            $"Required tool '{toolName}' not found on this system.", remediationCommand);

    public static ModuleAvailability FeatureDisabled(string reason) =>
        new(false, AvailabilityCategory.FeatureDisabled, reason, null);
}
