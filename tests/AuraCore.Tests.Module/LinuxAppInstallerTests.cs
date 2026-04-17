using Xunit;
using NSubstitute;
using AuraCore.Application;
using AuraCore.Application.Interfaces.Platform;
using AuraCore.Module.LinuxAppInstaller;
using AuraCore.Module.LinuxAppInstaller.Models;
using AuraCore.Domain.Enums;
using AuraCore.Application.Interfaces.Modules;

namespace AuraCore.Tests.Module;

public class LinuxAppInstallerTests
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

    private static LinuxAppInstallerModule MakeModule(IShellCommandService? shell = null)
        => new LinuxAppInstallerModule(shell ?? MakeShell());

    // ---- Metadata ----

    [Fact]
    public void LinuxAppInstaller_Metadata_IsValid()
    {
        var m = MakeModule();
        Assert.Equal("linux-app-installer", m.Id);
        Assert.False(string.IsNullOrWhiteSpace(m.DisplayName));
        Assert.Equal(OptimizationCategory.ApplicationManagement, m.Category);
        Assert.Equal(RiskLevel.Low, m.Risk);
        Assert.Equal(SupportedPlatform.Linux, m.Platform);
        IOptimizationModule iface = m;
        Assert.False(iface.IsAdvanced);
    }

    [Fact]
    public void LinuxAppInstaller_Bundles_Has10Categories()
    {
        Assert.Equal(10, LinuxAppBundles.AllBundles.Count);
    }

    [Fact]
    public void LinuxAppInstaller_Bundles_HasAtLeast130Apps()
    {
        var total = LinuxAppBundles.AllBundles.Sum(b => b.Apps.Count);
        Assert.True(total >= 130, $"Expected >= 130 apps, got {total}");
    }

    [Fact]
    public void LinuxAppInstaller_AllAppIds_AreUnique()
    {
        var ids = LinuxAppBundles.AllBundles.SelectMany(b => b.Apps).Select(a => a.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    // ---- Platform guard ----

    [Fact]
    public async Task LinuxAppInstaller_Scan_OnNonLinux_ReturnsBlocked()
    {
        if (OperatingSystem.IsLinux()) return;
        var m = MakeModule();
        var r = await m.ScanAsync(new ScanOptions());
        Assert.False(r.Success);
        Assert.Equal("linux-app-installer", r.ModuleId);
    }

    // ---- Optimize: invalid items (input validation blocks bad packages) ----

    [Fact]
    public async Task LinuxAppInstaller_Optimize_InvalidItemId_NoCrash()
    {
        var shell = MakeShell(success: true);
        var m = MakeModule(shell);
        var plan = new OptimizationPlan(m.Id, new List<string>
        {
            "install:nonexistent-app",
            "unknown-action:firefox",
            "install:;rm -rf /",
            "no-colon"
        });
        var result = await m.OptimizeAsync(plan);
        Assert.Equal("linux-app-installer", result.ModuleId);
        Assert.Equal(0, result.ItemsProcessed);
    }

    // ---- apt install: Success ----

    [Fact]
    public async Task LinuxAppInstaller_AptInstall_Success_CountsProcessed()
    {
        if (!OperatingSystem.IsLinux()) return;

        var shell = MakeShell(success: true, authResult: PrivilegeAuthResult.AlreadyAuthorized);
        var m = MakeModule(shell);

        // Find a real apt app from bundles
        var aptApp = LinuxAppBundles.AllBundles
            .SelectMany(b => b.Apps)
            .First(a => a.Source == LinuxPackageSource.Apt);

        var plan = new OptimizationPlan(m.Id, new List<string>
        {
            $"install:{aptApp.Id}"
        });
        var result = await m.OptimizeAsync(plan);

        Assert.Equal(1, result.ItemsProcessed);

        // Verify action id and argv: ["apt", "install", "-y", packageName]
        await shell.Received(1).RunPrivilegedAsync(
            Arg.Is<PrivilegedCommand>(c =>
                c.Id == "app-installer" &&
                c.Arguments.Length >= 4 &&
                c.Arguments[0] == "apt" &&
                c.Arguments[1] == "install" &&
                c.Arguments[2] == "-y" &&
                c.Arguments[3] == aptApp.PackageName),
            Arg.Any<CancellationToken>());
    }

    // ---- apt install: HelperMissing ----

    [Fact]
    public async Task LinuxAppInstaller_AptInstall_HelperMissing_ReportsNotProcessed()
    {
        if (!OperatingSystem.IsLinux()) return;

        var shell = MakeShell(
            success: false,
            authResult: PrivilegeAuthResult.HelperMissing,
            exitCode: -1);
        var m = MakeModule(shell);

        var aptApp = LinuxAppBundles.AllBundles
            .SelectMany(b => b.Apps)
            .First(a => a.Source == LinuxPackageSource.Apt);

        var plan = new OptimizationPlan(m.Id, new List<string>
        {
            $"install:{aptApp.Id}"
        });
        var result = await m.OptimizeAsync(plan);

        Assert.Equal(0, result.ItemsProcessed);

        await shell.Received(1).RunPrivilegedAsync(
            Arg.Is<PrivilegedCommand>(c =>
                c.Id == "app-installer" &&
                c.Arguments[0] == "apt" &&
                c.Arguments[1] == "install"),
            Arg.Any<CancellationToken>());
    }

    // ---- apt install: Denied ----

    [Fact]
    public async Task LinuxAppInstaller_AptInstall_Denied_ReportsNotProcessed()
    {
        if (!OperatingSystem.IsLinux()) return;

        var shell = MakeShell(
            success: false,
            authResult: PrivilegeAuthResult.Denied,
            exitCode: 1);
        var m = MakeModule(shell);

        var aptApp = LinuxAppBundles.AllBundles
            .SelectMany(b => b.Apps)
            .First(a => a.Source == LinuxPackageSource.Apt);

        var plan = new OptimizationPlan(m.Id, new List<string>
        {
            $"install:{aptApp.Id}"
        });
        var result = await m.OptimizeAsync(plan);

        Assert.Equal(0, result.ItemsProcessed);
    }

    // ---- apt remove: Success ----

    [Fact]
    public async Task LinuxAppInstaller_AptRemove_Success_CountsProcessed()
    {
        if (!OperatingSystem.IsLinux()) return;

        var shell = MakeShell(success: true, authResult: PrivilegeAuthResult.AlreadyAuthorized);
        var m = MakeModule(shell);

        var aptApp = LinuxAppBundles.AllBundles
            .SelectMany(b => b.Apps)
            .First(a => a.Source == LinuxPackageSource.Apt);

        // For uninstall, package name strips flags (split on space, take first)
        var expectedPkg = aptApp.PackageName.Split(' ', 2)[0];

        var plan = new OptimizationPlan(m.Id, new List<string>
        {
            $"uninstall:{aptApp.Id}"
        });
        var result = await m.OptimizeAsync(plan);

        Assert.Equal(1, result.ItemsProcessed);

        // Verify argv: ["apt", "remove", "-y", pkgName]
        await shell.Received(1).RunPrivilegedAsync(
            Arg.Is<PrivilegedCommand>(c =>
                c.Id == "app-installer" &&
                c.Arguments.Length >= 4 &&
                c.Arguments[0] == "apt" &&
                c.Arguments[1] == "remove" &&
                c.Arguments[2] == "-y" &&
                c.Arguments[3] == expectedPkg),
            Arg.Any<CancellationToken>());
    }

    // ---- apt remove: HelperMissing ----

    [Fact]
    public async Task LinuxAppInstaller_AptRemove_HelperMissing_ReportsNotProcessed()
    {
        if (!OperatingSystem.IsLinux()) return;

        var shell = MakeShell(
            success: false,
            authResult: PrivilegeAuthResult.HelperMissing,
            exitCode: -1);
        var m = MakeModule(shell);

        var aptApp = LinuxAppBundles.AllBundles
            .SelectMany(b => b.Apps)
            .First(a => a.Source == LinuxPackageSource.Apt);

        var plan = new OptimizationPlan(m.Id, new List<string>
        {
            $"uninstall:{aptApp.Id}"
        });
        var result = await m.OptimizeAsync(plan);

        Assert.Equal(0, result.ItemsProcessed);
    }

    // ---- snap install: Success ----

    [Fact]
    public async Task LinuxAppInstaller_SnapInstall_Success_CountsProcessed()
    {
        if (!OperatingSystem.IsLinux()) return;

        var shell = MakeShell(success: true, authResult: PrivilegeAuthResult.AlreadyAuthorized);
        var m = MakeModule(shell);

        var snapApp = LinuxAppBundles.AllBundles
            .SelectMany(b => b.Apps)
            .First(a => a.Source == LinuxPackageSource.Snap);

        var plan = new OptimizationPlan(m.Id, new List<string>
        {
            $"install:{snapApp.Id}"
        });
        var result = await m.OptimizeAsync(plan);

        Assert.Equal(1, result.ItemsProcessed);

        // Verify argv: ["snap", "install", packageName]
        await shell.Received(1).RunPrivilegedAsync(
            Arg.Is<PrivilegedCommand>(c =>
                c.Id == "app-installer" &&
                c.Arguments.Length >= 3 &&
                c.Arguments[0] == "snap" &&
                c.Arguments[1] == "install" &&
                c.Arguments[2] == snapApp.PackageName),
            Arg.Any<CancellationToken>());
    }

    // ---- snap install: HelperMissing ----

    [Fact]
    public async Task LinuxAppInstaller_SnapInstall_HelperMissing_ReportsNotProcessed()
    {
        if (!OperatingSystem.IsLinux()) return;

        var shell = MakeShell(
            success: false,
            authResult: PrivilegeAuthResult.HelperMissing,
            exitCode: -1);
        var m = MakeModule(shell);

        var snapApp = LinuxAppBundles.AllBundles
            .SelectMany(b => b.Apps)
            .First(a => a.Source == LinuxPackageSource.Snap);

        var plan = new OptimizationPlan(m.Id, new List<string>
        {
            $"install:{snapApp.Id}"
        });
        var result = await m.OptimizeAsync(plan);

        Assert.Equal(0, result.ItemsProcessed);
    }

    // ---- snap install: Denied ----

    [Fact]
    public async Task LinuxAppInstaller_SnapInstall_Denied_ReportsNotProcessed()
    {
        if (!OperatingSystem.IsLinux()) return;

        var shell = MakeShell(
            success: false,
            authResult: PrivilegeAuthResult.Denied,
            exitCode: 1);
        var m = MakeModule(shell);

        var snapApp = LinuxAppBundles.AllBundles
            .SelectMany(b => b.Apps)
            .First(a => a.Source == LinuxPackageSource.Snap);

        var plan = new OptimizationPlan(m.Id, new List<string>
        {
            $"install:{snapApp.Id}"
        });
        var result = await m.OptimizeAsync(plan);

        Assert.Equal(0, result.ItemsProcessed);
    }

    // ---- snap remove: Success ----

    [Fact]
    public async Task LinuxAppInstaller_SnapRemove_Success_CountsProcessed()
    {
        if (!OperatingSystem.IsLinux()) return;

        var shell = MakeShell(success: true, authResult: PrivilegeAuthResult.AlreadyAuthorized);
        var m = MakeModule(shell);

        var snapApp = LinuxAppBundles.AllBundles
            .SelectMany(b => b.Apps)
            .First(a => a.Source == LinuxPackageSource.Snap);

        var expectedPkg = snapApp.PackageName.Split(' ', 2)[0];

        var plan = new OptimizationPlan(m.Id, new List<string>
        {
            $"uninstall:{snapApp.Id}"
        });
        var result = await m.OptimizeAsync(plan);

        Assert.Equal(1, result.ItemsProcessed);

        // Verify argv: ["snap", "remove", pkgName]
        await shell.Received(1).RunPrivilegedAsync(
            Arg.Is<PrivilegedCommand>(c =>
                c.Id == "app-installer" &&
                c.Arguments.Length >= 3 &&
                c.Arguments[0] == "snap" &&
                c.Arguments[1] == "remove" &&
                c.Arguments[2] == expectedPkg),
            Arg.Any<CancellationToken>());
    }

    // ---- snap remove: HelperMissing ----

    [Fact]
    public async Task LinuxAppInstaller_SnapRemove_HelperMissing_ReportsNotProcessed()
    {
        if (!OperatingSystem.IsLinux()) return;

        var shell = MakeShell(
            success: false,
            authResult: PrivilegeAuthResult.HelperMissing,
            exitCode: -1);
        var m = MakeModule(shell);

        var snapApp = LinuxAppBundles.AllBundles
            .SelectMany(b => b.Apps)
            .First(a => a.Source == LinuxPackageSource.Snap);

        var plan = new OptimizationPlan(m.Id, new List<string>
        {
            $"uninstall:{snapApp.Id}"
        });
        var result = await m.OptimizeAsync(plan);

        Assert.Equal(0, result.ItemsProcessed);
    }

    // ---- snap remove: Denied ----

    [Fact]
    public async Task LinuxAppInstaller_SnapRemove_Denied_ReportsNotProcessed()
    {
        if (!OperatingSystem.IsLinux()) return;

        var shell = MakeShell(
            success: false,
            authResult: PrivilegeAuthResult.Denied,
            exitCode: 1);
        var m = MakeModule(shell);

        var snapApp = LinuxAppBundles.AllBundles
            .SelectMany(b => b.Apps)
            .First(a => a.Source == LinuxPackageSource.Snap);

        var plan = new OptimizationPlan(m.Id, new List<string>
        {
            $"uninstall:{snapApp.Id}"
        });
        var result = await m.OptimizeAsync(plan);

        Assert.Equal(0, result.ItemsProcessed);
    }

    // ---- Flatpak paths: shell NOT called (flatpak is unprivileged) ----

    [Fact]
    public async Task LinuxAppInstaller_FlatpakInstall_DoesNotCallShellService()
    {
        if (!OperatingSystem.IsLinux()) return;

        var shell = MakeShell(success: true);
        var m = MakeModule(shell);

        var flatpakApp = LinuxAppBundles.AllBundles
            .SelectMany(b => b.Apps)
            .FirstOrDefault(a => a.Source == LinuxPackageSource.Flatpak);

        if (flatpakApp == null) return; // No flatpak apps in catalog — skip

        var plan = new OptimizationPlan(m.Id, new List<string>
        {
            $"install:{flatpakApp.Id}"
        });
        await m.OptimizeAsync(plan);

        // IShellCommandService must NOT be called for flatpak (unprivileged path)
        await shell.DidNotReceive().RunPrivilegedAsync(
            Arg.Any<PrivilegedCommand>(),
            Arg.Any<CancellationToken>());
    }

    // ---- Rollback ----

    [Fact]
    public async Task LinuxAppInstaller_Rollback_IsNotSupported()
    {
        var m = MakeModule();
        var canRollback = await m.CanRollbackAsync("test-op-id");
        Assert.False(canRollback);
        // Should not throw
        await m.RollbackAsync("test-op-id");
    }
}
