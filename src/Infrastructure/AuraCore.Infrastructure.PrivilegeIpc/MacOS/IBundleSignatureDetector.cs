namespace AuraCore.Infrastructure.PrivilegeIpc.MacOS;

/// <summary>
/// Abstraction over native bundle code-signature inspection.
/// Allows orchestrator logic to be fully tested without macOS infrastructure.
/// </summary>
public interface IBundleSignatureDetector
{
    /// <summary>
    /// Returns true if the current .app bundle has a valid code signature
    /// with a non-placeholder team ID. Returns false on dev/ad-hoc/unsigned
    /// builds. Always returns false on non-macOS hosts.
    /// </summary>
    bool IsBundleProperlySignedWithTeamId();
}
