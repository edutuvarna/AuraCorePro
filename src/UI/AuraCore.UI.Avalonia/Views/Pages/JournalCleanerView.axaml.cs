using System.Runtime.Versioning;
using AuraCore.Module.JournalCleaner;
using AuraCore.UI.Avalonia.ViewModels;
using global::Avalonia.Controls;
using global::Avalonia.Interactivity;
using global::Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;

namespace AuraCore.UI.Avalonia.Views.Pages;

[SupportedOSPlatform("linux")]
public partial class JournalCleanerView : UserControl
{
    public JournalCleanerView()
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
                    var engine = App.Services.GetRequiredService<JournalCleanerModule>();
                    DataContext = new JournalCleanerViewModel(engine);
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
        var header = this.FindControl<Controls.ModuleHeader>("Header");
        if (header is not null)
        {
            header.Title = LocalizationService._("nav.journalCleaner");
            header.Subtitle = LocalizationService._("journalCleaner.subtitle");
        }
        if (this.FindControl<TextBlock>("PageTitle") is { } pt)
            pt.Text = LocalizationService._("nav.journalCleaner");
        if (this.FindControl<Controls.StatCard>("UsageCard") is { } uc)
            uc.Label = LocalizationService._("journalCleaner.stat.usage");
        if (this.FindControl<Controls.StatCard>("FilesCard") is { } fc)
            fc.Label = LocalizationService._("journalCleaner.stat.files");
        if (this.FindControl<Controls.StatCard>("OldestCard") is { } oc)
            oc.Label = LocalizationService._("journalCleaner.stat.oldest");
        if (this.FindControl<TextBlock>("VacuumHeading") is { } vh)
            vh.Text = LocalizationService._("journalCleaner.vacuum.heading");
        if (this.FindControl<Button>("ScanBtn") is { } sb)
            sb.Content = LocalizationService._("journalCleaner.action.scan");
        if (this.FindControl<Button>("CancelBtn") is { } cb)
            cb.Content = LocalizationService._("journalCleaner.action.cancel");
        if (this.FindControl<Button>("Vacuum500Btn") is { } v5)
            v5.Content = LocalizationService._("journalCleaner.action.vacuum500m");
        if (this.FindControl<Button>("Vacuum1GBtn") is { } v1)
            v1.Content = LocalizationService._("journalCleaner.action.vacuum1g");
        if (this.FindControl<Button>("Vacuum7DaysBtn") is { } v7)
            v7.Content = LocalizationService._("journalCleaner.action.vacuum7days");
        if (this.FindControl<Button>("Vacuum30DaysBtn") is { } v30)
            v30.Content = LocalizationService._("journalCleaner.action.vacuum30days");
        if (this.FindControl<TextBlock>("PrivilegeWarning") is { } pw)
            pw.Text = LocalizationService._("journalCleaner.warning.privilege");
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
