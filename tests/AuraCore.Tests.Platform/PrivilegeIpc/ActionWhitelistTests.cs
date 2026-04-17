using AuraCore.PrivHelper.Linux;
using FluentAssertions;
using Xunit;

namespace AuraCore.Tests.Platform.PrivilegeIpc;

public class ActionWhitelistTests
{
    private readonly ActionWhitelist _wl = new();

    [Fact]
    public void RegisteredActionIds_has_the_seven_action_ids()
    {
        _wl.RegisteredActionIds.Should().BeEquivalentTo(new[]
        {
            "journal", "snap-flatpak", "docker", "kernel", "app-installer", "grub",
            "symlink.create",  // Phase 5.5 — Symlink Manager create action
        });
    }

    [Fact]
    public void Dispatch_unknown_action_is_rejected()
    {
        var r = _wl.Dispatch("rm-rf-slash", new[] { "-rf", "/" });
        r.IsRejected.Should().BeTrue();
        r.RejectionReason.Should().Contain("unknown action");
    }

    // Journal
    [Fact]
    public void Journal_vacuum_size_is_accepted()
    {
        var r = _wl.Dispatch("journal", new[] { "--vacuum-size=500M" });
        r.IsRejected.Should().BeFalse();
        r.Executable.Should().Be("/usr/bin/journalctl");
        r.Argv.Should().BeEquivalentTo(new[] { "--vacuum-size=500M" });
    }

    [Fact]
    public void Journal_rejects_shell_metachar()
    {
        var r = _wl.Dispatch("journal", new[] { "--vacuum-size=500M; rm -rf /" });
        r.IsRejected.Should().BeTrue();
    }

    [Fact]
    public void Journal_rejects_unknown_flag()
    {
        var r = _wl.Dispatch("journal", new[] { "--evil-flag" });
        r.IsRejected.Should().BeTrue();
    }

    [Fact]
    public void Journal_rejects_extra_unknown_flag_mixed_with_valid()
    {
        // Even one unrecognized arg should reject (no --output-fields or similar)
        var r = _wl.Dispatch("journal", new[] { "--vacuum-size=500M", "--output-fields=malicious" });
        r.IsRejected.Should().BeTrue();
    }

    // Snap/Flatpak
    [Fact]
    public void SnapFlatpak_snap_remove_is_accepted()
    {
        var r = _wl.Dispatch("snap-flatpak", new[] { "snap", "remove", "firefox" });
        r.IsRejected.Should().BeFalse();
        r.Executable.Should().Be("/usr/bin/snap");
        r.Argv[0].Should().Be("remove");
        r.Argv[1].Should().Be("firefox");
    }

    [Fact]
    public void SnapFlatpak_flatpak_uninstall_is_accepted()
    {
        var r = _wl.Dispatch("snap-flatpak", new[] { "flatpak", "uninstall", "org.gimp.GIMP" });
        r.IsRejected.Should().BeFalse();
        r.Executable.Should().Be("/usr/bin/flatpak");
    }

    [Fact]
    public void SnapFlatpak_rejects_wildcard_package_name()
    {
        var r = _wl.Dispatch("snap-flatpak", new[] { "snap", "remove", "*" });
        r.IsRejected.Should().BeTrue();
    }

    [Fact]
    public void SnapFlatpak_rejects_invalid_verb()
    {
        var r = _wl.Dispatch("snap-flatpak", new[] { "snap", "install", "firefox" });
        r.IsRejected.Should().BeTrue();
    }

    [Fact]
    public void SnapFlatpak_rejects_unknown_manager()
    {
        var r = _wl.Dispatch("snap-flatpak", new[] { "brew", "remove", "firefox" });
        r.IsRejected.Should().BeTrue();
    }

    // Docker
    [Fact]
    public void Docker_system_prune_volumes_accepted()
    {
        var r = _wl.Dispatch("docker", new[] { "system", "prune", "--volumes", "-f" });
        r.IsRejected.Should().BeFalse();
        r.Executable.Should().Be("/usr/bin/docker");
    }

    [Fact]
    public void Docker_rejects_run_command()
    {
        var r = _wl.Dispatch("docker", new[] { "run", "-v", "/:/host", "alpine" });
        r.IsRejected.Should().BeTrue();
    }

    [Fact]
    public void Docker_rejects_volume_mount_flag()
    {
        var r = _wl.Dispatch("docker", new[] { "system", "prune", "--volume", "-f" });
        r.IsRejected.Should().BeTrue();
    }

    // Kernel
    [Fact]
    public void Kernel_autoremove_linux_image_accepted()
    {
        var r = _wl.Dispatch("kernel", new[] { "autoremove", "-y", "linux-image-5.15.0-92-generic" });
        r.IsRejected.Should().BeFalse();
        r.Executable.Should().Be("/usr/bin/apt-get");
    }

    [Fact]
    public void Kernel_rejects_non_kernel_package()
    {
        var r = _wl.Dispatch("kernel", new[] { "remove", "firefox" });
        r.IsRejected.Should().BeTrue();
    }

    [Fact]
    public void Kernel_rejects_invalid_verb()
    {
        var r = _wl.Dispatch("kernel", new[] { "install", "-y", "linux-image-5.15.0-92-generic" });
        r.IsRejected.Should().BeTrue();
    }

    // App-installer
    [Fact]
    public void AppInstaller_apt_install_accepted()
    {
        var r = _wl.Dispatch("app-installer", new[] { "apt", "install", "-y", "vim" });
        r.IsRejected.Should().BeFalse();
        r.Executable.Should().Be("/usr/bin/apt-get");
    }

    [Fact]
    public void AppInstaller_snap_install_accepted()
    {
        var r = _wl.Dispatch("app-installer", new[] { "snap", "install", "firefox" });
        r.IsRejected.Should().BeFalse();
        r.Executable.Should().Be("/usr/bin/snap");
    }

    [Fact]
    public void AppInstaller_rejects_path_traversal_in_package_name()
    {
        var r = _wl.Dispatch("app-installer", new[] { "apt", "install", "../../etc/passwd" });
        r.IsRejected.Should().BeTrue();
    }

    [Fact]
    public void AppInstaller_rejects_unknown_manager()
    {
        var r = _wl.Dispatch("app-installer", new[] { "pip", "install", "malware" });
        r.IsRejected.Should().BeTrue();
    }

    // Grub
    [Fact]
    public void Grub_update_grub_accepted()
    {
        var r = _wl.Dispatch("grub", new[] { "update-grub" });
        r.IsRejected.Should().BeFalse();
        r.Executable.Should().Be("/usr/sbin/update-grub");
    }

    [Fact]
    public void Grub_restore_backup_from_safe_path_accepted()
    {
        var r = _wl.Dispatch("grub", new[] { "restore-backup", "/var/backups/auracore-grub.bak" });
        r.IsRejected.Should().BeFalse();
        r.Executable.Should().Be("/bin/cp");
    }

    [Fact]
    public void Grub_rejects_restore_with_shell_metachar()
    {
        var r = _wl.Dispatch("grub", new[] { "restore-backup", "/tmp/foo; rm /boot/grub/grub.cfg" });
        r.IsRejected.Should().BeTrue();
    }

    [Fact]
    public void Grub_rejects_unknown_subaction()
    {
        var r = _wl.Dispatch("grub", new[] { "rm-everything" });
        r.IsRejected.Should().BeTrue();
    }

    // Symlink.Create (Phase 5.5)
    [Fact]
    public void SymlinkCreate_valid_abs_paths_accepted()
    {
        var r = _wl.Dispatch("symlink.create",
            new[] { "-s", "-f", "--", "/opt/mytool/bin/tool", "/usr/local/bin/mytool" });
        r.IsRejected.Should().BeFalse();
        r.Executable.Should().Be("/bin/ln");
    }

    [Fact]
    public void SymlinkCreate_rejects_shell_metacharacters_in_path()
    {
        var r = _wl.Dispatch("symlink.create",
            new[] { "-s", "-f", "--", "/opt/tool; rm -rf /", "/usr/local/bin/x" });
        r.IsRejected.Should().BeTrue();
    }

    [Fact]
    public void SymlinkCreate_rejects_path_traversal()
    {
        var r = _wl.Dispatch("symlink.create",
            new[] { "-s", "-f", "--", "/opt/../../etc/passwd", "/usr/local/bin/x" });
        r.IsRejected.Should().BeTrue();
    }

    [Fact]
    public void SymlinkCreate_rejects_wrong_arg_count()
    {
        var r = _wl.Dispatch("symlink.create", new[] { "-s", "/opt/tool" });
        r.IsRejected.Should().BeTrue();
    }

    [Fact]
    public void IsRegistered_returns_true_for_known_action()
    {
        _wl.IsRegistered("journal").Should().BeTrue();
        _wl.IsRegistered("grub").Should().BeTrue();
        _wl.IsRegistered("symlink.create").Should().BeTrue();
    }

    [Fact]
    public void IsRegistered_returns_false_for_unknown_action()
    {
        _wl.IsRegistered("rm-rf-slash").Should().BeFalse();
        _wl.IsRegistered("").Should().BeFalse();
    }
}
