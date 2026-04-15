using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace AuraCore.UI.Avalonia.ViewModels;

/// <summary>A sidebar category (like "Optimize" or "AI Features") with its modules.</summary>
public sealed record SidebarCategoryVM(
    string Id,
    string LocalizationKey,
    string Icon,
    IReadOnlyList<SidebarModuleVM> Modules,
    bool IsAccent = false,
    string? Badge = null
);

/// <summary>A single navigable module under a category.</summary>
public sealed record SidebarModuleVM(
    string Id,
    string LocalizationKey,
    string Platform = "all"
);

/// <summary>
/// Sidebar state + navigation. Categories are accordion-style: only one expanded
/// at a time. Active module auto-expands its owner category.
/// </summary>
public sealed class SidebarViewModel : INotifyPropertyChanged
{
    private string? _expandedCategoryId;
    private string _activeModuleId = "dashboard";
    private bool _advancedExpanded = true;

    public event PropertyChangedEventHandler? PropertyChanged;

    public IReadOnlyList<SidebarCategoryVM> Categories { get; } = BuildCategories();
    public IReadOnlyList<SidebarModuleVM> AdvancedItems { get; } = BuildAdvancedItems();

    public string? ExpandedCategoryId
    {
        get => _expandedCategoryId;
        private set { if (_expandedCategoryId != value) { _expandedCategoryId = value; OnChanged(); } }
    }

    public string ActiveModuleId
    {
        get => _activeModuleId;
        private set { if (_activeModuleId != value) { _activeModuleId = value; OnChanged(); } }
    }

    public bool AdvancedExpanded
    {
        get => _advancedExpanded;
        set { if (_advancedExpanded != value) { _advancedExpanded = value; OnChanged(); } }
    }

    public void ToggleCategory(string id)
    {
        ExpandedCategoryId = ExpandedCategoryId == id ? null : id;
    }

    public void NavigateTo(string moduleId)
    {
        ActiveModuleId = moduleId;
        var owner = Categories.FirstOrDefault(c => c.Modules.Any(m => m.Id == moduleId));
        if (owner is not null)
            ExpandedCategoryId = owner.Id;
    }

    private void OnChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private static IReadOnlyList<SidebarCategoryVM> BuildCategories() => new[]
    {
        new SidebarCategoryVM("optimize", "nav.categoryOptimize", "IconZap", new SidebarModuleVM[]
        {
            new("ram-optimizer", "nav.ramOptimizer"),
            new("startup-optimizer", "nav.startupOptimizer"),
            new("network-optimizer", "nav.network"),
            new("battery-optimizer", "nav.batteryOptimizer"),
            new("storage-compression", "nav.storage", Platform: "windows"),
        }),
        new SidebarCategoryVM("clean-debloat", "nav.categoryCleanDebloat", "IconSparkles", new SidebarModuleVM[]
        {
            new("junk-cleaner", "nav.junkCleaner"),
            new("disk-cleanup", "nav.diskCleanup"),
            new("privacy-cleaner", "nav.privacyCleaner"),
            new("registry-cleaner", "nav.registry", Platform: "windows"),
            new("bloatware-removal", "nav.bloatware", Platform: "windows"),
            new("app-installer", "nav.appInstaller", Platform: "windows"),
        }),
        new SidebarCategoryVM("gaming", "nav.categoryGaming", "IconGamepad", new SidebarModuleVM[]
        {
            new("gaming-mode", "nav.gamingMode", Platform: "windows"),
        }),
        new SidebarCategoryVM("security", "nav.categorySecurity", "IconShield", new SidebarModuleVM[]
        {
            new("defender-manager", "nav.defender", Platform: "windows"),
            new("firewall-rules", "nav.firewallRules", Platform: "windows"),
            new("file-shredder", "nav.fileShredder"),
            new("hosts-editor", "nav.hostsEditor"),
        }),
        new SidebarCategoryVM("apps-tools", "nav.categoryAppsTools", "IconPackage", new SidebarModuleVM[]
        {
            new("driver-updater", "nav.driverUpdater", Platform: "windows"),
            new("service-manager", "nav.serviceManager", Platform: "windows"),
            new("iso-builder", "nav.isoBuilder", Platform: "windows"),
            new("disk-health", "nav.diskHealth"),
            new("space-analyzer", "nav.spaceAnalyzer"),
        }),
        new SidebarCategoryVM("ai-features", "nav.categoryAiFeatures", "IconSparklesFilled", new SidebarModuleVM[]
        {
            new("ai-features", "nav.categoryAiFeatures"),
        }, IsAccent: true, Badge: "CORTEX"),
    };

    private static IReadOnlyList<SidebarModuleVM> BuildAdvancedItems() => new SidebarModuleVM[]
    {
        new("registry-deep", "nav.registry", Platform: "windows"),
        new("environment-variables", "nav.environmentVariables"),
        new("symlink-manager", "nav.symlinkManager"),
        new("process-monitor", "nav.processMonitor"),
        new("font-manager", "nav.fontManager"),
        new("context-menu", "nav.contextMenu", Platform: "windows"),
        new("taskbar-tweaks", "nav.taskbar", Platform: "windows"),
        new("explorer-tweaks", "nav.explorer", Platform: "windows"),
        new("autorun-manager", "nav.autorunManager"),
        new("wake-on-lan", "nav.wakeOnLan"),
    };

    public IEnumerable<SidebarCategoryVM> VisibleCategories()
    {
        var plat = CurrentPlatform();
        return Categories
            .Select(c => c with { Modules = c.Modules.Where(m => m.Platform == "all" || m.Platform == plat).ToList() })
            .Where(c => c.Modules.Count > 0);
    }

    public IEnumerable<SidebarModuleVM> VisibleAdvancedItems()
    {
        var plat = CurrentPlatform();
        return AdvancedItems.Where(m => m.Platform == "all" || m.Platform == plat);
    }

    private static string CurrentPlatform() =>
        OperatingSystem.IsWindows() ? "windows" :
        OperatingSystem.IsLinux()   ? "linux"   :
        OperatingSystem.IsMacOS()   ? "macos"   : "all";
}
