using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using AuraCore.UI.Avalonia.Services.AI;

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
    string Platform = "all",
    bool IsLocked = false
);

/// <summary>
/// Sidebar state + navigation. Categories are accordion-style: only one expanded
/// at a time. Active module auto-expands its owner category.
/// </summary>
public sealed class SidebarViewModel : INotifyPropertyChanged
{
    private readonly ITierService _tierService;
    private readonly UserTier _currentTier;

    private string? _expandedCategoryId;
    private string _activeModuleId = "dashboard";
    private bool _advancedExpanded = true;

    public event PropertyChangedEventHandler? PropertyChanged;

    public IReadOnlyList<SidebarCategoryVM> Categories { get; }
    public IReadOnlyList<SidebarModuleVM> AdvancedItems { get; }

    /// <summary>
    /// Primary constructor. tierService and currentTier default to a Free-tier TierService
    /// so existing callers (tests, design time) that do <c>new SidebarViewModel()</c> continue to work.
    /// </summary>
    public SidebarViewModel(ITierService? tierService = null, UserTier currentTier = UserTier.Free)
    {
        _tierService = tierService ?? new TierService();
        _currentTier = currentTier;
        Categories = BuildCategories();
        AdvancedItems = BuildAdvancedItems();
    }

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

    // ─── helper: build a module and compute its lock state ───────────

    private SidebarModuleVM Module(string id, string locKey, string platform = "all")
        => new(id, locKey, platform, IsLocked: _tierService.IsModuleLocked(id, _currentTier));

    // ─── Categories ──────────────────────────────────────────────────

    private IReadOnlyList<SidebarCategoryVM> BuildCategories()
    {
        var list = new List<SidebarCategoryVM>
        {
            new("optimize", "nav.categoryOptimize", "IconZap", BuildOptimize()),
            new("clean-debloat", "nav.categoryCleanDebloat", "IconSparkles", BuildCleanDebloat()),
            new("gaming", "nav.categoryGaming", "IconGamepad", BuildGaming()),
            new("security", "nav.categorySecurity", "IconShield", BuildSecurity()),
            new("apps-tools", "nav.categoryAppsTools", "IconPackage", BuildAppsTools()),
            new("ai-features", "nav.categoryAiFeatures", "IconSparklesFilled",
                new SidebarModuleVM[] { Module("ai-features", "nav.categoryAiFeatures") },
                IsAccent: true, Badge: "CORTEX"),
        };
        return list;
    }

    private IReadOnlyList<SidebarModuleVM> BuildOptimize()
    {
        var items = new List<SidebarModuleVM>
        {
            Module("ram-optimizer",       "nav.ramOptimizer"),
            Module("startup-optimizer",   "nav.startupOptimizer"),
            Module("network-optimizer",   "nav.network"),
            Module("battery-optimizer",   "nav.batteryOptimizer"),
            Module("storage-compression", "nav.storage", "windows"),
        };

        if (OperatingSystem.IsLinux())
        {
            items.Add(Module("systemd-manager", "nav.systemdManager", "linux"));
            items.Add(Module("swap-optimizer",  "nav.swapOptimizer",  "linux"));
        }

        return items;
    }

    private IReadOnlyList<SidebarModuleVM> BuildCleanDebloat()
    {
        var items = new List<SidebarModuleVM>
        {
            Module("junk-cleaner",      "nav.junkCleaner"),
            Module("disk-cleanup",      "nav.diskCleanup"),
            Module("privacy-cleaner",   "nav.privacyCleaner"),
            Module("registry-cleaner",  "nav.registry",    "windows"),
            Module("bloatware-removal", "nav.bloatware",   "windows"),
        };

        if (OperatingSystem.IsLinux())
        {
            items.Add(Module("package-cleaner", "nav.packageCleaner", "linux"));
            items.Add(Module("journal-cleaner", "nav.journalCleaner", "linux"));
            items.Add(Module("snap-flatpak-cleaner", "nav.snapFlatpakCleaner", "linux"));
            items.Add(Module("kernel-cleaner", "nav.kernelCleaner", "linux"));
        }

        if (OperatingSystem.IsMacOS())
        {
            items.Add(Module("purgeable-space-manager", "nav.purgeableSpace", "macos"));
            items.Add(Module("xcode-cleaner", "nav.xcodeCleaner", "macos"));
        }

        return items;
    }

    private IReadOnlyList<SidebarModuleVM> BuildGaming() => new[]
    {
        Module("gaming-mode", "nav.gamingMode", "windows"),
    };

    private IReadOnlyList<SidebarModuleVM> BuildSecurity()
    {
        var items = new List<SidebarModuleVM>
        {
            Module("defender-manager", "nav.defender",     "windows"),
            Module("firewall-rules",   "nav.firewallRules","windows"),
            Module("file-shredder",    "nav.fileShredder"),
            Module("hosts-editor",     "nav.hostsEditor"),
        };

        if (OperatingSystem.IsMacOS())
            items.Add(Module("timemachine-manager", "nav.timeMachineManager", "macos"));

        return items;
    }

    private IReadOnlyList<SidebarModuleVM> BuildAppsTools()
    {
        var items = new List<SidebarModuleVM>
        {
            Module("app-installer",   "nav.appInstaller",  "windows"),
            Module("driver-updater",  "nav.driverUpdater", "windows"),
            Module("service-manager", "nav.serviceManager","windows"),
            Module("iso-builder",     "nav.isoBuilder",    "windows"),
            Module("disk-health",     "nav.diskHealth"),
            Module("space-analyzer",  "nav.spaceAnalyzer"),
            Module("system-health",   "nav.systemHealth"),
        };

        if (OperatingSystem.IsLinux())
            items.Add(Module("linux-app-installer", "nav.linuxAppInstaller", "linux"));

        if (OperatingSystem.IsMacOS())
        {
            items.Add(Module("defaults-optimizer",  "nav.defaultsOptimizer",  "macos"));
            items.Add(Module("brew-manager",         "nav.brewManager",         "macos"));
            items.Add(Module("dns-flusher",          "nav.dnsFlusher",          "macos"));
            items.Add(Module("mac-app-installer",    "nav.macAppInstaller",     "macos"));
        }

        return items;
    }

    // ─── Advanced items (flat list below divider) ─────────────────────

    private IReadOnlyList<SidebarModuleVM> BuildAdvancedItems()
    {
        var items = new List<SidebarModuleVM>
        {
            Module("registry-deep",          "nav.registry",              "windows"),
            Module("environment-variables",  "nav.environmentVariables"),
            Module("symlink-manager",        "nav.symlinkManager"),
            Module("process-monitor",        "nav.processMonitor"),
            // Phase 5.1.10: font-manager soft-hidden (sidebar + route only; files kept).
            Module("context-menu",           "nav.contextMenu",           "windows"),
            Module("taskbar-tweaks",         "nav.taskbar",               "windows"),
            Module("explorer-tweaks",        "nav.explorer",              "windows"),
            Module("autorun-manager",        "nav.autorunManager"),
            Module("wake-on-lan",            "nav.wakeOnLan"),
            Module("admin-panel",            "nav.adminPanel"),
        };

        if (OperatingSystem.IsLinux())
        {
            items.Add(Module("cron-manager", "nav.cronManager", "linux"));
            items.Add(Module("docker-cleaner", "nav.dockerCleaner", "linux"));
            items.Add(Module("grub-manager", "nav.grubManager", "linux"));
        }

        if (OperatingSystem.IsMacOS())
        {
            items.Add(Module("launchagent-manager", "nav.launchAgentManager", "macos"));
            items.Add(Module("spotlight-manager", "nav.spotlightManager", "macos"));
        }

        return items;
    }

    // ─── Visible filtering (platform) ────────────────────────────────

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
