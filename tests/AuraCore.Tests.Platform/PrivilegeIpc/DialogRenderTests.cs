using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using AuraCore.UI.Avalonia.Views.Dialogs;
using FluentAssertions;

namespace AuraCore.Tests.Platform.PrivilegeIpc;

public class DialogRenderTests
{
    [AvaloniaFact]
    public void PrivilegeHelperInstallDialog_renders_without_throwing()
    {
        var dialog = new PrivilegeHelperInstallDialog();
        dialog.Should().NotBeNull();
        dialog.FindControl<Button>("InstallButton").Should().NotBeNull();
        dialog.FindControl<Button>("CancelButton").Should().NotBeNull();
    }

    [AvaloniaFact]
    public void PrivilegeHelperInstallDialog_exposes_InstallRequested_and_Cancelled_events()
    {
        var dialog = new PrivilegeHelperInstallDialog();
        bool installFired = false;
        bool cancelFired = false;

        dialog.InstallRequested += (_, _) => installFired = true;
        dialog.Cancelled += (_, _) => cancelFired = true;

        // Verify events are wired (just check the dialog is valid and events subscribe without throwing)
        dialog.Should().NotBeNull();
        installFired.Should().BeFalse();
        cancelFired.Should().BeFalse();
    }
}
