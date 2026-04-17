using System.Threading;
using System.Threading.Tasks;
using AuraCore.Application.Interfaces.Platform;
using AuraCore.Module.ServiceManager;
using NSubstitute;
using Xunit;

namespace AuraCore.Tests.Module;

public class ServiceManagerEngineTests
{
    private static IShellCommandService StubShell(bool success = true, PrivilegeAuthResult auth = PrivilegeAuthResult.AlreadyAuthorized)
    {
        var svc = Substitute.For<IShellCommandService>();
        svc.RunPrivilegedAsync(Arg.Any<PrivilegedCommand>(), Arg.Any<CancellationToken>())
           .Returns(new ShellResult(success, success ? 0 : -1, "out", success ? "" : "err", auth));
        return svc;
    }

    [Theory]
    [InlineData("Start",   "service.start")]
    [InlineData("Stop",    "service.stop")]
    [InlineData("Restart", "service.restart")]
    public async Task Start_Stop_Restart_dispatch_correct_action_ids(string methodName, string expectedId)
    {
        var svc = StubShell();
        var engine = new ServiceManagerEngine(svc);

        var task = methodName switch
        {
            "Start"   => engine.StartAsync("winmgmt"),
            "Stop"    => engine.StopAsync("winmgmt"),
            "Restart" => engine.RestartAsync("winmgmt"),
            _ => throw new System.ArgumentException()
        };
        await task;

        await svc.Received(1).RunPrivilegedAsync(
            Arg.Is<PrivilegedCommand>(c => c.Id == expectedId && c.Arguments[0] == "winmgmt"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetStartupAsync_rejects_unknown_mode()
    {
        var svc = StubShell();
        var engine = new ServiceManagerEngine(svc);

        var result = await engine.SetStartupAsync("winmgmt", "not-a-mode");

        Assert.False(result.Success);
        Assert.Contains("invalid mode", result.Error ?? "");
        await svc.DidNotReceive().RunPrivilegedAsync(Arg.Any<PrivilegedCommand>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("auto")]
    [InlineData("demand")]
    [InlineData("disabled")]
    public async Task SetStartupAsync_accepts_three_valid_modes(string mode)
    {
        var svc = StubShell();
        var engine = new ServiceManagerEngine(svc);

        var result = await engine.SetStartupAsync("winmgmt", mode);
        Assert.True(result.Success);
        await svc.Received(1).RunPrivilegedAsync(
            Arg.Is<PrivilegedCommand>(c => c.Id == "service.set-startup" && c.Arguments[1] == mode),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Default_ctor_start_returns_helper_missing()
    {
        var engine = new ServiceManagerEngine();
        var result = await engine.StartAsync("winmgmt");
        Assert.False(result.Success);
        Assert.True(result.HelperMissing);
    }
}
