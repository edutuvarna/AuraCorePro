using AuraCore.PrivHelper.MacOS;
using FluentAssertions;
using Xunit;

namespace AuraCore.Tests.Platform.PrivilegeIpc;

/// <summary>
/// Tests for <see cref="ActionWhitelist"/> + all four macOS argv validators
/// (Task 27, spec §3.9). Mirrors the structure of the Linux ActionWhitelistTests.
/// </summary>
public class MacOSActionWhitelistTests
{
    private readonly ActionWhitelist _wl = new();

    // -----------------------------------------------------------------------
    // Registration
    // -----------------------------------------------------------------------

    [Fact]
    public void RegisteredActionIds_returns_exactly_four_expected_ids()
    {
        _wl.RegisteredActionIds.Should().BeEquivalentTo(new[]
        {
            "dns-flush", "purgeable", "spotlight", "time-machine",
        });
    }

    [Fact]
    public void IsRegistered_true_for_all_four_action_ids()
    {
        _wl.IsRegistered("dns-flush").Should().BeTrue();
        _wl.IsRegistered("purgeable").Should().BeTrue();
        _wl.IsRegistered("spotlight").Should().BeTrue();
        _wl.IsRegistered("time-machine").Should().BeTrue();
    }

    [Fact]
    public void IsRegistered_false_for_unknown_action()
    {
        _wl.IsRegistered("rm-rf-slash").Should().BeFalse();
        _wl.IsRegistered("").Should().BeFalse();
        _wl.IsRegistered("swap").Should().BeFalse();
    }

    [Fact]
    public void Dispatch_unknown_action_is_rejected_with_unknown_action_in_reason()
    {
        var r = _wl.Dispatch("rm-rf-slash", new[] { "-rf", "/" });
        r.IsRejected.Should().BeTrue();
        r.RejectionReason.Should().Contain("unknown action");
    }

    // -----------------------------------------------------------------------
    // DnsFlush — /usr/bin/dscacheutil
    // -----------------------------------------------------------------------

    [Fact]
    public void DnsFlush_empty_argv_is_accepted()
    {
        var r = _wl.Dispatch("dns-flush", Array.Empty<string>());
        r.IsRejected.Should().BeFalse();
        r.Executable.Should().Be("/usr/bin/dscacheutil");
        r.Argv.Should().BeEmpty();
    }

    [Fact]
    public void DnsFlush_flushcache_flag_is_accepted()
    {
        var r = _wl.Dispatch("dns-flush", new[] { "-flushcache" });
        r.IsRejected.Should().BeFalse();
        r.Executable.Should().Be("/usr/bin/dscacheutil");
        r.Argv.Should().Equal("-flushcache");
    }

    [Fact]
    public void DnsFlush_rejects_extra_unknown_flag()
    {
        var r = _wl.Dispatch("dns-flush", new[] { "-flushcache", "-q" });
        r.IsRejected.Should().BeTrue();
    }

    [Fact]
    public void DnsFlush_rejects_shell_metachar_in_arg()
    {
        var r = _wl.Dispatch("dns-flush", new[] { "-flushcache; rm -rf /" });
        r.IsRejected.Should().BeTrue();
    }

    [Fact]
    public void DnsFlush_rejects_unknown_flag()
    {
        var r = _wl.Dispatch("dns-flush", new[] { "--malicious" });
        r.IsRejected.Should().BeTrue();
    }

    [Fact]
    public void DnsFlush_rejects_path_traversal()
    {
        var r = _wl.Dispatch("dns-flush", new[] { "../etc/passwd" });
        r.IsRejected.Should().BeTrue();
    }

    // -----------------------------------------------------------------------
    // Purgeable — /usr/bin/tmutil thinlocalsnapshots
    // -----------------------------------------------------------------------

    [Fact]
    public void Purgeable_valid_4_element_argv_is_accepted()
    {
        var r = _wl.Dispatch("purgeable", new[] { "thinlocalsnapshots", "/", "1073741824", "4" });
        r.IsRejected.Should().BeFalse();
        r.Executable.Should().Be("/usr/bin/tmutil");
        r.Argv.Should().Equal("thinlocalsnapshots", "/", "1073741824", "4");
    }

    [Fact]
    public void Purgeable_accepts_urgency_level_1()
    {
        var r = _wl.Dispatch("purgeable", new[] { "thinlocalsnapshots", "/", "500000000", "1" });
        r.IsRejected.Should().BeFalse();
    }

    [Fact]
    public void Purgeable_rejects_wrong_first_arg()
    {
        var r = _wl.Dispatch("purgeable", new[] { "deletelocalsnapshots", "/", "1073741824", "4" });
        r.IsRejected.Should().BeTrue();
    }

    [Fact]
    public void Purgeable_rejects_non_root_mount()
    {
        // Second arg must be exactly "/" — reject arbitrary mount point
        var r = _wl.Dispatch("purgeable", new[] { "thinlocalsnapshots", "/Volumes/Ext", "1073741824", "4" });
        r.IsRejected.Should().BeTrue();
    }

    [Fact]
    public void Purgeable_rejects_invalid_bytes_arg()
    {
        // Bytes field must be 1-10 digits only
        var r = _wl.Dispatch("purgeable", new[] { "thinlocalsnapshots", "/", "not-a-number", "4" });
        r.IsRejected.Should().BeTrue();
    }

    [Fact]
    public void Purgeable_rejects_invalid_urgency_level_5()
    {
        // Urgency must be 1-4
        var r = _wl.Dispatch("purgeable", new[] { "thinlocalsnapshots", "/", "1073741824", "5" });
        r.IsRejected.Should().BeTrue();
    }

    [Fact]
    public void Purgeable_rejects_urgency_level_0()
    {
        var r = _wl.Dispatch("purgeable", new[] { "thinlocalsnapshots", "/", "1073741824", "0" });
        r.IsRejected.Should().BeTrue();
    }

    [Fact]
    public void Purgeable_rejects_wrong_length()
    {
        var r = _wl.Dispatch("purgeable", new[] { "thinlocalsnapshots", "/" });
        r.IsRejected.Should().BeTrue();
    }

    [Fact]
    public void Purgeable_rejects_shell_metachar_in_bytes()
    {
        var r = _wl.Dispatch("purgeable", new[] { "thinlocalsnapshots", "/", "1073741824; rm /", "4" });
        r.IsRejected.Should().BeTrue();
    }

    [Fact]
    public void Purgeable_rejects_bytes_longer_than_10_digits()
    {
        var r = _wl.Dispatch("purgeable", new[] { "thinlocalsnapshots", "/", "12345678901", "4" });
        r.IsRejected.Should().BeTrue();
    }

    // -----------------------------------------------------------------------
    // Spotlight — /usr/bin/mdutil
    // -----------------------------------------------------------------------

    [Fact]
    public void Spotlight_status_all_volumes_is_accepted()
    {
        var r = _wl.Dispatch("spotlight", new[] { "-a" });
        r.IsRejected.Should().BeFalse();
        r.Executable.Should().Be("/usr/bin/mdutil");
        r.Argv.Should().Equal("-a");
    }

    [Fact]
    public void Spotlight_disable_indexing_no_path_is_accepted()
    {
        var r = _wl.Dispatch("spotlight", new[] { "-i", "off" });
        r.IsRejected.Should().BeFalse();
        r.Argv.Should().Equal("-i", "off");
    }

    [Fact]
    public void Spotlight_enable_indexing_on_users_path_is_accepted()
    {
        var r = _wl.Dispatch("spotlight", new[] { "-i", "on", "/Users/admin" });
        r.IsRejected.Should().BeFalse();
    }

    [Fact]
    public void Spotlight_enable_indexing_on_volumes_path_is_accepted()
    {
        var r = _wl.Dispatch("spotlight", new[] { "-i", "on", "/Volumes/Ext" });
        r.IsRejected.Should().BeFalse();
    }

    [Fact]
    public void Spotlight_erase_rebuild_on_users_path_is_accepted()
    {
        var r = _wl.Dispatch("spotlight", new[] { "-E", "/Users/admin" });
        r.IsRejected.Should().BeFalse();
        r.Argv.Should().Equal("-E", "/Users/admin");
    }

    [Fact]
    public void Spotlight_erase_rebuild_on_root_is_accepted()
    {
        var r = _wl.Dispatch("spotlight", new[] { "-E", "/" });
        r.IsRejected.Should().BeFalse();
    }

    [Fact]
    public void Spotlight_rejects_unknown_flag()
    {
        var r = _wl.Dispatch("spotlight", new[] { "-d" });
        r.IsRejected.Should().BeTrue();
    }

    [Fact]
    public void Spotlight_rejects_indexing_with_invalid_on_off_value()
    {
        var r = _wl.Dispatch("spotlight", new[] { "-i", "maybe" });
        r.IsRejected.Should().BeTrue();
    }

    [Fact]
    public void Spotlight_rejects_sip_protected_path()
    {
        var r = _wl.Dispatch("spotlight", new[] { "-i", "off", "/System/Library" });
        r.IsRejected.Should().BeTrue();
    }

    [Fact]
    public void Spotlight_rejects_sip_protected_path_in_erase()
    {
        var r = _wl.Dispatch("spotlight", new[] { "-E", "/usr/bin" });
        r.IsRejected.Should().BeTrue();
    }

    [Fact]
    public void Spotlight_rejects_shell_metachar_in_path()
    {
        var r = _wl.Dispatch("spotlight", new[] { "-i", "on", "/Users/admin; rm -rf /" });
        r.IsRejected.Should().BeTrue();
    }

    [Fact]
    public void Spotlight_rejects_path_not_in_allowed_roots()
    {
        // Path must start with /Users/, /Volumes/, / or /home/ — /var/ is not allowed
        var r = _wl.Dispatch("spotlight", new[] { "-i", "on", "/var/data" });
        r.IsRejected.Should().BeTrue();
    }

    [Fact]
    public void Spotlight_rejects_empty_argv()
    {
        var r = _wl.Dispatch("spotlight", Array.Empty<string>());
        r.IsRejected.Should().BeTrue();
    }

    // -----------------------------------------------------------------------
    // TimeMachine — /usr/bin/tmutil
    // -----------------------------------------------------------------------

    [Fact]
    public void TimeMachine_startbackup_no_flags_is_accepted()
    {
        var r = _wl.Dispatch("time-machine", new[] { "startbackup" });
        r.IsRejected.Should().BeFalse();
        r.Executable.Should().Be("/usr/bin/tmutil");
        r.Argv.Should().Equal("startbackup");
    }

    [Fact]
    public void TimeMachine_startbackup_auto_flag_is_accepted()
    {
        var r = _wl.Dispatch("time-machine", new[] { "startbackup", "--auto" });
        r.IsRejected.Should().BeFalse();
    }

    [Fact]
    public void TimeMachine_startbackup_no_auto_flag_is_accepted()
    {
        var r = _wl.Dispatch("time-machine", new[] { "startbackup", "--no-auto" });
        r.IsRejected.Should().BeFalse();
    }

    [Fact]
    public void TimeMachine_stopbackup_is_accepted()
    {
        var r = _wl.Dispatch("time-machine", new[] { "stopbackup" });
        r.IsRejected.Should().BeFalse();
    }

    [Fact]
    public void TimeMachine_listbackups_is_accepted()
    {
        var r = _wl.Dispatch("time-machine", new[] { "listbackups" });
        r.IsRejected.Should().BeFalse();
    }

    [Fact]
    public void TimeMachine_enable_is_accepted()
    {
        var r = _wl.Dispatch("time-machine", new[] { "enable" });
        r.IsRejected.Should().BeFalse();
    }

    [Fact]
    public void TimeMachine_disable_is_accepted()
    {
        var r = _wl.Dispatch("time-machine", new[] { "disable" });
        r.IsRejected.Should().BeFalse();
    }

    [Fact]
    public void TimeMachine_deletelocalsnapshots_valid_date_is_accepted()
    {
        var r = _wl.Dispatch("time-machine", new[] { "deletelocalsnapshots", "2024-03-15-120000" });
        r.IsRejected.Should().BeFalse();
        r.Argv.Should().Equal("deletelocalsnapshots", "2024-03-15-120000");
    }

    [Fact]
    public void TimeMachine_rejects_restore_verb()
    {
        // "restore" is explicitly forbidden — too powerful
        var r = _wl.Dispatch("time-machine", new[] { "restore", "/Volumes/Backup", "/Users/admin" });
        r.IsRejected.Should().BeTrue();
    }

    [Fact]
    public void TimeMachine_rejects_setdestination_verb()
    {
        var r = _wl.Dispatch("time-machine", new[] { "setdestination", "/Volumes/Backup" });
        r.IsRejected.Should().BeTrue();
    }

    [Fact]
    public void TimeMachine_rejects_latestbackup_verb()
    {
        var r = _wl.Dispatch("time-machine", new[] { "latestbackup" });
        r.IsRejected.Should().BeTrue();
    }

    [Fact]
    public void TimeMachine_rejects_delete_verb()
    {
        var r = _wl.Dispatch("time-machine", new[] { "delete", "/Volumes/Backup/2024-03-15-120000" });
        r.IsRejected.Should().BeTrue();
    }

    [Fact]
    public void TimeMachine_rejects_invalid_date_format()
    {
        var r = _wl.Dispatch("time-machine", new[] { "deletelocalsnapshots", "2024-13-99" });
        r.IsRejected.Should().BeTrue();
    }

    [Fact]
    public void TimeMachine_rejects_shell_metachar_in_date()
    {
        var r = _wl.Dispatch("time-machine", new[] { "deletelocalsnapshots", "2024-03-15-120000; rm /" });
        r.IsRejected.Should().BeTrue();
    }

    [Fact]
    public void TimeMachine_rejects_extra_flag_on_stopbackup()
    {
        var r = _wl.Dispatch("time-machine", new[] { "stopbackup", "--force" });
        r.IsRejected.Should().BeTrue();
    }

    [Fact]
    public void TimeMachine_rejects_empty_argv()
    {
        var r = _wl.Dispatch("time-machine", Array.Empty<string>());
        r.IsRejected.Should().BeTrue();
    }

    [Fact]
    public void TimeMachine_rejects_unknown_flag_on_startbackup()
    {
        var r = _wl.Dispatch("time-machine", new[] { "startbackup", "--force" });
        r.IsRejected.Should().BeTrue();
    }
}
