using AuraCore.Module.KernelCleaner;
using AuraCore.UI.Avalonia.ViewModels;
using global::Avalonia.Controls;
using global::Avalonia.Interactivity;
using global::Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;

namespace AuraCore.UI.Avalonia.Views.Pages;

public partial class KernelCleanerView : UserControl
{
    public KernelCleanerView()
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
                    var engine = App.Services.GetRequiredService<KernelCleanerModule>();
                    DataContext = new KernelCleanerViewModel(engine);
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
            h.Title = L("nav.kernelCleaner");
            h.Subtitle = L("kernelCleaner.subtitle");
        }
        if (this.FindControl<TextBlock>("PageTitle") is { } pt) pt.Text = L("nav.kernelCleaner");
        if (this.FindControl<Controls.StatCard>("ActiveKernelCard") is { } ac) ac.Label = L("kernelCleaner.stat.active");
        if (this.FindControl<Controls.StatCard>("RemovableCard") is { } rc) rc.Label = L("kernelCleaner.stat.removable");
        if (this.FindControl<Button>("ScanBtn") is { } sb) sb.Content = L("kernelCleaner.action.scan");
        if (this.FindControl<Button>("CancelBtn") is { } cb) cb.Content = L("kernelCleaner.action.cancel");
        if (this.FindControl<TextBlock>("SafeKicker") is { } sk) sk.Text = L("kernelCleaner.safe.kicker");
        if (this.FindControl<Button>("AutoRemoveOldBtn") is { } arb) arb.Content = L("kernelCleaner.safe.action");
        if (this.FindControl<TextBlock>("ManualHeading") is { } mh) mh.Text = L("kernelCleaner.manual.heading");
        if (this.FindControl<Button>("RemoveSelectedBtn") is { } rsb) rsb.Content = L("kernelCleaner.action.removeSelected");
        if (this.FindControl<TextBlock>("DangerKicker") is { } dk) dk.Text = L("kernelCleaner.danger.kicker");
        if (this.FindControl<TextBlock>("NoFallbackWarning") is { } nfw) nfw.Text = L("kernelCleaner.warning.noFallback");
        if (this.FindControl<CheckBox>("DangerAckCheckBox") is { } dck) dck.Content = L("kernelCleaner.danger.acknowledge");
        if (this.FindControl<Button>("RemoveAllButCurrentBtn") is { } rabc) rabc.Content = L("kernelCleaner.action.removeAllButRunning");
        if (this.FindControl<TextBlock>("PrivilegeWarning") is { } pw) pw.Text = L("kernelCleaner.warning.privilege");
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
