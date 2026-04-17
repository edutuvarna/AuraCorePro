using AuraCore.PrivHelper.MacOS;
using FluentAssertions;
using Xunit;

namespace AuraCore.Tests.Platform.PrivilegeIpc;

/// <summary>
/// Tests for <see cref="ShellMeta"/> — the shared metacharacter + SIP-path
/// rejection helper used by all four macOS argv validators (Task 27, spec §3.10).
/// </summary>
public class MacOSShellMetaTests
{
    // -----------------------------------------------------------------------
    // ContainsMetacharacters — each banned character individually
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(";")]
    [InlineData("|")]
    [InlineData("&")]
    [InlineData("<")]
    [InlineData(">")]
    [InlineData("$")]
    [InlineData("`")]
    [InlineData("(")]
    [InlineData(")")]
    [InlineData("{")]
    [InlineData("}")]
    [InlineData("[")]
    [InlineData("]")]
    [InlineData("\n")]
    [InlineData("\r")]
    [InlineData("\0")]
    [InlineData("\\")]
    public void ContainsMetacharacters_returns_true_for_each_banned_char(string banned)
    {
        ShellMeta.ContainsMetacharacters(banned).Should().BeTrue(
            because: $"'{banned}' is in the banned metacharacter list");
        // Also embedded in an otherwise-clean string
        ShellMeta.ContainsMetacharacters("hello" + banned + "world").Should().BeTrue();
    }

    [Fact]
    public void ContainsMetacharacters_returns_true_for_path_traversal()
    {
        ShellMeta.ContainsMetacharacters("..").Should().BeTrue();
        ShellMeta.ContainsMetacharacters("/usr/../etc/passwd").Should().BeTrue();
        ShellMeta.ContainsMetacharacters("foo/../bar").Should().BeTrue();
    }

    [Theory]
    [InlineData("flushcache")]
    [InlineData("-flushcache")]
    [InlineData("thinlocalsnapshots")]
    [InlineData("startbackup")]
    [InlineData("1234567890")]
    [InlineData("/Users/admin/MyFile.txt")]
    [InlineData("/Volumes/ExternalDrive/data")]
    [InlineData("/usr/local/bin/foo")]
    public void ContainsMetacharacters_returns_false_for_safe_strings(string safe)
    {
        ShellMeta.ContainsMetacharacters(safe).Should().BeFalse(
            because: $"'{safe}' contains no shell metacharacters");
    }

    [Fact]
    public void ContainsMetacharacters_returns_false_for_empty_string()
    {
        ShellMeta.ContainsMetacharacters("").Should().BeFalse();
    }

    [Fact]
    public void ContainsMetacharacters_returns_false_for_alphanumeric_with_hyphens()
    {
        ShellMeta.ContainsMetacharacters("abc-123-def").Should().BeFalse();
    }

    // -----------------------------------------------------------------------
    // IsSipProtectedPath — SIP-protected prefixes
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("/System/Library/CoreServices/Finder.app")]
    [InlineData("/System/")]
    [InlineData("/bin/sh")]
    [InlineData("/bin/bash")]
    [InlineData("/sbin/launchd")]
    [InlineData("/usr/bin/ls")]
    [InlineData("/usr/bin/dscacheutil")]
    [InlineData("/usr/sbin/sysctl")]
    [InlineData("/usr/libexec/locationd")]
    [InlineData("/usr/lib/libSystem.dylib")]
    [InlineData("/private/var/db/sudoers")]
    [InlineData("/private/var/db/dslocal/nodes/Default")]
    [InlineData("/.vol/12345")]
    [InlineData("/.DocumentRevisions-V100/db-V9")]
    public void IsSipProtectedPath_returns_true_for_protected_paths(string path)
    {
        ShellMeta.IsSipProtectedPath(path).Should().BeTrue(
            because: $"'{path}' is under a SIP-protected prefix");
    }

    [Theory]
    [InlineData("/Users/admin/file.txt")]
    [InlineData("/Users/bob/Documents/report.pdf")]
    [InlineData("/Volumes/ExternalDrive/data.img")]
    [InlineData("/tmp/auracore-temp.sock")]
    [InlineData("/home/user/file")]
    [InlineData("/usr/local/bin/foo")]
    [InlineData("/usr/local/lib/libcustom.dylib")]
    [InlineData("/usr/local/share/auracore")]
    public void IsSipProtectedPath_returns_false_for_safe_paths(string path)
    {
        ShellMeta.IsSipProtectedPath(path).Should().BeFalse(
            because: $"'{path}' is NOT a SIP-protected path");
    }

    [Fact]
    public void IsSipProtectedPath_returns_false_for_empty_string()
    {
        ShellMeta.IsSipProtectedPath("").Should().BeFalse();
    }

    [Fact]
    public void IsSipProtectedPath_returns_false_for_relative_path()
    {
        // Relative paths cannot directly target SIP locations
        ShellMeta.IsSipProtectedPath("System/Library/foo").Should().BeFalse();
        ShellMeta.IsSipProtectedPath("bin/sh").Should().BeFalse();
    }

    [Fact]
    public void IsSipProtectedPath_usr_local_is_explicitly_allowed()
    {
        // /usr/local/ is carve-out — NOT protected even though /usr/bin/ is
        ShellMeta.IsSipProtectedPath("/usr/local/bin/my-tool").Should().BeFalse();
        ShellMeta.IsSipProtectedPath("/usr/local/lib/libfoo.so").Should().BeFalse();
    }
}
