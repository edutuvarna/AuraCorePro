using System.Text.RegularExpressions;

namespace AuraCore.PrivHelper.Linux.Validators;

/// <summary>
/// Validates argv for the "snap-flatpak" action ID.
///
/// Routing: first arg selects the package manager:
///   "snap"    → /usr/bin/snap,    strips first arg, validates remainder
///   "flatpak" → /usr/bin/flatpak, strips first arg, validates remainder
///
/// Allowed verbs:  remove | uninstall
/// Allowed args:   package name matching ^[a-zA-Z0-9._-]+$  OR  --purge
/// No shell metacharacters in any arg.
/// </summary>
internal sealed class SnapFlatpakArgvValidator : IArgvValidator
{
    private const string SnapExe    = "/usr/bin/snap";
    private const string FlatpakExe = "/usr/bin/flatpak";

    private static readonly HashSet<string> AllowedVerbs =
        new(StringComparer.Ordinal) { "remove", "uninstall" };

    private static readonly Regex PackageName =
        new(@"^[a-zA-Z0-9._-]+$", RegexOptions.Compiled);

    private static readonly HashSet<string> AllowedFlags =
        new(StringComparer.Ordinal) { "--purge" };

    public ActionResolution Validate(string[] args)
    {
        if (args.Length < 2)
            return ActionResolution.Reject("snap-flatpak: at least 2 args required (manager verb ...)");

        // Check all args for shell metacharacters first
        foreach (var arg in args)
        {
            if (ShellMeta.ContainsMetacharacters(arg))
                return ActionResolution.Reject("snap-flatpak: arg contains shell metacharacters");
        }

        var manager = args[0];
        string executable;
        switch (manager)
        {
            case "snap":    executable = SnapExe;    break;
            case "flatpak": executable = FlatpakExe; break;
            default:
                return ActionResolution.Reject($"snap-flatpak: unknown manager '{manager}'; expected snap or flatpak");
        }

        // Args after the manager selector
        var remainder = args.Skip(1).ToArray();
        if (remainder.Length < 1)
            return ActionResolution.Reject("snap-flatpak: verb required after manager");

        var verb = remainder[0];
        if (!AllowedVerbs.Contains(verb))
            return ActionResolution.Reject($"snap-flatpak: verb '{verb}' not allowed; expected remove or uninstall");

        // Validate remaining tokens: each must be a valid package name or an allowed flag
        foreach (var token in remainder.Skip(1))
        {
            if (!PackageName.IsMatch(token) && !AllowedFlags.Contains(token))
                return ActionResolution.Reject($"snap-flatpak: token '{token}' not a valid package name or allowed flag");
        }

        // Return argv without the manager selector (daemon already knows the exe)
        return new ActionResolution(executable, remainder);
    }
}
