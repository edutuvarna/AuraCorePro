namespace AuraCore.PrivHelper.MacOS;

/// <summary>
/// Single source of truth for daemon version. Compared by the client
/// over XPC (IPrivHelper.GetVersion) to detect helper drift after
/// main app upgrades and trigger a re-install via SMAppService.
/// </summary>
public static class HelperVersion
{
    public const string Current = "5.2.2";
}
