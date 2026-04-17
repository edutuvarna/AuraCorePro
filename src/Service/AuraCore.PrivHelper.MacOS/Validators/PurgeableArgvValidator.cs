using System.Text.RegularExpressions;

namespace AuraCore.PrivHelper.MacOS.Validators;

/// <summary>
/// Validates argv for the "purgeable" action ID.
/// Hard-coded executable: /usr/bin/tmutil
///
/// Required argv format (exactly 4 elements):
///   [0] = "thinlocalsnapshots"
///   [1] = "/"  (root mount point only — arbitrary mount points rejected)
///   [2] = ^\d{1,10}$  (bytes to purge; 1-10 digit cap)
///   [3] = ^[1-4]$     (urgency level 1-4)
///
/// Length != 4, any shell metacharacter, or pattern mismatch → reject.
/// </summary>
internal sealed class PurgeableArgvValidator : IArgvValidator
{
    private const string Executable = "/usr/bin/tmutil";

    // 1-10 digits, no more (prevents ridiculously large numbers / overflow)
    private static readonly Regex BytesPattern =
        new(@"^\d{1,10}$", RegexOptions.Compiled);

    // Urgency level: exactly one digit 1-4
    private static readonly Regex UrgencyPattern =
        new(@"^[1-4]$", RegexOptions.Compiled);

    public ActionResolution Validate(string[] args)
    {
        if (args.Length != 4)
            return ActionResolution.Reject("purgeable: exactly 4 arguments required [thinlocalsnapshots, /, bytes, urgency]");

        foreach (var arg in args)
        {
            if (ShellMeta.ContainsMetacharacters(arg))
                return ActionResolution.Reject("purgeable: arg contains shell metacharacters");
        }

        if (args[0] != "thinlocalsnapshots")
            return ActionResolution.Reject("purgeable: first arg must be 'thinlocalsnapshots'");

        if (args[1] != "/")
            return ActionResolution.Reject("purgeable: second arg must be '/' (root mount only)");

        if (!BytesPattern.IsMatch(args[2]))
            return ActionResolution.Reject("purgeable: third arg must be 1-10 digits (bytes to purge)");

        if (!UrgencyPattern.IsMatch(args[3]))
            return ActionResolution.Reject("purgeable: fourth arg must be urgency level 1-4");

        return new ActionResolution(Executable, args);
    }
}
