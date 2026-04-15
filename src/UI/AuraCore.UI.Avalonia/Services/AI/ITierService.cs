namespace AuraCore.UI.Avalonia.Services.AI;

/// <summary>
/// User license tier. Determines which modules are unlocked.
/// </summary>
public enum UserTier
{
    Free,
    Pro,
    Enterprise,
    Admin
}

/// <summary>
/// Determines whether a module is accessible to a given user tier.
/// Phase 3: conservative minimal mapping (admin-panel requires Admin).
/// Future: load mapping from license / server.
/// </summary>
public interface ITierService
{
    bool IsModuleLocked(string moduleKey, UserTier userTier);
    UserTier GetRequiredTier(string moduleKey);
}
