using AuraCore.PrivHelper.MacOS.Validators;

namespace AuraCore.PrivHelper.MacOS;

// ---------------------------------------------------------------------------
// ActionResolution — outcome of a whitelist dispatch
// ---------------------------------------------------------------------------

/// <summary>
/// Outcome of a whitelist dispatch. Executable is hard-coded by the daemon;
/// the client's hint is intentionally ignored per spec §3.9.
/// </summary>
internal sealed record ActionResolution(string Executable, string[] Argv)
{
    public static ActionResolution Reject(string reason) =>
        new("__REJECTED__", new[] { reason });

    public bool IsRejected => Executable == "__REJECTED__";

    public string RejectionReason => IsRejected ? Argv[0] : string.Empty;
}

// ---------------------------------------------------------------------------
// IArgvValidator — per-action validator contract
// ---------------------------------------------------------------------------

/// <summary>
/// Argv validator contract. Each implementation is responsible for exactly
/// one action ID. The daemon calls <see cref="Validate"/> after routing via
/// <see cref="ActionWhitelist.Dispatch"/>.
/// </summary>
internal interface IArgvValidator
{
    /// <summary>
    /// Validates the incoming argv, returning the authoritative executable +
    /// argv the daemon should spawn. Executable is HARD-CODED per action —
    /// client's hint is ignored (spec §3.9).
    /// </summary>
    ActionResolution Validate(string[] args);
}

// ---------------------------------------------------------------------------
// ActionWhitelist — dispatcher
// ---------------------------------------------------------------------------

/// <summary>
/// Routes incoming XPC RunAction calls to the appropriate per-module argv
/// validator. Unknown action IDs are immediately rejected.
/// </summary>
internal sealed class ActionWhitelist
{
    private readonly Dictionary<string, IArgvValidator> _validators;

    public ActionWhitelist()
    {
        _validators = new Dictionary<string, IArgvValidator>(StringComparer.Ordinal)
        {
            ["dns-flush"]    = new DnsFlushArgvValidator(),
            ["purgeable"]    = new PurgeableArgvValidator(),
            ["spotlight"]    = new SpotlightArgvValidator(),
            ["time-machine"] = new TimeMachineArgvValidator(),
        };
    }

    /// <summary>All action IDs currently registered in the whitelist.</summary>
    public IReadOnlyCollection<string> RegisteredActionIds =>
        _validators.Keys.ToList().AsReadOnly();

    /// <summary>Returns <c>true</c> if the given action ID has a registered handler.</summary>
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
            return ActionResolution.Reject($"unknown action '{actionId}'");

        return validator.Validate(args);
    }
}
