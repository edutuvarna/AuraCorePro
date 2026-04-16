using System.Text.RegularExpressions;

namespace AuraCore.PrivHelper.Linux.Validators;

/// <summary>
/// Validates argv for the "grub" action ID.
///
/// Three sub-actions are supported (first arg selects):
///
///   update-grub
///     args:  ["update-grub"]
///     exe:   /usr/sbin/update-grub
///     argv:  [] (no extra args to update-grub)
///
///   edit-config
///     args:  ["edit-config", sed-expression, config-file-path]
///     exe:   /bin/sed
///     argv:  ["-i", expression, path]
///     constraints: expression must not contain shell metacharacters and must
///                  be a non-empty string; path must match safe-path pattern.
///
///   restore-backup
///     args:  ["restore-backup", backup-source-path]
///     exe:   /bin/cp
///     argv:  [backup-source-path, "/boot/grub/grub.cfg"]
///     constraints: source path must match ^[/\w._-]+$
/// </summary>
internal sealed class GrubArgvValidator : IArgvValidator
{
    private const string UpdateGrubExe = "/usr/sbin/update-grub";
    private const string SedExe        = "/bin/sed";
    private const string CpExe         = "/bin/cp";

    private const string GrubCfgPath   = "/boot/grub/grub.cfg";

    // Safe filesystem path: absolute, no spaces, no shell meta
    private static readonly Regex SafePath =
        new(@"^[/\w._-]+$", RegexOptions.Compiled);

    public ActionResolution Validate(string[] args)
    {
        if (args.Length == 0)
            return ActionResolution.Reject("grub: argv required");

        // Check all args for shell metacharacters
        foreach (var arg in args)
        {
            if (ShellMeta.ContainsMetacharacters(arg))
                return ActionResolution.Reject("grub: arg contains shell metacharacters");
        }

        var subAction = args[0];

        return subAction switch
        {
            "update-grub"    => ValidateUpdateGrub(args),
            "edit-config"    => ValidateEditConfig(args),
            "restore-backup" => ValidateRestoreBackup(args),
            _                => ActionResolution.Reject($"grub: unknown sub-action '{subAction}'; expected update-grub, edit-config, or restore-backup"),
        };
    }

    private static ActionResolution ValidateUpdateGrub(string[] args)
    {
        if (args.Length != 1)
            return ActionResolution.Reject("grub: update-grub takes no additional arguments");

        // update-grub takes no argv itself
        return new ActionResolution(UpdateGrubExe, Array.Empty<string>());
    }

    private static ActionResolution ValidateEditConfig(string[] args)
    {
        // Expected: ["edit-config", sed-expression, config-path]
        if (args.Length != 3)
            return ActionResolution.Reject("grub: edit-config requires exactly 2 args: expression config-path");

        var expression = args[1];
        var configPath = args[2];

        if (string.IsNullOrWhiteSpace(expression))
            return ActionResolution.Reject("grub: edit-config: expression must not be empty");

        if (!SafePath.IsMatch(configPath))
            return ActionResolution.Reject($"grub: edit-config: config path '{configPath}' does not match safe path pattern");

        // sed -i 'expression' configPath
        return new ActionResolution(SedExe, new[] { "-i", expression, configPath });
    }

    private static ActionResolution ValidateRestoreBackup(string[] args)
    {
        // Expected: ["restore-backup", source-path]
        if (args.Length != 2)
            return ActionResolution.Reject("grub: restore-backup requires exactly 1 arg: source-path");

        var sourcePath = args[1];

        if (!SafePath.IsMatch(sourcePath))
            return ActionResolution.Reject($"grub: restore-backup: source path '{sourcePath}' does not match safe path pattern");

        // cp source /boot/grub/grub.cfg
        return new ActionResolution(CpExe, new[] { sourcePath, GrubCfgPath });
    }
}
