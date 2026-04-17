using Xunit;
using NSubstitute;
using AuraCore.Application;
using AuraCore.Application.Interfaces.Platform;
using AuraCore.Module.PurgeableSpaceManager;
using AuraCore.Domain.Enums;
using AuraCore.Application.Interfaces.Modules;

namespace AuraCore.Tests.Module;

public class PurgeableSpaceManagerTests
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

    private static PurgeableSpaceManagerModule MakeModule(IShellCommandService? shell = null)
        => new PurgeableSpaceManagerModule(shell ?? MakeShell());

    // ---- Metadata ----

    [Fact]
    public void PurgeableSpaceManager_Metadata_IsValid()
    {
        var m = MakeModule();
        Assert.Equal("purgeable-space-manager", m.Id);
        Assert.False(string.IsNullOrWhiteSpace(m.DisplayName));
        Assert.Equal(OptimizationCategory.DiskCleanup, m.Category);
        Assert.Equal(RiskLevel.Medium, m.Risk);
        Assert.Equal(SupportedPlatform.MacOS, m.Platform);
        IOptimizationModule iface = m;
        Assert.False(iface.IsAdvanced);
    }

    // ---- Platform guard ----

    [Fact]
    public async Task PurgeableSpaceManager_Scan_OnNonMacOS_ReturnsBlocked()
    {
        if (OperatingSystem.IsMacOS()) return;
        var m = MakeModule();
        var r = await m.ScanAsync(new ScanOptions());
        Assert.False(r.Success);
        Assert.Equal("purgeable-space-manager", r.ModuleId);
    }

    // ---- Optimize: empty plan ----

    [Fact]
    public async Task PurgeableSpaceManager_Optimize_WithEmptyPlan_ReturnsSuccess()
    {
        var m = MakeModule();
        var plan = new OptimizationPlan(m.Id, new List<string>());
        var result = await m.OptimizeAsync(plan);
        Assert.Equal("purgeable-space-manager", result.ModuleId);
        Assert.NotEmpty(result.OperationId);
    }

    // ---- IShellCommandService: thin-snapshots success path ----

    [Fact]
    public async Task PurgeableSpaceManager_ThinSnapshots_Success_CountsProcessed()
    {
        if (!OperatingSystem.IsMacOS()) return;

        var shell = MakeShell(success: true, authResult: PrivilegeAuthResult.AlreadyAuthorized);
        var m = MakeModule(shell);

        var plan = new OptimizationPlan(m.Id, new List<string> { "thin-snapshots" });
        var result = await m.OptimizeAsync(plan);

        Assert.Equal(1, result.ItemsProcessed);
        Assert.True(result.Success);

        // Verify action id and argv passed to shell service
        await shell.Received(1).RunPrivilegedAsync(
            Arg.Is<PrivilegedCommand>(c =>
                c.Id == "purgeable" &&
                c.Arguments.Length == 4 &&
                c.Arguments[0] == "thinlocalsnapshots" &&
                c.Arguments[1] == "/" &&
                c.Arguments[3] == "1"),
            Arg.Any<CancellationToken>());
    }

    // ---- IShellCommandService: HelperMissing ----

    [Fact]
    public async Task PurgeableSpaceManager_ThinSnapshots_HelperMissing_ReportsNotProcessed()
    {
        if (!OperatingSystem.IsMacOS()) return;

        var shell = MakeShell(
            success: false,
            authResult: PrivilegeAuthResult.HelperMissing,
            exitCode: -1);
        var m = MakeModule(shell);

        var plan = new OptimizationPlan(m.Id, new List<string> { "thin-snapshots" });
        var result = await m.OptimizeAsync(plan);

        // When helper is missing the operation fails → not counted
        Assert.Equal(0, result.ItemsProcessed);
        Assert.True(result.Success); // Overall operation doesn't fail, just nothing processed

        await shell.Received(1).RunPrivilegedAsync(
            Arg.Is<PrivilegedCommand>(c =>
                c.Id == "purgeable"),
            Arg.Any<CancellationToken>());
    }

    // ---- IShellCommandService: Denied ----

    [Fact]
    public async Task PurgeableSpaceManager_ThinSnapshots_Denied_ReportsNotProcessed()
    {
        if (!OperatingSystem.IsMacOS()) return;

        var shell = MakeShell(
            success: false,
            authResult: PrivilegeAuthResult.Denied,
            exitCode: 1);
        var m = MakeModule(shell);

        var plan = new OptimizationPlan(m.Id, new List<string> { "thin-snapshots" });
        var result = await m.OptimizeAsync(plan);

        // When user denies the operation fails → not counted
        Assert.Equal(0, result.ItemsProcessed);
        Assert.True(result.Success); // Overall operation doesn't fail, just nothing processed

        await shell.Received(1).RunPrivilegedAsync(
            Arg.Is<PrivilegedCommand>(c =>
                c.Id == "purgeable"),
            Arg.Any<CancellationToken>());
    }

    // ---- Rollback ----

    [Fact]
    public async Task PurgeableSpaceManager_Rollback_IsNotSupported()
    {
        var m = MakeModule();
        var canRollback = await m.CanRollbackAsync("test-op-id");
        Assert.False(canRollback);
        // Should not throw
        await m.RollbackAsync("test-op-id");
    }
}
