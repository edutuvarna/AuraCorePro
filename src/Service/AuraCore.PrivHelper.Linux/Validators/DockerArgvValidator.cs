using System.Text.RegularExpressions;

namespace AuraCore.PrivHelper.Linux.Validators;

/// <summary>
/// Validates argv for the "docker" action ID.
/// Hard-coded executable: /usr/bin/docker
///
/// Allowed form: docker system prune [--volumes] [--all] [-f]
///
/// Explicit rejections:
///   - Any subcommand other than "system"
///   - Any verb other than "prune"
///   - The flags -v / --volume (volume mount — dangerous)
///   - Any unknown flags
///   - Shell metacharacters
/// </summary>
internal sealed class DockerArgvValidator : IArgvValidator
{
    private const string Executable = "/usr/bin/docker";

    private static readonly HashSet<string> AllowedFlags =
        new(StringComparer.Ordinal) { "--volumes", "--all", "-f" };

    // Explicitly banned to catch dangerous volume mount flags
    private static readonly HashSet<string> BannedFlags =
        new(StringComparer.Ordinal) { "-v", "--volume" };

    public ActionResolution Validate(string[] args)
    {
        if (args.Length < 2)
            return ActionResolution.Reject("docker: at least 2 args required (subcommand verb ...)");

        foreach (var arg in args)
        {
            if (ShellMeta.ContainsMetacharacters(arg))
                return ActionResolution.Reject("docker: arg contains shell metacharacters");
        }

        if (args[0] != "system")
            return ActionResolution.Reject($"docker: subcommand '{args[0]}' not allowed; only 'system' is permitted");

        if (args[1] != "prune")
            return ActionResolution.Reject($"docker: verb '{args[1]}' not allowed; only 'prune' is permitted");

        // Validate remaining flags
        foreach (var flag in args.Skip(2))
        {
            if (BannedFlags.Contains(flag))
                return ActionResolution.Reject($"docker: flag '{flag}' is explicitly banned");

            if (!AllowedFlags.Contains(flag))
                return ActionResolution.Reject($"docker: flag '{flag}' not in allowlist");
        }

        return new ActionResolution(Executable, args);
    }
}
