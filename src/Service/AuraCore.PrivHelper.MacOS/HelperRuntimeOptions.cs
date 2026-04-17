namespace AuraCore.PrivHelper.MacOS;

/// <summary>
/// Runtime constants used by both the daemon process and tests.
/// Matches the Linux parallel at <see cref="AuraCore.PrivHelper.Linux.HelperRuntimeOptions"/>.
/// </summary>
public static class HelperRuntimeOptions
{
    /// <summary>Mach service name registered with launchd.
    /// Must match the <c>MachServices</c> dict key in the LaunchDaemons plist (Task 28).</summary>
    public const string MachServiceName = "pro.auracore.PrivHelper";

    /// <summary>Bundle identifier of the helper. Used by SMAppService registration (Task 30)
    /// and enforced as a peer-identity check in the XPC handler (Task 26).</summary>
    public const string BundleIdentifier = "pro.auracore.PrivHelper";

    /// <summary>
    /// Seconds of idle (no XPC activity) before the daemon self-exits.
    /// Matches Linux behavior per spec §3.2 D2.
    /// </summary>
    public const int IdleExitTimeoutSeconds = 300;

    /// <summary>
    /// Minimum macOS major version that supports <c>SMAppService.daemon(plistName:)</c>.
    /// Ventura (macOS 13) introduced the modern privileged-helper registration API;
    /// earlier versions would require the deprecated <c>SMJobBless</c> which is
    /// out-of-scope for Phase 5.2.2.
    /// </summary>
    public const int MinimumMacOSVersion = 13;

    /// <summary>
    /// Reverse-DNS namespace root for action IDs accepted by the whitelist.
    /// Task 27 validators use this to prefix-match daemon-side action ids
    /// (defense against cross-project action name collisions).
    /// </summary>
    public const string ActionIdNamespace = "pro.auracore.privhelper";

    /// <summary>
    /// Maximum allowed <c>timeout_seconds</c> value accepted from XPC clients.
    /// Requests with larger values are silently clamped to this ceiling
    /// (prevents malicious clients from requesting indefinite blocking).
    /// </summary>
    internal const long MaxAllowedTimeoutSeconds = 3600L;
}
