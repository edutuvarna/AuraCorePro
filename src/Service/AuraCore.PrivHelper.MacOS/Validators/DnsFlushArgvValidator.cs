namespace AuraCore.PrivHelper.MacOS.Validators;

/// <summary>
/// Validates argv for the "dns-flush" action ID.
/// Hard-coded executable: /usr/bin/dscacheutil
///
/// Allowed argvs:
///   []               — empty, daemon treats as "-flushcache"
///   ["-flushcache"]  — explicit flush flag
///
/// Any other argument or shell metacharacter → reject.
/// </summary>
internal sealed class DnsFlushArgvValidator : IArgvValidator
{
    private const string Executable = "/usr/bin/dscacheutil";

    public ActionResolution Validate(string[] args)
    {
        // Empty argv → accepted (equivalent to -flushcache)
        if (args.Length == 0)
            return new ActionResolution(Executable, args);

        // Only exactly ["-flushcache"] is accepted
        if (args.Length == 1 && args[0] == "-flushcache")
        {
            if (ShellMeta.ContainsMetacharacters(args[0]))
                return ActionResolution.Reject("dns-flush: arg contains shell metacharacters");
            return new ActionResolution(Executable, args);
        }

        return ActionResolution.Reject("dns-flush: only [] or [\"-flushcache\"] are allowed");
    }
}
