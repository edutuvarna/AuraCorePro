using System.Runtime.Versioning;
using AuraCore.Module.LinuxAppInstaller;
using AuraCore.UI.Avalonia.ViewModels;
using global::Avalonia.Controls;
using global::Avalonia.Interactivity;
using global::Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;

namespace AuraCore.UI.Avalonia.Views.Pages;

[SupportedOSPlatform("linux")]
public partial class LinuxAppInstallerView : UserControl
{
    public LinuxAppInstallerView()
    {
        InitializeComponent();
        ApplyLocalizedTexts();
        LocalizationService.LanguageChanged += OnLanguageChanged;
        Unloaded += OnUnloaded;
        Loaded += (_, _) =>
        {
            if (DataContext is null)
            {
                try
                {
                    var engine = App.Services.GetRequiredService<LinuxAppInstallerModule>();
                    DataContext = new LinuxAppInstallerViewModel(engine);
                }
                catch { /* design-time / tests without DI */ }
            }
        };
    }

    private void OnLanguageChanged() =>
        global::Avalonia.Threading.Dispatcher.UIThread.Post(ApplyLocalizedTexts);

    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        LocalizationService.LanguageChanged -= OnLanguageChanged;
    }

    private void ApplyLocalizedTexts()
    {
        string L(string k) => LocalizationService._(k);
        if (this.FindControl<Controls.ModuleHeader>("Header") is { } h)
        {
            h.Title = L("nav.linuxAppInstaller");
            h.Subtitle = L("linuxAppInstaller.subtitle");
        }
        if (this.FindControl<TextBlock>("PageTitle") is { } pt) pt.Text = L("nav.linuxAppInstaller");
        if (this.FindControl<TextBox>("SearchBox") is { } sbx) sbx.Watermark = L("linuxAppInstaller.search.placeholder");
        if (this.FindControl<Controls.StatCard>("TotalAppsCard") is { } tc) tc.Label = L("linuxAppInstaller.stat.total");
        if (this.FindControl<Controls.StatCard>("InstalledCard") is { } ic) ic.Label = L("linuxAppInstaller.stat.installed");
        if (this.FindControl<Controls.StatCard>("AvailableCard") is { } ac) ac.Label = L("linuxAppInstaller.stat.available");
        if (this.FindControl<Button>("ScanBtn") is { } sb) sb.Content = L("linuxAppInstaller.action.scan");
        if (this.FindControl<Button>("CancelBtn") is { } cb) cb.Content = L("linuxAppInstaller.action.cancel");
        if (this.FindControl<Button>("InstallSelectedBtn") is { } isb) isb.Content = L("linuxAppInstaller.action.installSelected");
        if (this.FindControl<TextBlock>("PrivilegeWarning") is { } pw) pw.Text = L("linuxAppInstaller.warning.privilege");
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
