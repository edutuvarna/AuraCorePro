using Tmds.DBus;

namespace AuraCore.Infrastructure.PrivilegeIpc.Linux;

/// <summary>
/// D-Bus contract between AuraCorePro main app (client) and the
/// auracore-privhelper daemon. Served on the system bus at
/// <c>pro.auracore.PrivHelper</c> on object path <c>/pro/auracore/PrivHelper</c>.
/// Interface name includes the <c>1</c> suffix for versioned evolution.
/// </summary>
[DBusInterface("pro.auracore.PrivHelper1")]
public interface IPrivHelper : IDBusObject
{
    /// <summary>
    /// Invokes a whitelisted action on the daemon. The daemon validates
    /// <paramref name="actionId"/> against its per-module whitelist and
    /// applies the argv validator before spawning the underlying process.
    /// </summary>
    /// <param name="actionId">Action id, e.g. "journal", "grub", "docker". Must match daemon whitelist.</param>
    /// <param name="args">Argv passed to the whitelisted executable.</param>
    /// <param name="timeoutSeconds">Per-call timeout for the spawned process.</param>
    Task<PrivHelperResult> RunActionAsync(string actionId, string[] args, int timeoutSeconds);

    /// <summary>
    /// Returns the daemon's version string. Used by the client to detect
    /// helper version drift and trigger a re-install if the main app is
    /// newer than the installed daemon.
    /// </summary>
    Task<string> GetVersionAsync();
}

/// <summary>
/// Result shape returned by <see cref="IPrivHelper.RunActionAsync"/>.
/// Deliberately a struct with public writable fields for Tmds.DBus
/// marshalling compatibility (Tmds.DBus 0.15 prefers fields over
/// init-only properties for complex wire types).
/// </summary>
public struct PrivHelperResult
{
    public int ExitCode;
    public string Stdout;
    public string Stderr;

    /// <summary>
    /// One of: "cached" (auth cached by polkit, no prompt), "prompted"
    /// (user was prompted and approved), "denied" (polkit denied or
    /// user cancelled), "rejected" (action id or argv failed daemon-side
    /// whitelist validation).
    /// </summary>
    public string AuthState;
}
