using System.Threading;
using System.Threading.Tasks;
using AuraCore.Application.Interfaces.Platform;
using AuraCore.Module.SymlinkManager;
using NSubstitute;
using Xunit;

namespace AuraCore.Tests.Module;

public class SymlinkCreateModeTests
{
    [Fact]
    public async Task CreateSymlinkAsync_dispatches_symlink_create_action()
    {
        var svc = Substitute.For<IShellCommandService>();
        svc.RunPrivilegedAsync(Arg.Any<PrivilegedCommand>(), Arg.Any<CancellationToken>())
           .Returns(new ShellResult(true, 0, "", "", PrivilegeAuthResult.AlreadyAuthorized));

        var module = new SymlinkManagerModule(svc);
        var outcome = await module.CreateSymlinkAsync("/usr/local/bin/mytool", "/opt/mytool/bin/tool");

        Assert.True(outcome.Success);
        await svc.Received(1).RunPrivilegedAsync(
            Arg.Is<PrivilegedCommand>(c => c.Id == "symlink.create"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateSymlinkAsync_passes_correct_ln_ordering()
    {
        // POSIX ln: ln -s -f -- <target> <linkname>
        // source = linkname, target = what it points to
        var svc = Substitute.For<IShellCommandService>();
        svc.RunPrivilegedAsync(Arg.Any<PrivilegedCommand>(), Arg.Any<CancellationToken>())
           .Returns(new ShellResult(true, 0, "", "", PrivilegeAuthResult.AlreadyAuthorized));

        var module = new SymlinkManagerModule(svc);
        var source = "/usr/local/bin/mytool";
        var target = "/opt/mytool/bin/tool";
        await module.CreateSymlinkAsync(source, target);

        await svc.Received(1).RunPrivilegedAsync(
            Arg.Is<PrivilegedCommand>(c =>
                c.Id == "symlink.create" &&
                c.Arguments.Length == 5 &&
                c.Arguments[0] == "-s" &&
                c.Arguments[1] == "-f" &&
                c.Arguments[2] == "--" &&
                c.Arguments[3] == target &&   // target is what the link points AT
                c.Arguments[4] == source),    // source is the link name
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateSymlinkAsync_rejects_empty_source()
    {
        var svc = Substitute.For<IShellCommandService>();
        var module = new SymlinkManagerModule(svc);

        var outcome = await module.CreateSymlinkAsync("", "/opt/tool");
        Assert.False(outcome.Success);
        Assert.NotNull(outcome.Error);

        await svc.DidNotReceive().RunPrivilegedAsync(Arg.Any<PrivilegedCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateSymlinkAsync_rejects_empty_target()
    {
        var svc = Substitute.For<IShellCommandService>();
        var module = new SymlinkManagerModule(svc);

        var outcome = await module.CreateSymlinkAsync("/usr/local/bin/mytool", "");
        Assert.False(outcome.Success);

        await svc.DidNotReceive().RunPrivilegedAsync(Arg.Any<PrivilegedCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateSymlinkAsync_returns_not_wired_error_when_service_null()
    {
        var module = new SymlinkManagerModule(); // default ctor — no shell service
        var outcome = await module.CreateSymlinkAsync("/a", "/b");
        Assert.False(outcome.Success);
        Assert.Contains("not wired", outcome.Error ?? "", System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateSymlinkAsync_propagates_stderr_on_failure()
    {
        var svc = Substitute.For<IShellCommandService>();
        svc.RunPrivilegedAsync(Arg.Any<PrivilegedCommand>(), Arg.Any<CancellationToken>())
           .Returns(new ShellResult(false, 1, "", "ln: failed to create symbolic link: File exists", PrivilegeAuthResult.AlreadyAuthorized));

        var module = new SymlinkManagerModule(svc);
        var outcome = await module.CreateSymlinkAsync("/usr/local/bin/mytool", "/opt/tool");

        Assert.False(outcome.Success);
        Assert.Contains("File exists", outcome.Error ?? "");
    }
}
