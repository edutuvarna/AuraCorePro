using System.Runtime.Versioning;
using AuraCore.Module.SnapFlatpakCleaner;
using AuraCore.UI.Avalonia.ViewModels;
using global::Avalonia.Controls;
using global::Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;

namespace AuraCore.UI.Avalonia.Views.Pages;

[SupportedOSPlatform("linux")]
public partial class SnapFlatpakCleanerView : UserControl
{
    public SnapFlatpakCleanerView()
    {
        InitializeComponent();
        ApplyLocalizedTexts();
        Loaded += (_, _) =>
        {
            if (DataContext is null)
            {
                try
                {
                    var engine = App.Services.GetRequiredService<SnapFlatpakCleanerModule>();
                    DataContext = new SnapFlatpakCleanerViewModel(engine);
                }
                catch { /* design-time / tests without DI */ }
            }
        };
        LocalizationService.LanguageChanged += OnLanguageChanged;
        Unloaded += (_, _) => LocalizationService.LanguageChanged -= OnLanguageChanged;
    }

    private void OnLanguageChanged() =>
        global::Avalonia.Threading.Dispatcher.UIThread.Post(ApplyLocalizedTexts);

    private void ApplyLocalizedTexts()
    {
        var header = this.FindControl<Controls.ModuleHeader>("Header");
        if (header is not null)
        {
            header.Title = LocalizationService._("nav.snapFlatpakCleaner");
            header.Subtitle = LocalizationService._("snapFlatpakCleaner.subtitle");
        }
        if (this.FindControl<TextBlock>("PageTitle") is { } pt)
            pt.Text = LocalizationService._("nav.snapFlatpakCleaner");
        if (this.FindControl<Controls.StatCard>("SnapCard") is { } sc)
            sc.Label = LocalizationService._("snapFlatpakCleaner.stat.snap");
        if (this.FindControl<Controls.StatCard>("FlatpakCard") is { } fc)
            fc.Label = LocalizationService._("snapFlatpakCleaner.stat.flatpak");
        if (this.FindControl<Button>("ScanBtn") is { } sb)
            sb.Content = LocalizationService._("snapFlatpakCleaner.action.scan");
        if (this.FindControl<Button>("CancelBtn") is { } cb)
            cb.Content = LocalizationService._("snapFlatpakCleaner.action.cancel");
        if (this.FindControl<Button>("CleanSnapBtn") is { } cs)
            cs.Content = LocalizationService._("snapFlatpakCleaner.action.cleanSnap");
        if (this.FindControl<Button>("CleanFlatpakBtn") is { } cf)
            cf.Content = LocalizationService._("snapFlatpakCleaner.action.cleanFlatpak");
        if (this.FindControl<Button>("CleanBothBtn") is { } cboth)
            cboth.Content = LocalizationService._("snapFlatpakCleaner.action.cleanBoth");
        if (this.FindControl<TextBlock>("PrivilegeWarning") is { } pw)
            pw.Text = LocalizationService._("snapFlatpakCleaner.warning.privilege");
        if (this.FindControl<TextBlock>("CleanupHeading") is { } ch)
            ch.Text = LocalizationService._("snapFlatpakCleaner.action.cleanupActions");
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
