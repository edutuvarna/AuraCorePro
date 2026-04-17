namespace AuraCore.UI.Avalonia.Services.AI;

public sealed class TierService : ITierService
{
    /// <summary>
    /// Module → required tier mapping. Unlisted modules default to Free (unlocked for everyone).
    /// Carries forward module gating from pre-Phase-2 TierFeatures.ModuleRequirements mapping.
    /// Conservative Phase 3 seed: admin-panel = Admin (UI-specific, not in Domain).
    /// </summary>
    private static readonly IReadOnlyDictionary<string, UserTier> _requiredTiers =
        new Dictionary<string, UserTier>(StringComparer.OrdinalIgnoreCase)
        {
            // Admin tier
            ["admin-panel"] = UserTier.Admin,

            // Free tier (already unlocked for everyone by default)
            ["system-health"] = UserTier.Free,
            ["junk-cleaner"] = UserTier.Free,
            ["explorer-tweaks"] = UserTier.Free,
            ["taskbar-tweaks"] = UserTier.Free,
            ["ram-optimizer"] = UserTier.Free,
            ["network-optimizer"] = UserTier.Free,
            ["gaming-mode"] = UserTier.Free,
            ["app-installer"] = UserTier.Free,
            ["defender-manager"] = UserTier.Free,
            ["battery-optimizer"] = UserTier.Free,

            // Pro tier
            ["storage-compression"] = UserTier.Pro,
            ["registry-optimizer"] = UserTier.Pro,
            ["bloatware-removal"] = UserTier.Pro,
            ["context-menu"] = UserTier.Pro,
            ["disk-cleanup"] = UserTier.Pro,
            ["privacy-cleaner"] = UserTier.Pro,
            ["iso-builder"] = UserTier.Pro,
            ["driver-updater"] = UserTier.Pro,
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
