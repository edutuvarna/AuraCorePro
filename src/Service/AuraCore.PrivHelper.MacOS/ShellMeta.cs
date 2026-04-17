namespace AuraCore.PrivHelper.MacOS;

/// <summary>
/// Shared metacharacter and SIP-protected-path rejection helper.
/// Used by every argv validator in the macOS privilege helper (Task 27, spec §3.10).
/// <para>
/// Two categories of rejection:
/// <list type="bullet">
///   <item><see cref="ContainsMetacharacters"/> — rejects any arg containing shell
///   injection characters or path-traversal sequences.</item>
///   <item><see cref="IsSipProtectedPath"/> — rejects paths targeting
///   SIP-protected system locations. Defense-in-depth: the kernel would block writes
///   anyway, but surfacing the rejection at validation time yields a clean error
///   instead of a kernel EACCES.</item>
/// </list>
/// </para>
/// </summary>
internal static partial class ShellMeta
{
    // Metacharacters that indicate shell injection attempts.
    // Reject ANY arg containing any of these.
    private static readonly char[] BannedMetaCharacters =
    {
        ';', '|', '&', '<', '>', '$', '`', '(', ')', '{', '}', '[', ']',
        '\n', '\r', '\0', '\\'
    };

    // SIP-protected path prefixes (§3.10). Exception: /usr/local/ is allowed.
    private static readonly string[] SipProtectedPrefixes =
    {
        "/System/",
        "/bin/",
        "/sbin/",
        "/usr/bin/",
        "/usr/sbin/",
        "/usr/libexec/",
        "/usr/lib/",
        "/private/var/db/",
        "/.vol/",
        "/.DocumentRevisions-V100/",
    };

    // /usr/local/ is explicitly allowed; don't accidentally match /usr/lib/ etc.
    private static readonly string[] SipAllowlist = { "/usr/local/" };

    /// <summary>
    /// Returns <c>true</c> if <paramref name="s"/> contains any shell
    /// metacharacter or path-traversal sequence.
    /// </summary>
    public static bool ContainsMetacharacters(string s)
    {
        if (string.IsNullOrEmpty(s)) return false;
        foreach (var c in s)
            foreach (var banned in BannedMetaCharacters)
                if (c == banned) return true;
        // Path-traversal check: ".." is suspicious in privileged contexts
        if (s.Contains("..")) return true;
        return false;
    }

    /// <summary>
    /// Returns <c>true</c> if <paramref name="path"/> targets a SIP-protected
    /// system location (§3.10). Arguments with SIP-protected paths are
    /// rejected at the validator layer before dispatching to the daemon.
    /// </summary>
    public static bool IsSipProtectedPath(string path)
    {
        if (string.IsNullOrEmpty(path)) return false;
        // SIP rules apply to absolute paths only; relative paths can't
        // reach system locations without a cd/chroot which validators also reject.
        if (!path.StartsWith('/')) return false;

        // Check allowlist first — /usr/local/* is fine even though /usr/* is protected.
        foreach (var allowed in SipAllowlist)
            if (path.StartsWith(allowed, StringComparison.Ordinal))
                return false;

        foreach (var protect in SipProtectedPrefixes)
            if (path.StartsWith(protect, StringComparison.Ordinal))
                return true;

        return false;
    }
}
