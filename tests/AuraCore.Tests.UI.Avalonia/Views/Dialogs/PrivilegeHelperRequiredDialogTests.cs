using AuraCore.UI.Avalonia.Views.Dialogs;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.Views.Dialogs;

public class PrivilegeHelperRequiredDialogTests
{
    [AvaloniaFact]
    public void Dialog_RendersActionDescription_AndRemediation()
    {
        var d = new PrivilegeHelperRequiredDialog(
            "Flush RAM caches",
            "sudo bash /opt/auracorepro/install-privhelper.sh");

        var actionDesc = d.FindControl<TextBlock>("ActionDescText");
        var remediation = d.FindControl<SelectableTextBlock>("RemediationText");

        Assert.Equal("Flush RAM caches", actionDesc!.Text);
        Assert.Equal("sudo bash /opt/auracorepro/install-privhelper.sh", remediation!.Text);
    }

    [AvaloniaFact]
    public void Dialog_TitleAndButtons_Localized()
    {
        var d = new PrivilegeHelperRequiredDialog("anything", "anything");
        var title = d.FindControl<TextBlock>("TitleText");
        var closeBtn = d.FindControl<Button>("CloseButton");
        var tryAgainBtn = d.FindControl<Button>("TryAgainButton");

        Assert.False(string.IsNullOrEmpty(title!.Text));
        Assert.False(string.IsNullOrEmpty(closeBtn!.Content?.ToString()));
        Assert.False(string.IsNullOrEmpty(tryAgainBtn!.Content?.ToString()));
    }
}
