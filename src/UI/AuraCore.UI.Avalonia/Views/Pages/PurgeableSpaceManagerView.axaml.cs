using System.ComponentModel;
using System.Runtime.Versioning;
using AuraCore.Module.PurgeableSpaceManager;
using AuraCore.UI.Avalonia.ViewModels;
using global::Avalonia.Controls;
using global::Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;

namespace AuraCore.UI.Avalonia.Views.Pages;

[SupportedOSPlatform("macos")]
public partial class PurgeableSpaceManagerView : UserControl
{
    public PurgeableSpaceManagerView()
    {
        InitializeComponent();
        ApplyLocalizedTexts();
        Loaded += (_, _) =>
        {
            if (DataContext is null)
            {
                try
                {
                    var engine = App.Services.GetRequiredService<PurgeableSpaceManagerModule>();
                    DataContext = new PurgeableSpaceManagerViewModel(engine);
                }
                catch { /* design-time / tests without DI */ }
            }
            HookStackedBar();
            (DataContext as PurgeableSpaceManagerViewModel)?.TriggerInitialScan();
        };
    }

    // Avalonia Grid.ColumnDefinitions isn't a StyledProperty, so code-behind
    // re-parse on PropertyChanged is the stable approach for VM-driven widths.
    private void HookStackedBar()
    {
        if (DataContext is not PurgeableSpaceManagerViewModel vm) return;
        ApplyColumns(vm.ColumnDefinitions);
        vm.PropertyChanged -= OnVmPropertyChanged;
        vm.PropertyChanged += OnVmPropertyChanged;
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PurgeableSpaceManagerViewModel.ColumnDefinitions)
            && sender is PurgeableSpaceManagerViewModel vm)
            ApplyColumns(vm.ColumnDefinitions);
    }

    private void ApplyColumns(string spec)
    {
        if (this.FindControl<Grid>("BreakdownBar") is not { } grid) return;
        try { grid.ColumnDefinitions = ColumnDefinitions.Parse(spec); }
        catch { grid.ColumnDefinitions = ColumnDefinitions.Parse("1*,0*,0*"); }
    }

    private void ApplyLocalizedTexts()
    {
        string L(string k) => LocalizationService._(k);
        void T(string n, string k) { if (this.FindControl<TextBlock>(n) is { } t) t.Text = L(k); }
        void B(string n, string k) { if (this.FindControl<Button>(n) is { } b) b.Content = L(k); }
        void S(string n, string k) { if (this.FindControl<Controls.StatCard>(n) is { } s) s.Label = L(k); }
        if (this.FindControl<Controls.ModuleHeader>("Header") is { } h)
        { h.Title = L("nav.purgeableSpace"); h.Subtitle = L("purgeableSpace.subtitle"); }
        T("PageTitle", "nav.purgeableSpace");
        T("BreakdownHeading", "purgeableSpace.breakdown.heading");
        T("UsedLabel", "purgeableSpace.legend.used");
        T("PurgeableLegendLabel", "purgeableSpace.legend.purgeable");
        T("FreeLabel", "purgeableSpace.legend.free");
        S("PurgeableStatCard", "purgeableSpace.stat.purgeable");
        S("SnapshotsStatCard", "purgeableSpace.stat.snapshots");
        T("EducationHeading", "purgeableSpace.education.heading");
        T("EducationBody1", "purgeableSpace.education.body1");
        T("EducationBody2", "purgeableSpace.education.body2");
        T("ActionsHeading", "purgeableSpace.actions.heading");
        B("CleanCachesBtn", "purgeableSpace.action.cleanCaches");
        T("CleanCachesHint", "purgeableSpace.action.cleanCaches.hint");
        B("RunPeriodicBtn", "purgeableSpace.action.runPeriodic");
        T("RunPeriodicHint", "purgeableSpace.action.runPeriodic.hint");
        T("ThinSnapshotsDescription", "purgeableSpace.action.thinSnapshots.description");
        B("ThinSnapshotsBtn", "purgeableSpace.action.thinSnapshots");
        B("CancelBtn", "purgeableSpace.action.cancel");
        T("PrivilegeWarning", "purgeableSpace.warning.privilege");
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
