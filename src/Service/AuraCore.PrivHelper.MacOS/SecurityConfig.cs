namespace AuraCore.PrivHelper.MacOS;

/// <summary>
/// Compile-time constants for peer identity verification.
/// <para>
/// <see cref="ExpectedTeamId"/> is a placeholder substituted at signing time by the
/// build pipeline (<c>sed -i '' "s/AURACORE_TEAM_ID_PLACEHOLDER/$TEAM_ID/g" ...</c>).
/// In DEBUG builds <see cref="PeerVerifier"/> treats the placeholder as a soft-fail
/// (log warning, continue); in RELEASE it hard-fails every XPC call until the
/// placeholder is replaced.
/// </para>
/// </summary>
internal static class SecurityConfig
{
    /// <summary>
    /// Expected CFBundle identifier of the main app XPC client.
    /// Must match the app's <c>CFBundleIdentifier</c> in its Info.plist.
    /// </summary>
    internal const string ExpectedBundleId = "pro.auracore.auracorepro";

    /// <summary>
    /// Expected Apple Developer Team ID embedded in the app code-signature.
    /// Value is substituted by the signing pipeline; the literal placeholder
    /// triggers soft-fail in DEBUG, hard-fail in RELEASE.
    /// </summary>
    internal const string ExpectedTeamId = "AURACORE_TEAM_ID_PLACEHOLDER";

    /// <summary>Minimum PID considered valid for an XPC peer. PID 0 is the kernel.</summary>
    internal const int MinValidPid = 1;

    /// <summary>
    /// Returns <c>true</c> when the team ID has NOT been substituted by the
    /// signing pipeline (i.e. the binary is an unsigned dev build).
    /// </summary>
    internal static bool IsTeamIdPlaceholder(string teamId) =>
        string.Equals(teamId, ExpectedTeamId, StringComparison.Ordinal);
}
