using AuraCore.Application.Interfaces.Modules;
using AuraCore.Domain.Enums;
using AuraCore.UI.Avalonia.Views;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.Views;

public class UnavailableModuleViewTests
{
    [AvaloniaFact]
    public void SetState_WrongPlatform_ShowsModuleName_AndHidesRemediation()
    {
        var v = new UnavailableModuleView();
        v.SetState("Test Module",
            ModuleAvailability.WrongPlatform(SupportedPlatform.Linux));

        var moduleName = v.FindControl<TextBlock>("ModuleNameText");
        var category   = v.FindControl<TextBlock>("CategoryTitleText");
        var panel      = v.FindControl<Border>("RemediationPanel");

        Assert.Equal("Test Module", moduleName!.Text);
        Assert.False(string.IsNullOrEmpty(category!.Text));
        Assert.False(panel!.IsVisible);
    }

    [AvaloniaFact]
    public void SetState_HelperNotRunning_ShowsRemediationCommand()
    {
        var v = new UnavailableModuleView();
        v.SetState("Systemd Manager",
            ModuleAvailability.HelperNotRunning("sudo bash /opt/install.sh"));

        var panel = v.FindControl<Border>("RemediationPanel");
        var cmd   = v.FindControl<SelectableTextBlock>("RemediationText");

        Assert.True(panel!.IsVisible);
        Assert.Equal("sudo bash /opt/install.sh", cmd!.Text);
    }

    [AvaloniaFact]
    public void SetState_NoTryAgain_HidesTryAgainButton()
    {
        var v = new UnavailableModuleView();
        v.SetState("Test", ModuleAvailability.WrongPlatform(SupportedPlatform.Linux), onTryAgain: null);

        var btn = v.FindControl<Button>("TryAgainButton");
        Assert.False(btn!.IsVisible);
    }
}
