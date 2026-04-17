using System.Text.RegularExpressions;

namespace AuraCore.PrivHelper.Linux.Validators;

/// <summary>
/// Validates argv for the "app-installer" action ID.
///
/// Routing: first arg selects the package manager:
///   "apt"  → /usr/bin/apt-get, strips first arg, validates remainder
///   "snap" → /usr/bin/snap,    strips first arg, validates remainder
///
/// Allowed verbs:    install | remove | update
/// Allowed flags:    -y
/// Allowed packages: ^[a-zA-Z0-9_.+-]+$  (no path separators, no ..)
///
/// Path traversal (../../, etc.) is caught by both the ShellMeta check
/// and the strict package-name regex.
/// </summary>
internal sealed class AppInstallerArgvValidator : IArgvValidator
{
    private const string AptExe  = "/usr/bin/apt-get";
    private const string SnapExe = "/usr/bin/snap";

    private static readonly HashSet<string> AllowedVerbs =
        new(StringComparer.Ordinal) { "install", "remove", "update" };

    private static readonly HashSet<string> AllowedFlags =
        new(StringComparer.Ordinal) { "-y" };

    private static readonly Regex PackageName =
        new(@"^[a-zA-Z0-9_.+-]+$", RegexOptions.Compiled);

    public ActionResolution Validate(string[] args)
    {
        if (args.Length < 2)
            return ActionResolution.Reject("app-installer: at least 2 args required (manager verb ...)");

        foreach (var arg in args)
        {
            if (ShellMeta.ContainsMetacharacters(arg))
                return ActionResolution.Reject("app-installer: arg contains shell metacharacters");
        }

        var manager = args[0];
        string executable;
        switch (manager)
        {
            case "apt":  executable = AptExe;  break;
            case "snap": executable = SnapExe; break;
            default:
                return ActionResolution.Reject($"app-installer: unknown manager '{manager}'; expected apt or snap");
        }

        var remainder = args.Skip(1).ToArray();
        if (remainder.Length < 1)
            return ActionResolution.Reject("app-installer: verb required after manager");

        var verb = remainder[0];
        if (!AllowedVerbs.Contains(verb))
            return ActionResolution.Reject($"app-installer: verb '{verb}' not allowed; expected install, remove, or update");

        // Validate remaining tokens: flag or safe package name
        foreach (var token in remainder.Skip(1))
        {
            if (AllowedFlags.Contains(token))
                continue;

            if (!PackageName.IsMatch(token))
                return ActionResolution.Reject($"app-installer: '{token}' is not a valid package name or allowed flag");
        }

        // Return argv without the manager selector
        return new ActionResolution(executable, remainder);
    }
}
