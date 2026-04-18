using System.Text.RegularExpressions;

namespace AuraCore.PrivHelper.Linux.Validators;

/// <summary>
/// Validates argv for the "grub" action ID.
///
/// Six sub-actions are supported (first arg selects):
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
///
///   grub-mkconfig
///     args:  ["grub-mkconfig"]
///     exe:   /usr/sbin/grub-mkconfig
///     argv:  ["-o", "/boot/grub/grub.cfg"]
///
///   backup-etc-grub
///     args:  ["backup-etc-grub"]
///     exe:   /bin/cp
///     argv:  ["/etc/default/grub", "/etc/default/grub.bak.auracore"]
///
///   restore-etc-grub
///     args:  ["restore-etc-grub"]
///     exe:   /bin/cp
///     argv:  ["/etc/default/grub.bak.auracore", "/etc/default/grub"]
/// </summary>
internal sealed class GrubArgvValidator : IArgvValidator
{
    private const string UpdateGrubExe = "/usr/sbin/update-grub";
    private const string SedExe        = "/bin/sed";
    private const string CpExe         = "/bin/cp";

    private const string GrubCfgPath          = "/boot/grub/grub.cfg";
    private const string GrubMkconfigExe      = "/usr/sbin/grub-mkconfig";
    private const string EtcDefaultGrub       = "/etc/default/grub";
    private const string EtcDefaultGrubBackup = "/etc/default/grub.bak.auracore";

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
            "update-grub"      => ValidateUpdateGrub(args),
            "edit-config"      => ValidateEditConfig(args),
            "restore-backup"   => ValidateRestoreBackup(args),
            "grub-mkconfig"    => ValidateGrubMkconfig(args),
            "backup-etc-grub"  => ValidateBackupEtcGrub(args),
            "restore-etc-grub" => ValidateRestoreEtcGrub(args),
            _                  => ActionResolution.Reject($"grub: unknown sub-action '{subAction}'; expected update-grub, edit-config, restore-backup, grub-mkconfig, backup-etc-grub, or restore-etc-grub"),
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

    private static ActionResolution ValidateGrubMkconfig(string[] args)
    {
        // Expected: ["grub-mkconfig"] — no extra client args; dest is locked to /boot/grub/grub.cfg.
        if (args.Length != 1)
            return ActionResolution.Reject("grub: grub-mkconfig takes no additional arguments");

        return new ActionResolution(GrubMkconfigExe, new[] { "-o", GrubCfgPath });
    }

    private static ActionResolution ValidateBackupEtcGrub(string[] args)
    {
        // Expected: ["backup-etc-grub"] — copy /etc/default/grub -> /etc/default/grub.bak.auracore.
        if (args.Length != 1)
            return ActionResolution.Reject("grub: backup-etc-grub takes no additional arguments");

        return new ActionResolution(CpExe, new[] { EtcDefaultGrub, EtcDefaultGrubBackup });
    }

    private static ActionResolution ValidateRestoreEtcGrub(string[] args)
    {
        // Expected: ["restore-etc-grub"] — copy /etc/default/grub.bak.auracore -> /etc/default/grub.
        if (args.Length != 1)
            return ActionResolution.Reject("grub: restore-etc-grub takes no additional arguments");

        return new ActionResolution(CpExe, new[] { EtcDefaultGrubBackup, EtcDefaultGrub });
    }
}
