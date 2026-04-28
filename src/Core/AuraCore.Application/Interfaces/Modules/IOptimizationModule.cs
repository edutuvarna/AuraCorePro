using AuraCore.Domain.Enums;

namespace AuraCore.Application.Interfaces.Modules;

public interface IOptimizationModule
{
    string Id { get; }
    string DisplayName { get; }
    OptimizationCategory Category { get; }
    RiskLevel Risk { get; }

    /// <summary>
    /// Which platform(s) this module supports.
    /// Default: Windows (all existing modules are Windows-only).
    /// Override to SupportedPlatform.All for cross-platform modules (e.g. HostsEditor).
    /// </summary>
    SupportedPlatform Platform => SupportedPlatform.Windows;

    /// <summary>
    /// Whether the module should be hidden from default UI views.
    /// Advanced modules are shown only in the "Advanced" category/filter.
    /// Default: false (module is visible).
    /// </summary>
    bool IsAdvanced => false;

    /// <summary>
    /// Phase 6.16: fast sync platform check derived from Platform enum.
    /// Used by SidebarViewModel.VisibleCategories() — no async overhead during sidebar render.
    /// </summary>
    bool IsPlatformSupported => Platform switch
    {
        SupportedPlatform.Windows => OperatingSystem.IsWindows(),
        SupportedPlatform.Linux   => OperatingSystem.IsLinux(),
        SupportedPlatform.MacOS   => OperatingSystem.IsMacOS(),
        SupportedPlatform.All     => true,
        _                         => true,
    };

    /// <summary>
    /// Phase 6.16: slow async runtime check. Returns rich result.
    /// Used by NavigationService BEFORE rendering view to surface
    /// helper-not-running, tool-not-installed, etc. as a graceful UnavailableModuleView.
    /// Default: Available on all supported platforms; modules opt in by overriding.
    /// </summary>
    Task<ModuleAvailability> CheckRuntimeAvailabilityAsync(CancellationToken ct = default)
        => Task.FromResult(IsPlatformSupported
            ? ModuleAvailability.Available
            : ModuleAvailability.WrongPlatform(Platform));

    Task<ScanResult> ScanAsync(ScanOptions options, CancellationToken ct = default);
    Task<OptimizationResult> OptimizeAsync(
        OptimizationPlan plan,
        IProgress<TaskProgress>? progress = null,
        CancellationToken ct = default);
    Task<bool> CanRollbackAsync(string operationId, CancellationToken ct = default);
    Task RollbackAsync(string operationId, CancellationToken ct = default);
}
