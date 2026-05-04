using System.Linq;
using System.Runtime.Versioning;
using AuraCore.Module.XcodeCleaner;
using AuraCore.UI.Avalonia.ViewModels;
using global::Avalonia.Controls;
using global::Avalonia.Markup.Xaml;
using global::Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;

namespace AuraCore.UI.Avalonia.Views.Pages;

[SupportedOSPlatform("macos")]
public partial class XcodeCleanerView : UserControl
{
    public XcodeCleanerView()
    {
        InitializeComponent();
        ApplyLocalizedTexts();
        Loaded += (_, _) =>
        {
            if (DataContext is null)
            {
                try { DataContext = new XcodeCleanerViewModel(App.Services.GetRequiredService<XcodeCleanerModule>()); }
                catch { /* design-time / tests without DI */ }
            }
            (DataContext as XcodeCleanerViewModel)?.TriggerInitialScan();
        };
    }

    private void ApplyLocalizedTexts()
    {
        string L(string k) => LocalizationService._(k);
        void T(string n, string k) { if (this.FindControl<TextBlock>(n) is { } t) t.Text = L(k); }
        void S(string n, string k) { if (this.FindControl<Controls.StatCard>(n) is { } s) s.Label = L(k); }
        void B(string n, string k) { if (this.FindControl<Button>(n) is { } b) b.Content = L(k); }
        if (this.FindControl<Controls.ModuleHeader>("Header") is { } h)
        { h.Title = L("nav.xcodeCleaner"); h.Subtitle = L("xcodeCleaner.subtitle"); }
        T("PageTitle", "nav.xcodeCleaner");
        S("TotalStatCard", "xcodeCleaner.stat.total"); S("CategoriesStatCard", "xcodeCleaner.stat.categories"); S("OldestStatCard", "xcodeCleaner.stat.oldest");
        T("SafeKicker", "xcodeCleaner.safe.kicker"); B("PruneSafeBtn", "xcodeCleaner.safe.action");
        T("GranularHeading", "xcodeCleaner.granular.heading"); T("GranularNote", "xcodeCleaner.granular.note");
        T("DangerKicker", "xcodeCleaner.danger.kicker"); T("DangerWarning", "xcodeCleaner.danger.warning");
        if (this.FindControl<CheckBox>("DangerAckCheckBox") is { } cb) cb.Content = L("xcodeCleaner.danger.acknowledge");
        B("PruneDangerAllBtn", "xcodeCleaner.danger.pruneAll"); B("CancelBtn", "xcodeCleaner.action.cancel");
        T("AboutHeading", "xcodeCleaner.about.heading");
        T("AboutItem1", "xcodeCleaner.about.item1"); T("AboutItem2", "xcodeCleaner.about.item2");
        T("AboutItem3", "xcodeCleaner.about.item3"); T("AboutItem4", "xcodeCleaner.about.item4");
        T("AboutItem5", "xcodeCleaner.about.item5");
        // Per-row Prune buttons localize after ItemsControls materialize.
        Loaded += (_, _) =>
        {
            foreach (var btn in this.GetVisualDescendants().OfType<Button>())
                if (btn.Classes.Contains("category-prune-btn") || btn.Classes.Contains("danger-prune-btn"))
                    btn.Content = L("xcodeCleaner.action.prune");
        };
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
