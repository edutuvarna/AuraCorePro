using System.Threading;
using System.Threading.Tasks;
using AuraCore.Application.Interfaces.Platform;
using AuraCore.Module.DefenderManager;
using NSubstitute;
using Xunit;

namespace AuraCore.Tests.Module;

public class DefenderManagerPrivilegedWritesTests
{
    private static IShellCommandService StubService(bool success = true, PrivilegeAuthResult auth = PrivilegeAuthResult.AlreadyAuthorized)
    {
        var svc = Substitute.For<IShellCommandService>();
        svc.RunPrivilegedAsync(Arg.Any<PrivilegedCommand>(), Arg.Any<CancellationToken>())
           .Returns(new ShellResult(success, success ? 0 : -1, "out", success ? "" : "err", auth));
        return svc;
    }

    [Theory]
    [InlineData("UpdateSignatures", "defender.update-signatures")]
    [InlineData("QuickScan",        "defender.scan-quick")]
    [InlineData("FullScan",         "defender.scan-full")]
    public async Task Simple_actions_route_to_correct_action_id(string methodName, string expectedId)
    {
        var svc = StubService();
        var module = new DefenderManagerModule(svc);

        var task = methodName switch
        {
            "UpdateSignatures" => module.UpdateSignaturesAsync(),
            "QuickScan"        => module.QuickScanAsync(),
            "FullScan"         => module.FullScanAsync(),
            _ => throw new System.ArgumentException()
        };
        await task;

        await svc.Received(1).RunPrivilegedAsync(
            Arg.Is<PrivilegedCommand>(c => c.Id == expectedId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetRealtimeAsync_maps_enabled_flag_to_enable_or_disable_arg()
    {
        var svc = StubService();
        var module = new DefenderManagerModule(svc);

        await module.SetRealtimeAsync(true);
        await svc.Received(1).RunPrivilegedAsync(
            Arg.Is<PrivilegedCommand>(c => c.Id == "defender.set-realtime" && c.Arguments[0] == "enable"),
            Arg.Any<CancellationToken>());

        await module.SetRealtimeAsync(false);
        await svc.Received(1).RunPrivilegedAsync(
            Arg.Is<PrivilegedCommand>(c => c.Id == "defender.set-realtime" && c.Arguments[0] == "disable"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AddExclusionAsync_passes_path_argument()
    {
        var svc = StubService();
        var module = new DefenderManagerModule(svc);
        await module.AddExclusionAsync(@"C:\Dev\MyRepo");

        await svc.Received(1).RunPrivilegedAsync(
            Arg.Is<PrivilegedCommand>(c => c.Id == "defender.add-exclusion" && c.Arguments[0] == @"C:\Dev\MyRepo"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RemoveThreatAsync_passes_numeric_id_argument()
    {
        var svc = StubService();
        var module = new DefenderManagerModule(svc);
        await module.RemoveThreatAsync("12345");

        await svc.Received(1).RunPrivilegedAsync(
            Arg.Is<PrivilegedCommand>(c => c.Id == "defender.remove-threat" && c.Arguments[0] == "12345"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Default_ctor_returns_helper_missing_outcome()
    {
        var module = new DefenderManagerModule();
        var result = await module.UpdateSignaturesAsync();

        Assert.False(result.Success);
        Assert.True(result.HelperMissing);
    }
}
