using Xunit;
using NSubstitute;
using AuraCore.Application;
using AuraCore.Application.Interfaces.Platform;
using AuraCore.Module.SpotlightManager;
using AuraCore.Domain.Enums;
using AuraCore.Application.Interfaces.Modules;

namespace AuraCore.Tests.Module;

public class SpotlightManagerTests
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

    private static SpotlightManagerModule MakeModule(IShellCommandService? shell = null)
        => new SpotlightManagerModule(shell ?? MakeShell());

    // ---- Metadata ----

    [Fact]
    public void SpotlightManager_Metadata_IsValid()
    {
        var m = MakeModule();
        Assert.Equal("spotlight-manager", m.Id);
        Assert.False(string.IsNullOrWhiteSpace(m.DisplayName));
        Assert.Equal(OptimizationCategory.SystemHealth, m.Category);
        Assert.Equal(RiskLevel.Low, m.Risk);
        Assert.Equal(SupportedPlatform.MacOS, m.Platform);
        IOptimizationModule iface = m;
        Assert.False(iface.IsAdvanced);
    }

    // ---- Platform guard ----

    [Fact]
    public async Task SpotlightManager_Scan_OnNonMacOS_ReturnsBlocked()
    {
        if (OperatingSystem.IsMacOS()) return;
        var m = MakeModule();
        var r = await m.ScanAsync(new ScanOptions());
        Assert.False(r.Success);
        Assert.Equal("spotlight-manager", r.ModuleId);
    }

    // ---- Optimize: empty plan ----

    [Fact]
    public async Task SpotlightManager_Optimize_WithEmptyPlan_ReturnsSuccess()
    {
        var m = MakeModule();
        var plan = new OptimizationPlan(m.Id, new List<string>());
        var result = await m.OptimizeAsync(plan);
        Assert.Equal("spotlight-manager", result.ModuleId);
        Assert.NotEmpty(result.OperationId);
    }

    // ---- IShellCommandService: disable success path ----

    [Fact]
    public async Task SpotlightManager_Disable_Success_CountsProcessed()
    {
        if (!OperatingSystem.IsMacOS()) return;

        var shell = MakeShell(success: true, authResult: PrivilegeAuthResult.AlreadyAuthorized);
        var m = MakeModule(shell);

        // First scan to populate known volumes
        var scanShell = MakeShell(
            success: true,
            stdout: "/Volumes/MyDisk:\n        Indexing enabled.\n");
        var scanModule = MakeModule(scanShell);
        await scanModule.ScanAsync(new ScanOptions());

        var plan = new OptimizationPlan(m.Id, new List<string> { "disable:/Volumes/MyDisk" });
        var result = await m.OptimizeAsync(plan);

        Assert.Equal(1, result.ItemsProcessed);
        Assert.True(result.Success);

        // Verify action id and argv passed to shell service
        await shell.Received(1).RunPrivilegedAsync(
            Arg.Is<PrivilegedCommand>(c =>
                c.Id == "spotlight" &&
                c.Arguments.Length == 3 &&
                c.Arguments[0] == "-i" &&
                c.Arguments[1] == "off" &&
                c.Arguments[2] == "/Volumes/MyDisk"),
            Arg.Any<CancellationToken>());
    }

    // ---- IShellCommandService: enable success path ----

    [Fact]
    public async Task SpotlightManager_Enable_Success_CountsProcessed()
    {
        if (!OperatingSystem.IsMacOS()) return;

        var shell = MakeShell(success: true, authResult: PrivilegeAuthResult.AlreadyAuthorized);
        var m = MakeModule(shell);

        // First scan to populate known volumes
        var scanShell = MakeShell(
            success: true,
            stdout: "/Volumes/Data:\n        Indexing disabled.\n");
        var scanModule = MakeModule(scanShell);
        await scanModule.ScanAsync(new ScanOptions());

        var plan = new OptimizationPlan(m.Id, new List<string> { "enable:/Volumes/Data" });
        var result = await m.OptimizeAsync(plan);

        Assert.Equal(1, result.ItemsProcessed);
        Assert.True(result.Success);

        // Verify action id and argv passed to shell service
        await shell.Received(1).RunPrivilegedAsync(
            Arg.Is<PrivilegedCommand>(c =>
                c.Id == "spotlight" &&
                c.Arguments.Length == 3 &&
                c.Arguments[0] == "-i" &&
                c.Arguments[1] == "on" &&
                c.Arguments[2] == "/Volumes/Data"),
            Arg.Any<CancellationToken>());
    }

    // ---- IShellCommandService: rebuild success path ----

    [Fact]
    public async Task SpotlightManager_Rebuild_Success_CountsProcessed()
    {
        if (!OperatingSystem.IsMacOS()) return;

        var shell = MakeShell(success: true, authResult: PrivilegeAuthResult.AlreadyAuthorized);
        var m = MakeModule(shell);

        // First scan to populate known volumes
        var scanShell = MakeShell(
            success: true,
            stdout: "/Users/me:\n        Indexing enabled.\n");
        var scanModule = MakeModule(scanShell);
        await scanModule.ScanAsync(new ScanOptions());

        var plan = new OptimizationPlan(m.Id, new List<string> { "rebuild:/Users/me" });
        var result = await m.OptimizeAsync(plan);

        Assert.Equal(1, result.ItemsProcessed);
        Assert.True(result.Success);

        // Verify action id and argv passed to shell service
        await shell.Received(1).RunPrivilegedAsync(
            Arg.Is<PrivilegedCommand>(c =>
                c.Id == "spotlight" &&
                c.Arguments.Length == 2 &&
                c.Arguments[0] == "-E" &&
                c.Arguments[2] == "/Users/me"),
            Arg.Any<CancellationToken>());
    }

    // ---- IShellCommandService: HelperMissing ----

    [Fact]
    public async Task SpotlightManager_Disable_HelperMissing_ReportsNotProcessed()
    {
        if (!OperatingSystem.IsMacOS()) return;

        var shell = MakeShell(
            success: false,
            authResult: PrivilegeAuthResult.HelperMissing,
            exitCode: -1);
        var m = MakeModule(shell);

        // First scan to populate known volumes
        var scanShell = MakeShell(
            success: true,
            stdout: "/Volumes/Test:\n        Indexing enabled.\n");
        var scanModule = MakeModule(scanShell);
        await scanModule.ScanAsync(new ScanOptions());

        var plan = new OptimizationPlan(m.Id, new List<string> { "disable:/Volumes/Test" });
        var result = await m.OptimizeAsync(plan);

        // When helper is missing the operation fails → not counted
        Assert.Equal(0, result.ItemsProcessed);
        Assert.True(result.Success); // Overall operation doesn't fail, just nothing processed

        await shell.Received(1).RunPrivilegedAsync(
            Arg.Is<PrivilegedCommand>(c =>
                c.Id == "spotlight"),
            Arg.Any<CancellationToken>());
    }

    // ---- IShellCommandService: Denied ----

    [Fact]
    public async Task SpotlightManager_Disable_Denied_ReportsNotProcessed()
    {
        if (!OperatingSystem.IsMacOS()) return;

        var shell = MakeShell(
            success: false,
            authResult: PrivilegeAuthResult.Denied,
            exitCode: 1);
        var m = MakeModule(shell);

        // First scan to populate known volumes
        var scanShell = MakeShell(
            success: true,
            stdout: "/Volumes/X:\n        Indexing enabled.\n");
        var scanModule = MakeModule(scanShell);
        await scanModule.ScanAsync(new ScanOptions());

        var plan = new OptimizationPlan(m.Id, new List<string> { "disable:/Volumes/X" });
        var result = await m.OptimizeAsync(plan);

        // When user denies the operation fails → not counted
        Assert.Equal(0, result.ItemsProcessed);
        Assert.True(result.Success); // Overall operation doesn't fail, just nothing processed

        await shell.Received(1).RunPrivilegedAsync(
            Arg.Is<PrivilegedCommand>(c =>
                c.Id == "spotlight"),
            Arg.Any<CancellationToken>());
    }

    // ---- Security: injection attempts ----

    [Fact]
    public async Task SpotlightManager_Optimize_InjectionAttempt_Rejected()
    {
        var m = MakeModule();
        var plan = new OptimizationPlan(m.Id, new List<string>
        {
            "disable:/Volumes/foo;rm -rf /",
            "unknown-action:/Volumes/X",
            "disable:",
            "no-colon"
        });
        var result = await m.OptimizeAsync(plan);
        Assert.Equal("spotlight-manager", result.ModuleId);
        Assert.Equal(0, result.ItemsProcessed);
    }

    // ---- Rollback ----

    [Fact]
    public async Task SpotlightManager_Rollback_IsNotSupported()
    {
        var m = MakeModule();
        var canRollback = await m.CanRollbackAsync("test-op-id");
        Assert.False(canRollback);
        // Should not throw
        await m.RollbackAsync("test-op-id");
    }
}
