using AuraCore.Application.Interfaces.Platform;
using AuraCore.Infrastructure.PrivilegeIpc.Linux;

namespace AuraCore.UI.Avalonia.Views;

/// <summary>
/// Small testable seam between the install-button handlers (MainWindow,
/// PrivilegeHelperInstallDialog) and PrivHelperInstaller. Keeps
/// MainWindow.axaml.cs thin and lets us unit-test the success/failure
/// branches without an Avalonia application host.
/// </summary>
public static class PrivilegeInstallCoordinator
{
    public static async Task<InstallOutcome> RunInstallFlowAsync(
        PrivHelperInstaller? installer,
        IHelperAvailabilityService availability,
        CancellationToken ct = default)
    {
        if (installer is null)
        {
            return new InstallOutcome(
                Success: false, ExitCode: -10,
                Stdout: string.Empty,
                Stderr: "Privilege helper install is not supported on this platform or build.",
                StageDir: null);
        }

        var outcome = await installer.ExtractAndInstallAsync(ct);
        if (outcome.Success)
        {
            availability.ReportAvailable();
        }
        return outcome;
    }
}
