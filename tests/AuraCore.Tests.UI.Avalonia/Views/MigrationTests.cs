using global::Avalonia.Headless.XUnit;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.Views;

/// <summary>
/// Phase 5.1 migration regression tests. Verifies the 4 pre-Phase-5 views
/// (Payment / Upgrade / Settings / Onboarding) render without XAML
/// resolution errors. Each test constructs the view in the
/// AvaloniaTestApplication and asserts the content tree is non-null
/// (XAML loaded successfully). After Phase 5.1.9 deletes the V1 bridge,
/// these tests guard against V1-only key leakage.
/// </summary>
public class MigrationTests
{
    [AvaloniaFact]
    public void PaymentView_Renders()
    {
        var view = new global::AuraCore.UI.Avalonia.Views.Pages.PaymentView();
        Assert.NotNull(view);
        Assert.NotNull(view.Content);
    }

    [AvaloniaFact]
    public void UpgradeView_Renders()
    {
        var view = new global::AuraCore.UI.Avalonia.Views.Pages.UpgradeView();
        Assert.NotNull(view);
        Assert.NotNull(view.Content);
    }

    [AvaloniaFact]
    public void SettingsView_Renders()
    {
        var view = new global::AuraCore.UI.Avalonia.Views.Pages.SettingsView();
        Assert.NotNull(view);
        Assert.NotNull(view.Content);
    }

    [AvaloniaFact]
    public void OnboardingView_Renders()
    {
        var view = new global::AuraCore.UI.Avalonia.Views.Pages.OnboardingView();
        Assert.NotNull(view);
        Assert.NotNull(view.Content);
    }
}
