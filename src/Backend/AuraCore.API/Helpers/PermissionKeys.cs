namespace AuraCore.API.Helpers;

/// <summary>
/// Hardcoded permission key namespace (spec D1). Adding a new key requires
/// backend code change, migration, and frontend label addition. Keys are
/// format "tab:&lt;name&gt;" (Tier 1 — gates whole tab/feature area) or
/// "action:&lt;area&gt;.&lt;verb&gt;" (Tier 2 — gates a single destructive operation).
/// </summary>
public static class PermissionKeys
{
    // Tier 1 — tab-level gating
    public const string TabConfiguration = "tab:configuration";
    public const string TabIpWhitelist   = "tab:ipwhitelist";
    public const string TabUpdates       = "tab:updates";
    public const string TabRoleChange    = "tab:rolechange";

    // Tier 2 — action-level gating
    public const string ActionUsersDelete             = "action:users.delete";
    public const string ActionUsersBan                = "action:users.ban";
    public const string ActionSubscriptionsGrant      = "action:subscriptions.grant";
    public const string ActionSubscriptionsRevoke     = "action:subscriptions.revoke";
    public const string ActionPaymentsApproveCrypto   = "action:payments.approveCrypto";
    public const string ActionPaymentsRejectCrypto    = "action:payments.rejectCrypto";

    public static readonly IReadOnlyList<string> AllTier1 = new[]
    {
        TabConfiguration, TabIpWhitelist, TabUpdates, TabRoleChange,
    };

    public static readonly IReadOnlyList<string> AllTier2 = new[]
    {
        ActionUsersDelete, ActionUsersBan,
        ActionSubscriptionsGrant, ActionSubscriptionsRevoke,
        ActionPaymentsApproveCrypto, ActionPaymentsRejectCrypto,
    };

    public static readonly IReadOnlyList<string> AllKeys =
        AllTier1.Concat(AllTier2).ToArray();

    public static bool IsTabKey(string key) => key?.StartsWith("tab:") == true;
    public static bool IsActionKey(string key) => key?.StartsWith("action:") == true;
    public static bool IsValidKey(string key) => AllKeys.Contains(key);
}
