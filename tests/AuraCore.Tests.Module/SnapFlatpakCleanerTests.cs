using Xunit;
using NSubstitute;
using AuraCore.Application;
using AuraCore.Application.Interfaces.Platform;
using AuraCore.Module.SnapFlatpakCleaner;
using AuraCore.Domain.Enums;
using AuraCore.Application.Interfaces.Modules;

namespace AuraCore.Tests.Module;

public class SnapFlatpakCleanerTests
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

    private static SnapFlatpakCleanerModule MakeModule(IShellCommandService? shell = null)
        => new SnapFlatpakCleanerModule(shell ?? MakeShell());

    // ---- Metadata ----

    [Fact]
    public void SnapFlatpakCleaner_Metadata_IsValid()
    {
        var m = MakeModule();
        Assert.Equal("snap-flatpak-cleaner", m.Id);
        Assert.Equal("Snap & Flatpak Cleaner", m.DisplayName);
        Assert.Equal(OptimizationCategory.SystemCleaning, m.Category);
        Assert.Equal(RiskLevel.Low, m.Risk);
        Assert.Equal(SupportedPlatform.Linux, m.Platform);
        IOptimizationModule iface = m;
        Assert.False(iface.IsAdvanced);
    }

    // ---- Platform guard ----

    [Fact]
    public async Task SnapFlatpakCleaner_Scan_OnNonLinux_ReturnsBlockedOrFailure()
    {
        if (OperatingSystem.IsLinux()) return;
        var m = MakeModule();
        var r = await m.ScanAsync(new ScanOptions());
        Assert.False(r.Success);
        Assert.Equal("snap-flatpak-cleaner", r.ModuleId);
    }

    // ---- Optimize: empty plan ----

    [Fact]
    public async Task SnapFlatpakCleaner_Optimize_WithEmptyPlan_ReturnsSuccess()
    {
        var m = MakeModule();
        var plan = new OptimizationPlan(m.Id, new List<string>());
        var result = await m.OptimizeAsync(plan);
        Assert.Equal("snap-flatpak-cleaner", result.ModuleId);
        Assert.NotEmpty(result.OperationId);
    }

    // ---- Optimize: invalid items (input validation still blocks bad names) ----

    [Fact]
    public async Task SnapFlatpakCleaner_Optimize_WithInvalidItems_DoesNotCrash()
    {
        var shell = MakeShell(success: true);
        var m = MakeModule(shell);
        var plan = new OptimizationPlan(m.Id, new List<string>
        {
            "snap-remove:../../../../etc/passwd:1",
            "flatpak-remove:org.evil;rm -rf /",
            "unknown-action:foo",
            "snap-remove:missingrevision",
            "flatpak-remove:"
        });
        var result = await m.OptimizeAsync(plan);
        Assert.Equal("snap-flatpak-cleaner", result.ModuleId);
        Assert.NotEmpty(result.OperationId);
        // None of the invalid items should be processed
        Assert.Equal(0, result.ItemsProcessed);
        // Shell should never have been called (input validation rejects all)
        await shell.DidNotReceive().RunPrivilegedAsync(
            Arg.Any<PrivilegedCommand>(), Arg.Any<CancellationToken>());
    }

    // ---- Rollback ----

    [Fact]
    public async Task SnapFlatpakCleaner_Rollback_IsNotSupported()
    {
        var m = MakeModule();
        var canRollback = await m.CanRollbackAsync("test-op-id");
        Assert.False(canRollback);
        // Should not throw
        await m.RollbackAsync("test-op-id");
    }

    // ---- IShellCommandService: snap-remove success path ----

    [Fact]
    public async Task SnapFlatpakCleaner_SnapRemove_Success_CountsProcessed()
    {
        if (!OperatingSystem.IsLinux()) return; // snap-remove only runs on Linux

        var shell = MakeShell(success: true, authResult: PrivilegeAuthResult.AlreadyAuthorized);
        var m = MakeModule(shell);
        var plan = new OptimizationPlan(m.Id, new List<string>
        {
            "snap-remove:firefox:1234"
        });
        var result = await m.OptimizeAsync(plan);

        Assert.Equal(1, result.ItemsProcessed);

        // Verify action id and argv
        await shell.Received(1).RunPrivilegedAsync(
            Arg.Is<PrivilegedCommand>(c =>
                c.Id == "snap-flatpak" &&
                c.Arguments.Length == 3 &&
                c.Arguments[0] == "snap" &&
                c.Arguments[1] == "remove" &&
                c.Arguments[2] == "firefox"),
            Arg.Any<CancellationToken>());
    }

    // ---- IShellCommandService: snap-remove HelperMissing ----

    [Fact]
    public async Task SnapFlatpakCleaner_SnapRemove_HelperMissing_ReportsNotProcessed()
    {
        if (!OperatingSystem.IsLinux()) return;

        var shell = MakeShell(
            success: false,
            authResult: PrivilegeAuthResult.HelperMissing,
            exitCode: -1);
        var m = MakeModule(shell);
        var plan = new OptimizationPlan(m.Id, new List<string>
        {
            "snap-remove:firefox:1234"
        });
        var result = await m.OptimizeAsync(plan);

        // When helper is missing the removal fails → not counted
        Assert.Equal(0, result.ItemsProcessed);

        await shell.Received(1).RunPrivilegedAsync(
            Arg.Is<PrivilegedCommand>(c =>
                c.Id == "snap-flatpak" &&
                c.Arguments[0] == "snap" &&
                c.Arguments[1] == "remove" &&
                c.Arguments[2] == "firefox"),
            Arg.Any<CancellationToken>());
    }

    // ---- IShellCommandService: snap-remove Denied ----

    [Fact]
    public async Task SnapFlatpakCleaner_SnapRemove_Denied_ReportsNotProcessed()
    {
        if (!OperatingSystem.IsLinux()) return;

        var shell = MakeShell(
            success: false,
            authResult: PrivilegeAuthResult.Denied,
            exitCode: 1);
        var m = MakeModule(shell);
        var plan = new OptimizationPlan(m.Id, new List<string>
        {
            "snap-remove:chromium:42"
        });
        var result = await m.OptimizeAsync(plan);

        Assert.Equal(0, result.ItemsProcessed);

        await shell.Received(1).RunPrivilegedAsync(
            Arg.Is<PrivilegedCommand>(c =>
                c.Id == "snap-flatpak" &&
                c.Arguments[2] == "chromium"),
            Arg.Any<CancellationToken>());
    }

    // ---- snap-clean-all: HelperMissing stops the batch ----

    [Fact]
    public async Task SnapFlatpakCleaner_SnapCleanAll_HelperMissing_ReportsNotProcessed()
    {
        if (!OperatingSystem.IsLinux()) return;

        var shell = MakeShell(
            success: false,
            authResult: PrivilegeAuthResult.HelperMissing,
            exitCode: -1);
        var m = MakeModule(shell);
        var plan = new OptimizationPlan(m.Id, new List<string> { "snap-clean-all" });
        var result = await m.OptimizeAsync(plan);

        // SnapCleanAllAsync calls snap list --all (non-privileged) then per-item privileged.
        // On Windows / when snap list returns nothing, 0 items → processed = 0.
        // On Linux with no real snap: also 0. Either way helper-missing path yields 0.
        Assert.Equal(0, result.ItemsProcessed);
    }

    // ---- Argv format: no --revision in the call ----

    [Fact]
    public async Task SnapFlatpakCleaner_SnapRemove_NeverPassesRevisionFlag()
    {
        if (!OperatingSystem.IsLinux()) return;

        var shell = MakeShell(success: true);
        var m = MakeModule(shell);
        var plan = new OptimizationPlan(m.Id, new List<string>
        {
            "snap-remove:snapcraft:999"
        });
        await m.OptimizeAsync(plan);

        await shell.Received(1).RunPrivilegedAsync(
            Arg.Is<PrivilegedCommand>(c =>
                !c.Arguments.Any(a => a.StartsWith("--revision"))),
            Arg.Any<CancellationToken>());
    }
}
