using System.Runtime.Versioning;
using AuraCore.Module.GrubManager;
using AuraCore.UI.Avalonia.ViewModels;
using global::Avalonia.Controls;
using global::Avalonia.Interactivity;
using global::Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;

namespace AuraCore.UI.Avalonia.Views.Pages;

[SupportedOSPlatform("linux")]
public partial class GrubManagerView : UserControl
{
    public GrubManagerView()
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
                    var engine = App.Services.GetRequiredService<GrubManagerModule>();
                    DataContext = new GrubManagerViewModel(engine);
                }
                catch { /* design-time / tests without DI */ }
            }
            // Auto-scan on load (user-requested tweak: no manual Scan button).
            (DataContext as GrubManagerViewModel)?.TriggerInitialScan();
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
            h.Title = L("nav.grubManager");
            h.Subtitle = L("grubManager.subtitle");
        }
        if (this.FindControl<TextBlock>("PageTitle") is { } pt) pt.Text = L("nav.grubManager");
        if (this.FindControl<TextBlock>("BackupKicker") is { } bk) bk.Text = L("grubManager.backup.kicker");
        if (this.FindControl<Button>("RollbackBtn") is { } rb) rb.Content = L("grubManager.action.rollback");
        if (this.FindControl<TextBlock>("SettingsHeading") is { } sh) sh.Text = L("grubManager.settings.heading");
        if (this.FindControl<TextBlock>("TimeoutLabel") is { } tl) tl.Text = L("grubManager.timeout.label");
        if (this.FindControl<TextBlock>("TimeoutHelp") is { } th) th.Text = L("grubManager.timeout.help");
        if (this.FindControl<TextBlock>("DefaultLabel") is { } dl) dl.Text = L("grubManager.default.label");
        if (this.FindControl<TextBlock>("DefaultHelp") is { } dh) dh.Text = L("grubManager.default.help");
        if (this.FindControl<TextBlock>("OsProberLabel") is { } ol) ol.Text = L("grubManager.osProber.label");
        if (this.FindControl<TextBlock>("OsProberHelp") is { } oh) oh.Text = L("grubManager.osProber.help");
        if (this.FindControl<TextBlock>("KernelsHeading") is { } kh) kh.Text = L("grubManager.kernels.heading");
        if (this.FindControl<TextBlock>("KernelsHelperText") is { } kht) kht.Text = L("grubManager.kernels.helperText");
        if (this.FindControl<TextBlock>("PendingHelpText") is { } pht) pht.Text = L("grubManager.pending.helpText");
        if (this.FindControl<CheckBox>("BackupAckCheckBox") is { } ack) ack.Content = L("grubManager.pending.acknowledge");
        if (this.FindControl<Button>("ResetBtn") is { } rs) rs.Content = L("grubManager.action.reset");
        if (this.FindControl<Button>("ApplyChangesBtn") is { } apb) apb.Content = L("grubManager.action.apply");
        if (this.FindControl<Button>("CancelBtn") is { } cb) cb.Content = L("grubManager.action.cancel");
        if (this.FindControl<TextBlock>("BootWarning") is { } bw) bw.Text = L("grubManager.warning.bootRisk");
        if (this.FindControl<TextBlock>("TimeoutMinLabel") is { } tml) tml.Text = L("grubManager.timeout.min");
        if (this.FindControl<TextBlock>("TimeoutMaxLabel") is { } tmx) tmx.Text = L("grubManager.timeout.max");
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
