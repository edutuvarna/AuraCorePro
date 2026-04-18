using System.Collections.Generic;

namespace AuraCore.UI.Avalonia.Helpers;

/// <summary>
/// Central whitelist of module IDs valid as auracore://module/&lt;id&gt; deep-link targets.
/// Phase 6.1.A — consumed by UrlSchemeHandler.Parse as knownModuleIds.
/// Add a new entry here when adding a new module that should be deep-linkable.
/// IDs must match the sidebar's SidebarViewModel module IDs exactly.
/// </summary>
public static class ModuleIdsRegistry
{
    public static readonly IReadOnlyCollection<string> All = new HashSet<string>(System.StringComparer.Ordinal)
    {
        // AI Features
        "ai-features",

        // Optimize category
        "ram-optimizer",
        "startup-optimizer",
        "network-optimizer",
        "battery-optimizer",
        "storage-compression",
        "systemd-manager",
        "swap-optimizer",

        // Clean/Debloat category
        "junk-cleaner",
        "disk-cleanup",
        "privacy-cleaner",
        "registry-cleaner",
        "bloatware-removal",
        "package-cleaner",
        "journal-cleaner",
        "snap-flatpak-cleaner",
        "kernel-cleaner",
        "purgeable-space-manager",
        "xcode-cleaner",

        // Gaming category
        "gaming-mode",

        // Security category
        "defender-manager",
        "firewall-rules",
        "file-shredder",
        "hosts-editor",
        "timemachine-manager",

        // Apps/Tools category
        "app-installer",
        "driver-updater",
        "service-manager",
        "iso-builder",
        "space-analyzer",
        "system-health",
        "linux-app-installer",
        "defaults-optimizer",
        "brew-manager",
        "dns-flusher",
        "mac-app-installer",

        // Advanced items
        "registry-deep",
        "environment-variables",
        "symlink-manager",
        "process-monitor",
        "context-menu",
        "taskbar-tweaks",
        "explorer-tweaks",
        "autorun-manager",
        "wake-on-lan",
        "admin-panel",
        "cron-manager",
        "docker-cleaner",
        "grub-manager",
        "launchagent-manager",
        "spotlight-manager",
    };
}
