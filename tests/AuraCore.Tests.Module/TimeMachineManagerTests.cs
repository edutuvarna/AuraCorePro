using Xunit;
using NSubstitute;
using AuraCore.Application;
using AuraCore.Application.Interfaces.Platform;
using AuraCore.Module.TimeMachineManager;
using AuraCore.Domain.Enums;
using AuraCore.Application.Interfaces.Modules;

namespace AuraCore.Tests.Module;

public class TimeMachineManagerTests
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

    private static TimeMachineManagerModule MakeModule(IShellCommandService? shell = null)
        => new TimeMachineManagerModule(shell ?? MakeShell());

    // ---- Metadata ----

    [Fact]
    public void TimeMachineManager_Metadata_IsValid()
    {
        var m = MakeModule();
        Assert.Equal("time-machine-manager", m.Id);
        Assert.False(string.IsNullOrWhiteSpace(m.DisplayName));
        Assert.Equal(OptimizationCategory.SystemHealth, m.Category);
        Assert.Equal(RiskLevel.Low, m.Risk);
        Assert.Equal(SupportedPlatform.MacOS, m.Platform);
        IOptimizationModule iface = m;
        Assert.False(iface.IsAdvanced);
    }

    // ---- Platform guard ----

    [Fact]
    public async Task TimeMachineManager_Scan_OnNonMacOS_ReturnsBlocked()
    {
        if (OperatingSystem.IsMacOS()) return;
        var m = MakeModule();
        var r = await m.ScanAsync(new ScanOptions());
        Assert.False(r.Success);
        Assert.Equal("time-machine-manager", r.ModuleId);
    }

    // ---- Optimize: empty plan ----

    [Fact]
    public async Task TimeMachineManager_Optimize_WithEmptyPlan_ReturnsSuccess()
    {
        var m = MakeModule();
        var plan = new OptimizationPlan(m.Id, new List<string>());
        var result = await m.OptimizeAsync(plan);
        Assert.Equal("time-machine-manager", result.ModuleId);
        Assert.NotEmpty(result.OperationId);
    }

    // ---- IShellCommandService: thinlocalsnapshots deferred (belongs to "purgeable" action) ----

    [Fact]
    public async Task TimeMachineManager_Optimize_ThinLocalSnapshots_Deferred_NotProcessed()
    {
        if (!OperatingSystem.IsMacOS()) return;

        var shell = MakeShell();
        var m = MakeModule(shell);

        var plan = new OptimizationPlan(m.Id, new List<string> { "delete-local-snapshots" });
        var result = await m.OptimizeAsync(plan);

        // thinlocalsnapshots is deferred (belongs to purgeable action, not time-machine)
        // so it should not be processed
        Assert.Equal(0, result.ItemsProcessed);
        Assert.True(result.Success);

        // Shell should not be invoked for deferred operation
        await shell.DidNotReceive().RunPrivilegedAsync(
            Arg.Any<PrivilegedCommand>(),
            Arg.Any<CancellationToken>());
    }

    // ---- IShellCommandService: delete backup deferred (forbidden verb) ----

    [Fact]
    public async Task TimeMachineManager_Optimize_DeleteBackup_Deferred_NotProcessed()
    {
        if (!OperatingSystem.IsMacOS()) return;

        var shell = MakeShell();
        var m = MakeModule(shell);

        var plan = new OptimizationPlan(m.Id, new List<string> { "delete-backup:/Volumes/Backup/2024-01-01" });
        var result = await m.OptimizeAsync(plan);

        // tmutil delete is explicitly forbidden by the validator, so deferred
        Assert.Equal(0, result.ItemsProcessed);
        Assert.True(result.Success);

        // Shell should not be invoked for deferred operation
        await shell.DidNotReceive().RunPrivilegedAsync(
            Arg.Any<PrivilegedCommand>(),
            Arg.Any<CancellationToken>());
    }

    // ---- IShellCommandService: delete old backups deferred (forbidden verb) ----

    [Fact]
    public async Task TimeMachineManager_Optimize_DeleteOldBackups_Deferred_NotProcessed()
    {
        if (!OperatingSystem.IsMacOS()) return;

        var shell = MakeShell();
        var m = MakeModule(shell);

        var plan = new OptimizationPlan(m.Id, new List<string> { "delete-old-backups:30" });
        var result = await m.OptimizeAsync(plan);

        // tmutil delete is explicitly forbidden, so deferred
        Assert.Equal(0, result.ItemsProcessed);
        Assert.True(result.Success);

        // Shell should not be invoked for deferred operation
        await shell.DidNotReceive().RunPrivilegedAsync(
            Arg.Any<PrivilegedCommand>(),
            Arg.Any<CancellationToken>());
    }

    // ---- Injection attempt rejection ----

    [Fact]
    public async Task TimeMachineManager_Optimize_InjectionAttempt_Rejected()
    {
        var m = MakeModule();
        var plan = new OptimizationPlan(m.Id, new List<string>
        {
            "delete-backup:/Volumes/Backup/foo;rm -rf /",
            "delete-old-backups:not-a-number",
            "delete-old-backups:-5",
            "unknown-item-id"
        });
        var result = await m.OptimizeAsync(plan);
        Assert.Equal("time-machine-manager", result.ModuleId);
        Assert.Equal(0, result.ItemsProcessed);
    }
}
