using System.Linq;
using AuraCore.Module.SpotlightManager;
using AuraCore.UI.Avalonia.ViewModels;
using global::Avalonia.Controls;
using global::Avalonia.Markup.Xaml;
using global::Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;

namespace AuraCore.UI.Avalonia.Views.Pages;

public partial class SpotlightManagerView : UserControl
{
    public SpotlightManagerView()
    {
        InitializeComponent();
        ApplyLocalizedTexts();
        Loaded += (_, _) =>
        {
            if (DataContext is null)
            {
                try
                {
                    var engine = App.Services.GetRequiredService<SpotlightManagerModule>();
                    DataContext = new SpotlightManagerViewModel(engine);
                }
                catch { /* design-time / tests without DI */ }
            }
            (DataContext as SpotlightManagerViewModel)?.TriggerInitialScan();
        };
    }

    private void ApplyLocalizedTexts()
    {
        string L(string k) => LocalizationService._(k);
        void T(string n, string k) { if (this.FindControl<TextBlock>(n) is { } t) t.Text = L(k); }
        void S(string n, string k) { if (this.FindControl<Controls.StatCard>(n) is { } s) s.Label = L(k); }
        void B(string n, string k) { if (this.FindControl<Button>(n) is { } b) b.Content = L(k); }
        if (this.FindControl<Controls.ModuleHeader>("Header") is { } h)
        { h.Title = L("nav.spotlightManager"); h.Subtitle = L("spotlight.subtitle"); }
        T("PageTitle", "nav.spotlightManager");
        S("VolumesStatCard", "spotlight.stat.volumes");
        S("IndexedStatCard", "spotlight.stat.indexed");
        S("DisabledStatCard", "spotlight.stat.disabled");
        T("VolumesHeading", "spotlight.volumes.heading");
        T("RebuildConfirmTitle", "spotlight.rebuild.confirm.title");
        B("RebuildCancelBtn", "spotlight.rebuild.confirm.cancel");
        B("RebuildConfirmBtn", "spotlight.rebuild.confirm.proceed");
        T("EmptyStateText", "spotlight.empty");
        T("AboutHeading", "spotlight.about.heading");
        T("AboutBody1", "spotlight.about.body1");
        T("AboutBody2", "spotlight.about.body2");
        B("CancelBtn", "spotlight.action.cancel");
        T("PrivilegeWarning", "spotlight.warning.privilege");
        // Per-row Rebuild buttons are inside the ItemTemplate — localize them
        // after the ItemsControl materialises by walking the visual tree.
        Loaded += (_, _) =>
        {
            foreach (var btn in this.GetVisualDescendants().OfType<Button>())
                if (btn.Classes.Contains("rebuild-btn"))
                    btn.Content = L("spotlight.action.rebuild");
        };
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
