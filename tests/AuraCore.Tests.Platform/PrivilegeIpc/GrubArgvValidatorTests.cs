using AuraCore.PrivHelper.Linux;
using Xunit;

namespace AuraCore.Tests.Platform.PrivilegeIpc;

/// <summary>
/// Validator-level tests for the three new GrubArgvValidator sub-actions added in B5:
/// grub-mkconfig, backup-etc-grub, restore-etc-grub.
///
/// Uses ActionWhitelist.Dispatch (public entry point) since GrubArgvValidator is internal.
/// Plain xunit Assert — no FluentAssertions.
/// </summary>
public class GrubArgvValidatorTests
{
    private readonly ActionWhitelist _wl = new();

    // ── grub-mkconfig ────────────────────────────────────────────────

    [Fact]
    public void GrubMkconfig_resolves_to_mkconfig_exe_with_o_flag()
    {
        var r = _wl.Dispatch("grub", new[] { "grub-mkconfig" });
        Assert.False(r.IsRejected);
        Assert.Equal("/usr/sbin/grub-mkconfig", r.Executable);
        Assert.Equal(new[] { "-o", "/boot/grub/grub.cfg" }, r.Argv);
    }

    [Fact]
    public void GrubMkconfig_extra_args_rejected()
    {
        var r = _wl.Dispatch("grub", new[] { "grub-mkconfig", "extra" });
        Assert.True(r.IsRejected);
        Assert.Contains("no additional arguments", r.RejectionReason);
    }

    // ── backup-etc-grub ──────────────────────────────────────────────

    [Fact]
    public void BackupEtcGrub_resolves_to_cp_from_src_to_backup()
    {
        var r = _wl.Dispatch("grub", new[] { "backup-etc-grub" });
        Assert.False(r.IsRejected);
        Assert.Equal("/bin/cp", r.Executable);
        Assert.Equal(new[] { "/etc/default/grub", "/etc/default/grub.bak.auracore" }, r.Argv);
    }

    [Fact]
    public void BackupEtcGrub_extra_args_rejected()
    {
        var r = _wl.Dispatch("grub", new[] { "backup-etc-grub", "extra" });
        Assert.True(r.IsRejected);
    }

    // ── restore-etc-grub ─────────────────────────────────────────────

    [Fact]
    public void RestoreEtcGrub_resolves_to_cp_reversed()
    {
        var r = _wl.Dispatch("grub", new[] { "restore-etc-grub" });
        Assert.False(r.IsRejected);
        Assert.Equal("/bin/cp", r.Executable);
        Assert.Equal(new[] { "/etc/default/grub.bak.auracore", "/etc/default/grub" }, r.Argv);
    }

    [Fact]
    public void RestoreEtcGrub_extra_args_rejected()
    {
        var r = _wl.Dispatch("grub", new[] { "restore-etc-grub", "extra" });
        Assert.True(r.IsRejected);
    }
}
