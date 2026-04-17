using System.Text.RegularExpressions;

namespace AuraCore.PrivHelper.Linux.Validators;

/// <summary>
/// Validates argv for the "symlink.create" action ID.
///
/// Maps to: ln -s [-f] -- target linkname
///
///   args: ["-s", "-f", "--", target, linkname]   (all required, -f optional but included for idempotency)
///   exe:  /bin/ln
///   argv: ["-s", "-f", "--", target, linkname]
///
/// Target and linkname must be absolute paths matching the safe-path pattern.
/// Shell metacharacters and path traversal components are rejected.
/// The "--" end-of-options marker is always required as arg[2] to guard
/// against arguments that start with '-'.
/// </summary>
internal sealed class SymlinkArgvValidator : IArgvValidator
{
    private const string LnExe = "/bin/ln";

    // Absolute path, no spaces, no shell metacharacters, no ".." component.
    // Characters allowed: letters, digits, /, ., _, -, @, +, ~
    private static readonly Regex SafeAbsPath =
        new(@"^/[/\w._\-@\+~]*$", RegexOptions.Compiled);

    // Individual components we always allow (the fixed flags we hard-code)
    private static readonly HashSet<string> AllowedFlags =
        new(StringComparer.Ordinal) { "-s", "-f", "--" };

    public ActionResolution Validate(string[] args)
    {
        // Expected exactly 5 args: ["-s", "-f", "--", target, linkname]
        if (args.Length != 5)
            return ActionResolution.Reject(
                "symlink.create: expected exactly 5 args: -s -f -- <target> <linkname>");

        // Verify the three fixed leading tokens
        if (args[0] != "-s")
            return ActionResolution.Reject("symlink.create: args[0] must be -s");
        if (args[1] != "-f")
            return ActionResolution.Reject("symlink.create: args[1] must be -f");
        if (args[2] != "--")
            return ActionResolution.Reject("symlink.create: args[2] must be --");

        var target   = args[3];
        var linkname = args[4];

        // Shell metacharacter check on all args
        foreach (var arg in args)
        {
            if (AllowedFlags.Contains(arg)) continue;
            if (ShellMeta.ContainsMetacharacters(arg))
                return ActionResolution.Reject("symlink.create: arg contains shell metacharacters");
        }

        // Reject path traversal components
        if (target.Contains("..") || linkname.Contains(".."))
            return ActionResolution.Reject("symlink.create: path traversal (..) not allowed");

        // Both paths must be absolute and match the safe-path pattern
        if (!SafeAbsPath.IsMatch(target))
            return ActionResolution.Reject(
                $"symlink.create: target '{target}' does not match safe absolute-path pattern");

        if (!SafeAbsPath.IsMatch(linkname))
            return ActionResolution.Reject(
                $"symlink.create: linkname '{linkname}' does not match safe absolute-path pattern");

        // Hard-code the executable; client's hint is ignored per spec §9
        return new ActionResolution(LnExe, args);
    }
}
