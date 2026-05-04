namespace AuraCore.UI.Avalonia.Services.AI;

public sealed class TierService : ITierService
{
    /// <summary>
    /// Module → required tier mapping.
    ///
    /// Phase 6.16 (post-Linux-VM-smoke) revision: every shipped module
    /// is now explicitly mapped. Unlisted modules still default to Free
    /// (the GetRequiredTier fallback) but the goal is to keep the table
    /// exhaustive so adding a new module forces a tier decision.
    ///
    /// Tiering rationale:
    ///   Free  — entry-level optimizers + native Linux/macOS tools that
    ///           are conceptually "the OS package manager / health view".
    ///           Linux + macOS users skew more technical and price-sensitive,
    ///           so the platform-native tools stay free as a hook.
    ///   Pro   — system-modifying / privileged tools, advanced cleanup,
    ///           registry / service / firewall management, deep system
    ///           introspection. The actual value tier.
    ///   Enterprise — reserved for future fleet / multi-device features.
    ///                Today empty (handled at billing level, not module gate).
    ///   Admin — internal-only.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, UserTier> _requiredTiers =
        new Dictionary<string, UserTier>(StringComparer.OrdinalIgnoreCase)
        {
            // ── Admin tier ──
            ["admin-panel"] = UserTier.Admin,

            // ── Free tier — entry-level + native platform tools ──
            ["dashboard"]               = UserTier.Free,
            ["ai-features"]             = UserTier.Free, // limited free tier; advanced models gated in-feature
            ["settings"]                = UserTier.Free,
            ["system-health"]           = UserTier.Free,
            ["junk-cleaner"]            = UserTier.Free,
            ["ram-optimizer"]           = UserTier.Free,
            ["network-optimizer"]       = UserTier.Free,
            ["gaming-mode"]             = UserTier.Free,
            ["hosts-editor"]            = UserTier.Free,
            ["file-shredder"]           = UserTier.Free,
            ["wake-on-lan"]             = UserTier.Free,
            // Linux native
            ["systemd-manager"]         = UserTier.Free,
            ["swap-optimizer"]          = UserTier.Free,
            ["package-cleaner"]         = UserTier.Free,
            ["journal-cleaner"]         = UserTier.Free,
            // macOS native
            ["brew-manager"]            = UserTier.Free,
            ["dns-flusher"]             = UserTier.Free,

            // ── Pro tier — advanced + system-modifying ──
            // Windows-only (was Free in Phase 3 seed; promoted to Pro in 6.16)
            ["app-installer"]           = UserTier.Pro,
            ["defender-manager"]        = UserTier.Pro,
            ["battery-optimizer"]       = UserTier.Pro,
            ["explorer-tweaks"]         = UserTier.Pro,
            ["taskbar-tweaks"]          = UserTier.Pro,
            // Windows-only (already Pro since Phase 3)
            ["storage-compression"]     = UserTier.Pro,
            ["registry-optimizer"]      = UserTier.Pro,
            ["registry-cleaner"]        = UserTier.Pro,
            ["registry-deep"]           = UserTier.Pro,
            ["bloatware-removal"]       = UserTier.Pro,
            ["context-menu"]            = UserTier.Pro,
            ["disk-cleanup"]            = UserTier.Pro,
            ["privacy-cleaner"]         = UserTier.Pro,
            ["iso-builder"]             = UserTier.Pro,
            ["driver-updater"]          = UserTier.Pro,
            // Windows new (Phase 5.5+)
            ["service-manager"]         = UserTier.Pro,
            ["autorun-manager"]         = UserTier.Pro,
            ["firewall-rules"]          = UserTier.Pro,
            ["network-monitor"]         = UserTier.Pro,
            ["dns-benchmark"]           = UserTier.Pro,
            ["font-manager"]            = UserTier.Pro,
            // Cross-platform advanced
            ["environment-variables"]   = UserTier.Pro,
            ["symlink-manager"]         = UserTier.Pro,
            ["process-monitor"]         = UserTier.Pro,
            ["space-analyzer"]          = UserTier.Pro,
            ["disk-health"]             = UserTier.Pro,
            // Linux advanced
            ["snap-flatpak-cleaner"]    = UserTier.Pro,
            ["docker-cleaner"]          = UserTier.Pro,
            ["kernel-cleaner"]          = UserTier.Pro,
            ["grub-manager"]            = UserTier.Pro,
            ["linux-app-installer"]     = UserTier.Pro,
            ["cron-manager"]            = UserTier.Pro,
            // macOS advanced
            ["mac-app-installer"]       = UserTier.Pro,
            ["defaults-optimizer"]      = UserTier.Pro,
            ["launchagent-manager"]     = UserTier.Pro,
            ["timemachine-manager"]     = UserTier.Pro,
            ["xcode-cleaner"]           = UserTier.Pro,
            ["purgeable-space-manager"] = UserTier.Pro,
            ["spotlight-manager"]       = UserTier.Pro,

            // ── Enterprise tier ──
            // (Reserved for future fleet / multi-device features; empty today.
            //  Enterprise license = Pro feature set + multi-device entitlement +
            //  priority support, all handled at billing-row level.)
        };

    public bool IsModuleLocked(string moduleKey, UserTier userTier)
    {
        if (userTier == UserTier.Admin) return false; // admins see everything

        var required = GetRequiredTier(moduleKey);
        return (int)userTier < (int)required;
    }

    public UserTier GetRequiredTier(string moduleKey)
    {
        return _requiredTiers.TryGetValue(moduleKey, out var tier)
            ? tier
            : UserTier.Free;
    }
}
