namespace AuraCore.PrivHelper.MacOS.Validators;

/// <summary>
/// Validates argv for the "spotlight" action ID.
/// Hard-coded executable: /usr/bin/mdutil
///
/// Allowed argv patterns:
///   ["-a"]                     — status all volumes (length 1)
///   ["-i", "on" | "off"]       — toggle indexing, no path (length 2)
///   ["-i", "on" | "off", path] — toggle indexing on specific volume (length 3)
///   ["-E", path]               — erase + rebuild index (length 2)
///
/// Path rules (when present):
///   - Must NOT be SIP-protected
///   - Must start with /Users/, /Volumes/, exactly /, or /home/
///
/// Shell metacharacter check on every arg.
/// SIP-protected-path check on every path arg.
/// </summary>
internal sealed class SpotlightArgvValidator : IArgvValidator
{
    private const string Executable = "/usr/bin/mdutil";

    // Allowed path prefixes for volume/path arguments
    private static readonly string[] AllowedPathPrefixes =
    {
        "/Users/",
        "/Volumes/",
        "/home/",
    };

    public ActionResolution Validate(string[] args)
    {
        if (args.Length == 0)
            return ActionResolution.Reject("spotlight: argv required");

        // Check metacharacters on ALL args first
        foreach (var arg in args)
        {
            if (ShellMeta.ContainsMetacharacters(arg))
                return ActionResolution.Reject("spotlight: arg contains shell metacharacters");
        }

        return args[0] switch
        {
            "-a" when args.Length == 1
                => new ActionResolution(Executable, args),

            "-i" when args.Length == 2
                => ValidateIndexToggle(args, pathIndex: -1),

            "-i" when args.Length == 3
                => ValidateIndexToggle(args, pathIndex: 2),

            "-E" when args.Length == 2
                => ValidateErase(args),

            _ => ActionResolution.Reject(
                "spotlight: unrecognized argv pattern — " +
                "allowed: [-a] | [-i on|off] | [-i on|off path] | [-E path]")
        };
    }

    private static ActionResolution ValidateIndexToggle(string[] args, int pathIndex)
    {
        var onOff = args[1];
        if (onOff != "on" && onOff != "off")
            return ActionResolution.Reject("spotlight: second arg for -i must be 'on' or 'off'");

        if (pathIndex >= 0)
        {
            var pathValidation = ValidatePath(args[pathIndex]);
            if (pathValidation is not null) return pathValidation;
        }

        return new ActionResolution(Executable, args);
    }

    private static ActionResolution ValidateErase(string[] args)
    {
        var pathValidation = ValidatePath(args[1]);
        if (pathValidation is not null) return pathValidation;
        return new ActionResolution(Executable, args);
    }

    /// <summary>
    /// Returns a rejected resolution if the path is invalid; null if valid.
    /// </summary>
    private static ActionResolution? ValidatePath(string path)
    {
        if (ShellMeta.IsSipProtectedPath(path))
            return ActionResolution.Reject($"spotlight: path '{path}' is SIP-protected");

        // Must start with an allowed prefix OR be exactly "/"
        if (path == "/")
            return null;   // root is allowed for -E and -i

        foreach (var prefix in AllowedPathPrefixes)
        {
            if (path.StartsWith(prefix, StringComparison.Ordinal))
                return null;
        }

        return ActionResolution.Reject(
            $"spotlight: path '{path}' must start with /Users/, /Volumes/, /home/, or be /");
    }
}
