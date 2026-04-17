using System.Text.RegularExpressions;

namespace AuraCore.PrivHelper.Linux.Validators;

/// <summary>
/// Validates argv for the "kernel" action ID.
/// Hard-coded executable: /usr/bin/apt-get
///
/// Allowed form:  apt-get (autoremove|remove|purge) [-y] linux-(image|headers|modules)-PACKAGE
///
/// Every non-flag arg that is not the verb must match the linux-* package pattern.
/// This prevents removing non-kernel packages through this action.
/// </summary>
internal sealed class KernelArgvValidator : IArgvValidator
{
    private const string Executable = "/usr/bin/apt-get";

    private static readonly HashSet<string> AllowedVerbs =
        new(StringComparer.Ordinal) { "autoremove", "remove", "purge" };

    private static readonly HashSet<string> AllowedFlags =
        new(StringComparer.Ordinal) { "-y" };

    // Only kernel-related packages are permitted
    private static readonly Regex KernelPackage =
        new(@"^linux-(image|headers|modules)-[\w.+-]+$", RegexOptions.Compiled);

    public ActionResolution Validate(string[] args)
    {
        if (args.Length == 0)
            return ActionResolution.Reject("kernel: argv required");

        foreach (var arg in args)
        {
            if (ShellMeta.ContainsMetacharacters(arg))
                return ActionResolution.Reject("kernel: arg contains shell metacharacters");
        }

        var verb = args[0];
        if (!AllowedVerbs.Contains(verb))
            return ActionResolution.Reject($"kernel: verb '{verb}' not allowed; expected autoremove, remove, or purge");

        // Validate remaining tokens: each must be an allowed flag or a linux-* package name
        foreach (var token in args.Skip(1))
        {
            if (AllowedFlags.Contains(token))
                continue;

            if (!KernelPackage.IsMatch(token))
                return ActionResolution.Reject($"kernel: '{token}' is not a kernel package name and not an allowed flag");
        }

        return new ActionResolution(Executable, args);
    }
}
