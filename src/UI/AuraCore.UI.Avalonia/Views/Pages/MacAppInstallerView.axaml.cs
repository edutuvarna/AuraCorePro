using System.Runtime.Versioning;
using AuraCore.Module.MacAppInstaller;
using AuraCore.UI.Avalonia.ViewModels;
using global::Avalonia.Controls;
using global::Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;

namespace AuraCore.UI.Avalonia.Views.Pages;

[SupportedOSPlatform("macos")]
public partial class MacAppInstallerView : UserControl
{
    public MacAppInstallerView()
    {
        InitializeComponent();
        ApplyLocalizedTexts();
        Loaded += (_, _) =>
        {
            if (DataContext is null)
            {
                try
                {
                    var engine = App.Services.GetRequiredService<MacAppInstallerModule>();
                    DataContext = new MacAppInstallerViewModel(engine);
                }
                catch { /* design-time / tests without DI */ }
            }
            (DataContext as MacAppInstallerViewModel)?.TriggerInitialScan();
        };
    }

    private void ApplyLocalizedTexts()
    {
        string L(string k) => LocalizationService._(k);
        if (this.FindControl<Controls.ModuleHeader>("Header") is { } h)
        {
            h.Title = L("nav.macAppInstaller");
            h.Subtitle = L("macAppInstaller.subtitle");
        }
        if (this.FindControl<TextBlock>("PageTitle") is { } pt) pt.Text = L("nav.macAppInstaller");
        if (this.FindControl<TextBox>("SearchBox") is { } sbx) sbx.Watermark = L("macAppInstaller.search.placeholder");
        if (this.FindControl<Controls.StatCard>("TotalAppsCard") is { } tc) tc.Label = L("macAppInstaller.stat.total");
        if (this.FindControl<Controls.StatCard>("InstalledCard") is { } ic) ic.Label = L("macAppInstaller.stat.installed");
        if (this.FindControl<Controls.StatCard>("AvailableCard") is { } ac) ac.Label = L("macAppInstaller.stat.available");
        if (this.FindControl<Button>("ScanBtn") is { } sb) sb.Content = L("macAppInstaller.action.scan");
        if (this.FindControl<Button>("CancelBtn") is { } cb) cb.Content = L("macAppInstaller.action.cancel");
        if (this.FindControl<Button>("InstallSelectedBtn") is { } isb) isb.Content = L("macAppInstaller.action.installSelected");
        if (this.FindControl<TextBlock>("PrivilegeWarning") is { } pw) pw.Text = L("macAppInstaller.warning.privilege");
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
