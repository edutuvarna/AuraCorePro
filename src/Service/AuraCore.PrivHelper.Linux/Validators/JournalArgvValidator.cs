using System.Text.RegularExpressions;

namespace AuraCore.PrivHelper.Linux.Validators;

/// <summary>
/// Validates argv for the "journal" action ID.
/// Hard-coded executable: /usr/bin/journalctl
///
/// Allowed patterns (all args must match one):
///   --vacuum-size=NNN[KMG]
///   --vacuum-time=NNN[smhd]
///   --vacuum-files=NNN
///   --rotate
///   --flush
///
/// Any other flag, any shell metacharacter, or empty argv → reject.
/// </summary>
internal sealed class JournalArgvValidator : IArgvValidator
{
    private const string Executable = "/usr/bin/journalctl";

    private static readonly Regex[] AllowedPatterns =
    {
        new(@"^--vacuum-size=\d+[KMG]$",  RegexOptions.Compiled),
        new(@"^--vacuum-time=\d+[smhd]$", RegexOptions.Compiled),
        new(@"^--vacuum-files=\d+$",      RegexOptions.Compiled),
        new(@"^--rotate$",                RegexOptions.Compiled),
        new(@"^--flush$",                 RegexOptions.Compiled),
    };

    public ActionResolution Validate(string[] args)
    {
        if (args.Length == 0)
            return ActionResolution.Reject("journal: argv required");

        foreach (var arg in args)
        {
            if (ShellMeta.ContainsMetacharacters(arg))
                return ActionResolution.Reject($"journal: arg contains shell metacharacters");

            if (!AllowedPatterns.Any(p => p.IsMatch(arg)))
                return ActionResolution.Reject($"journal: arg not in allowlist");
        }

        return new ActionResolution(Executable, args);
    }
}
