using System.Text.RegularExpressions;
using AuraCore.PrivHelper.Linux.Validators;

namespace AuraCore.PrivHelper.Linux;

/// <summary>
/// Outcome of a whitelist dispatch. Executable is hard-coded by the daemon;
/// the client's hint (PrivilegedCommand.Executable) is intentionally ignored per spec §9.
/// </summary>
public sealed record ActionResolution(string Executable, string[] Argv)
{
    public static ActionResolution Reject(string reason) =>
        new("__REJECTED__", new[] { reason });

    public bool IsRejected => Executable == "__REJECTED__";

    public string RejectionReason => IsRejected ? Argv[0] : string.Empty;
}

/// <summary>
/// Argv validator contract. Each implementation is responsible for one action ID.
/// The daemon calls Validate() after routing via ActionWhitelist.Dispatch().
/// </summary>
public interface IArgvValidator
{
    /// <summary>
    /// Validates incoming argv, returns the authoritative executable + argv
    /// the daemon should spawn. Executable is HARD-CODED per action — client's
    /// hint is ignored (spec §9).
    /// </summary>
    ActionResolution Validate(string[] args);
}

/// <summary>
/// Global shell-metacharacter detector. Called by every validator on every arg
/// before applying domain-specific pattern checks.
/// </summary>
internal static class ShellMeta
{
    // Any of these characters in an arg → automatic reject.
    private static readonly Regex MetaPattern = new(
        @"[;|&<>$`()\{\}\[\]!\\'""]|\.\.",
        RegexOptions.Compiled);

    // Spaces anywhere in an arg are also forbidden (each token must be a single word).
    private static readonly Regex SpacePattern = new(@"\s", RegexOptions.Compiled);

    public static bool ContainsMetacharacters(string s) =>
        MetaPattern.IsMatch(s) || SpacePattern.IsMatch(s);
}

/// <summary>
/// Routes incoming D-Bus RunActionAsync calls to the appropriate per-module
/// argv validator. Unknown action IDs are immediately rejected.
/// </summary>
public sealed class ActionWhitelist
{
    private readonly Dictionary<string, IArgvValidator> _validators;

    public ActionWhitelist()
    {
        _validators = new Dictionary<string, IArgvValidator>(StringComparer.Ordinal)
        {
            ["journal"]        = new JournalArgvValidator(),
            ["snap-flatpak"]   = new SnapFlatpakArgvValidator(),
            ["docker"]         = new DockerArgvValidator(),
            ["kernel"]         = new KernelArgvValidator(),
            ["app-installer"]  = new AppInstallerArgvValidator(),
            ["grub"]           = new GrubArgvValidator(),
        };
    }

    /// <summary>All action IDs currently registered in the whitelist.</summary>
    public IReadOnlyCollection<string> RegisteredActionIds =>
        _validators.Keys.ToList().AsReadOnly();

    /// <summary>Returns true if the given action ID has a registered handler.</summary>
    public bool IsRegistered(string actionId) =>
        !string.IsNullOrEmpty(actionId) && _validators.ContainsKey(actionId);

    /// <summary>
    /// Dispatches an incoming action call through the whitelist.
    /// Returns a rejected resolution if the action ID is unknown or if the
    /// validator rejects the arguments.
    /// </summary>
    public ActionResolution Dispatch(string actionId, string[] args)
    {
        if (!_validators.TryGetValue(actionId, out var validator))
            return ActionResolution.Reject($"unknown action: '{actionId}'");

        return validator.Validate(args);
    }
}
