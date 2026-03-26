namespace AuraCore.Domain.Enums;

public enum SubscriptionTier
{
    Free,
    Pro,
    Enterprise,
    Admin
}

public static class TierFeatures
{
    public static readonly Dictionary<string, SubscriptionTier> ModuleRequirements = new()
    {
        // Free tier
        ["system-health"] = SubscriptionTier.Free,
        ["junk-cleaner"] = SubscriptionTier.Free,
        ["explorer-tweaks"] = SubscriptionTier.Free,
        ["taskbar-tweaks"] = SubscriptionTier.Free,
        ["ram-optimizer"] = SubscriptionTier.Free,
        ["network-optimizer"] = SubscriptionTier.Free,
        ["gaming-mode"] = SubscriptionTier.Free,
        ["app-installer"] = SubscriptionTier.Free,
        ["defender-manager"] = SubscriptionTier.Free,

        // Pro tier
        ["storage-compression"] = SubscriptionTier.Pro,
        ["registry-optimizer"] = SubscriptionTier.Pro,
        ["bloatware-removal"] = SubscriptionTier.Pro,
        ["context-menu"] = SubscriptionTier.Pro,
        ["disk-cleanup"] = SubscriptionTier.Pro,
        ["privacy-cleaner"] = SubscriptionTier.Pro,
        ["iso-builder"] = SubscriptionTier.Pro,
    };

    public static readonly Dictionary<SubscriptionTier, decimal> MonthlyPrices = new()
    {
        [SubscriptionTier.Free] = 0m,
        [SubscriptionTier.Pro] = 9.99m,
        [SubscriptionTier.Enterprise] = 19.99m,
    };

    public static readonly Dictionary<SubscriptionTier, decimal> YearlyPrices = new()
    {
        [SubscriptionTier.Free] = 0m,
        [SubscriptionTier.Pro] = 79.99m,
        [SubscriptionTier.Enterprise] = 149.99m,
    };

    public static bool IsModuleAllowed(string moduleId, SubscriptionTier userTier)
    {
        if (userTier == SubscriptionTier.Admin) return true;
        if (!ModuleRequirements.TryGetValue(moduleId, out var required)) return true;
        return userTier >= required;
    }
}
