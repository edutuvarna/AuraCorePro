using Xunit;
using NSubstitute;
using AuraCore.Application;
using AuraCore.Application.Interfaces.Platform;
using AuraCore.Module.DnsFlusher;
using AuraCore.Domain.Enums;
using AuraCore.Application.Interfaces.Modules;

namespace AuraCore.Tests.Module;

public class DnsFlusherTests
{
    // ---- Helpers ----

    private static IShellCommandService MakeShell(
        bool success = true,
        PrivilegeAuthResult authResult = PrivilegeAuthResult.AlreadyAuthorized,
        int exitCode = 0,
        string stdout = "",
        string stderr = "")
    {
        var shell = Substitute.For<IShellCommandService>();
        shell.RunPrivilegedAsync(Arg.Any<PrivilegedCommand>(), Arg.Any<CancellationToken>())
            .Returns(new ShellResult(success, exitCode, stdout, stderr, authResult));
        return shell;
    }

    private static DnsFlusherModule MakeModule(IShellCommandService? shell = null)
        => new DnsFlusherModule(shell ?? MakeShell());

    // ---- Metadata ----

    [Fact]
    public void DnsFlusher_Metadata_IsValid()
    {
        var m = MakeModule();
        Assert.Equal("dns-flusher", m.Id);
        Assert.False(string.IsNullOrWhiteSpace(m.DisplayName));
        Assert.Equal(OptimizationCategory.NetworkOptimization, m.Category);
        Assert.Equal(RiskLevel.Low, m.Risk);
        Assert.Equal(SupportedPlatform.MacOS, m.Platform);
        IOptimizationModule iface = m;
        Assert.False(iface.IsAdvanced);
    }

    // ---- Platform guard ----

    [Fact]
    public async Task DnsFlusher_Scan_OnNonMacOS_ReturnsBlocked()
    {
        if (OperatingSystem.IsMacOS()) return;
        var m = MakeModule();
        var r = await m.ScanAsync(new ScanOptions());
        Assert.False(r.Success);
        Assert.Equal("dns-flusher", r.ModuleId);
    }

    // ---- Optimize: empty plan ----

    [Fact]
    public async Task DnsFlusher_Optimize_WithEmptyPlan_ReturnsSuccess()
    {
        var m = MakeModule();
        var plan = new OptimizationPlan(m.Id, new List<string>());
        var result = await m.OptimizeAsync(plan);
        Assert.Equal("dns-flusher", result.ModuleId);
        Assert.NotEmpty(result.OperationId);
    }

    // ---- IShellCommandService: flush success path ----

    [Fact]
    public async Task DnsFlusher_Flush_Success_CountsProcessed()
    {
        if (!OperatingSystem.IsMacOS()) return;

        var shell = MakeShell(success: true, authResult: PrivilegeAuthResult.AlreadyAuthorized);
        var m = MakeModule(shell);
        var plan = new OptimizationPlan(m.Id, new List<string> { "flush" });
        var result = await m.OptimizeAsync(plan);

        Assert.Equal(1, result.ItemsProcessed);
        Assert.True(result.Success);

        // Verify action id and argv passed to shell service
        await shell.Received(1).RunPrivilegedAsync(
            Arg.Is<PrivilegedCommand>(c =>
                c.Id == "dns-flush" &&
                (c.Arguments.Length == 0 ||
                 (c.Arguments.Length == 1 && c.Arguments[0] == "-flushcache"))),
            Arg.Any<CancellationToken>());
    }

    // ---- IShellCommandService: flush HelperMissing ----

    [Fact]
    public async Task DnsFlusher_Flush_HelperMissing_ReportsNotProcessed()
    {
        if (!OperatingSystem.IsMacOS()) return;

        var shell = MakeShell(
            success: false,
            authResult: PrivilegeAuthResult.HelperMissing,
            exitCode: -1);
        var m = MakeModule(shell);
        var plan = new OptimizationPlan(m.Id, new List<string> { "flush" });
        var result = await m.OptimizeAsync(plan);

        // When helper is missing the flush fails → not counted
        Assert.Equal(0, result.ItemsProcessed);
        Assert.True(result.Success); // Overall operation doesn't fail, just nothing processed

        await shell.Received(1).RunPrivilegedAsync(
            Arg.Is<PrivilegedCommand>(c =>
                c.Id == "dns-flush"),
            Arg.Any<CancellationToken>());
    }

    // ---- IShellCommandService: flush Denied ----

    [Fact]
    public async Task DnsFlusher_Flush_Denied_ReportsNotProcessed()
    {
        if (!OperatingSystem.IsMacOS()) return;

        var shell = MakeShell(
            success: false,
            authResult: PrivilegeAuthResult.Denied,
            exitCode: 1);
        var m = MakeModule(shell);
        var plan = new OptimizationPlan(m.Id, new List<string> { "flush" });
        var result = await m.OptimizeAsync(plan);

        // When user denies the flush fails → not counted
        Assert.Equal(0, result.ItemsProcessed);
        Assert.True(result.Success); // Overall operation doesn't fail, just nothing processed

        await shell.Received(1).RunPrivilegedAsync(
            Arg.Is<PrivilegedCommand>(c =>
                c.Id == "dns-flush"),
            Arg.Any<CancellationToken>());
    }

    // ---- Rollback ----

    [Fact]
    public async Task DnsFlusher_Rollback_IsNotSupported()
    {
        var m = MakeModule();
        var canRollback = await m.CanRollbackAsync("test-op-id");
        Assert.False(canRollback);
        // Should not throw
        await m.RollbackAsync("test-op-id");
    }
}
