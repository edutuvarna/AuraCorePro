using System.Text.RegularExpressions;

namespace AuraCore.PrivHelper.MacOS.Validators;

/// <summary>
/// Validates argv for the "time-machine" action ID.
/// Hard-coded executable: /usr/bin/tmutil
///
/// Allowed argv patterns (first arg is verb):
///   ["startbackup"]                         — start backup
///   ["startbackup", "--auto"]               — with auto flag
///   ["startbackup", "--no-auto"]            — with no-auto flag
///   ["stopbackup"]                          — stop current backup
///   ["listbackups"]                         — list local backups
///   ["enable"]                              — enable Time Machine
///   ["disable"]                             — disable Time Machine
///   ["deletelocalsnapshots", date_string]   — delete snapshot by date
///                                             date_string: ^\d{4}-\d{2}-\d{2}-\d{6}$
///
/// Explicitly rejected verbs: restore, setdestination, delete, latestbackup, etc.
/// Shell metacharacter check on every arg.
/// </summary>
internal sealed class TimeMachineArgvValidator : IArgvValidator
{
    private const string Executable = "/usr/bin/tmutil";

    // tmutil local snapshot date format: YYYY-MM-DD-HHMMSS
    private static readonly Regex SnapshotDatePattern =
        new(@"^\d{4}-\d{2}-\d{2}-\d{6}$", RegexOptions.Compiled);

    // Verbs that take no arguments
    private static readonly HashSet<string> ZeroArgVerbs = new(StringComparer.Ordinal)
    {
        "stopbackup",
        "listbackups",
        "enable",
        "disable",
    };

    // Explicitly forbidden verbs (powerful / dangerous)
    private static readonly HashSet<string> ForbiddenVerbs = new(StringComparer.Ordinal)
    {
        "restore",
        "setdestination",
        "delete",
        "latestbackup",
        "removedestination",
        "associatedisk",
    };

    public ActionResolution Validate(string[] args)
    {
        if (args.Length == 0)
            return ActionResolution.Reject("time-machine: verb required");

        // Check metacharacters on ALL args
        foreach (var arg in args)
        {
            if (ShellMeta.ContainsMetacharacters(arg))
                return ActionResolution.Reject("time-machine: arg contains shell metacharacters");
        }

        var verb = args[0];

        // Reject explicitly forbidden verbs before anything else
        if (ForbiddenVerbs.Contains(verb))
            return ActionResolution.Reject($"time-machine: verb '{verb}' is not permitted");

        // startbackup: 1 arg (verb only) or 2 args (verb + --auto | --no-auto)
        if (verb == "startbackup")
        {
            return args.Length switch
            {
                1 => new ActionResolution(Executable, args),
                2 when args[1] is "--auto" or "--no-auto"
                    => new ActionResolution(Executable, args),
                2 => ActionResolution.Reject(
                    $"time-machine: unrecognized startbackup flag '{args[1]}' — allowed: --auto, --no-auto"),
                _ => ActionResolution.Reject("time-machine: startbackup takes 0 or 1 flags")
            };
        }

        // Zero-argument verbs: must have exactly 1 element total
        if (ZeroArgVerbs.Contains(verb))
        {
            if (args.Length != 1)
                return ActionResolution.Reject($"time-machine: '{verb}' takes no arguments");
            return new ActionResolution(Executable, args);
        }

        // deletelocalsnapshots <date>
        if (verb == "deletelocalsnapshots")
        {
            if (args.Length != 2)
                return ActionResolution.Reject(
                    "time-machine: deletelocalsnapshots requires exactly one date argument");

            if (!SnapshotDatePattern.IsMatch(args[1]))
                return ActionResolution.Reject(
                    $"time-machine: invalid snapshot date format '{args[1]}' " +
                    "(expected YYYY-MM-DD-HHMMSS, e.g. 2024-03-15-120000)");

            return new ActionResolution(Executable, args);
        }

        return ActionResolution.Reject($"time-machine: unknown verb '{verb}'");
    }
}
