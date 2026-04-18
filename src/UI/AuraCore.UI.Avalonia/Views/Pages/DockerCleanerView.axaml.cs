using AuraCore.Module.DockerCleaner;
using AuraCore.UI.Avalonia.ViewModels;
using global::Avalonia.Controls;
using global::Avalonia.Interactivity;
using global::Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;

namespace AuraCore.UI.Avalonia.Views.Pages;

public partial class DockerCleanerView : UserControl
{
    public DockerCleanerView()
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
                    var engine = App.Services.GetRequiredService<DockerCleanerModule>();
                    DataContext = new DockerCleanerViewModel(engine);
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
            h.Title = L("nav.dockerCleaner");
            h.Subtitle = L("dockerCleaner.subtitle");
        }
        if (this.FindControl<TextBlock>("PageTitle") is { } pt) pt.Text = L("nav.dockerCleaner");
        if (this.FindControl<Controls.StatCard>("ImagesCard") is { } ic) ic.Label = L("dockerCleaner.stat.images");
        if (this.FindControl<Controls.StatCard>("ContainersCard") is { } cc) cc.Label = L("dockerCleaner.stat.containers");
        if (this.FindControl<Controls.StatCard>("VolumesCard") is { } vc) vc.Label = L("dockerCleaner.stat.volumes");
        if (this.FindControl<Controls.StatCard>("BuildCacheCard") is { } bc) bc.Label = L("dockerCleaner.stat.buildCache");
        if (this.FindControl<Button>("ScanBtn") is { } sb) sb.Content = L("dockerCleaner.action.scan");
        if (this.FindControl<Button>("CancelBtn") is { } cb) cb.Content = L("dockerCleaner.action.cancel");
        if (this.FindControl<TextBlock>("SafeKicker") is { } sk) sk.Text = L("dockerCleaner.safe.kicker");
        if (this.FindControl<Button>("PruneSafeBtn") is { } psb) psb.Content = L("dockerCleaner.safe.action");
        if (this.FindControl<TextBlock>("GranularHeading") is { } gh) gh.Text = L("dockerCleaner.granular.heading");
        if (this.FindControl<Button>("PruneImagesBtn") is { } pib) pib.Content = L("dockerCleaner.action.pruneImages");
        if (this.FindControl<Button>("PruneContainersBtn") is { } pcb) pcb.Content = L("dockerCleaner.action.pruneContainers");
        if (this.FindControl<Button>("PruneBuildCacheBtn") is { } pbcb) pbcb.Content = L("dockerCleaner.action.pruneBuildCache");
        if (this.FindControl<TextBlock>("DangerKicker") is { } dk) dk.Text = L("dockerCleaner.danger.kicker");
        if (this.FindControl<TextBlock>("VolumeDataLossWarning") is { } vdl) vdl.Text = L("dockerCleaner.warning.volumeDataLoss");
        if (this.FindControl<CheckBox>("VolumeAckCheckBox") is { } vck) vck.Content = L("dockerCleaner.danger.acknowledge");
        if (this.FindControl<Button>("PruneVolumesBtn") is { } pvb) pvb.Content = L("dockerCleaner.action.pruneVolumes");
        if (this.FindControl<TextBlock>("PrivilegeWarning") is { } pw) pw.Text = L("dockerCleaner.warning.privilege");
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
