using System.Threading;
using System.Threading.Tasks;
using AuraCore.Application.Interfaces.Platform;
using AuraCore.Module.DriverUpdater;
using NSubstitute;
using Xunit;

namespace AuraCore.Tests.Module;

public class DriverUpdaterPrivilegedWritesTests
{
    [Fact]
    public async Task ScanDevicesAsync_routes_to_shell_command_service_with_driver_scan_id()
    {
        var svc = Substitute.For<IShellCommandService>();
        svc.RunPrivilegedAsync(Arg.Any<PrivilegedCommand>(), Arg.Any<CancellationToken>())
           .Returns(new ShellResult(true, 0, "scanned", "", PrivilegeAuthResult.AlreadyAuthorized));

        var module = new DriverUpdaterModule(svc);
        var result = await module.ScanDevicesAsync();

        Assert.True(result.Success);
        Assert.False(result.HelperMissing);
        await svc.Received(1).RunPrivilegedAsync(
            Arg.Is<PrivilegedCommand>(c => c.Id == "driver.scan"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExportDriversAsync_passes_backup_path_as_argument()
    {
        var svc = Substitute.For<IShellCommandService>();
        svc.RunPrivilegedAsync(Arg.Any<PrivilegedCommand>(), Arg.Any<CancellationToken>())
           .Returns(new ShellResult(true, 0, "", "", PrivilegeAuthResult.AlreadyAuthorized));

        var module = new DriverUpdaterModule(svc);
        var path = @"C:\ProgramData\AuraCorePro\DriverBackup\manual-20260417";
        await module.ExportDriversAsync(path);

        await svc.Received(1).RunPrivilegedAsync(
            Arg.Is<PrivilegedCommand>(c => c.Id == "driver.export" && c.Arguments[0] == path),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ScanDevicesAsync_returns_helper_missing_from_shell_result()
    {
        var svc = Substitute.For<IShellCommandService>();
        svc.RunPrivilegedAsync(Arg.Any<PrivilegedCommand>(), Arg.Any<CancellationToken>())
           .Returns(new ShellResult(false, -3, "", "helper missing", PrivilegeAuthResult.HelperMissing));

        var module = new DriverUpdaterModule(svc);
        var result = await module.ScanDevicesAsync();

        Assert.False(result.Success);
        Assert.True(result.HelperMissing);
    }

    [Fact]
    public async Task ScanDevicesAsync_returns_helper_missing_when_service_not_wired()
    {
        var module = new DriverUpdaterModule(); // default ctor — no shell service

        var result = await module.ScanDevicesAsync();

        Assert.False(result.Success);
        Assert.True(result.HelperMissing);
    }
}
