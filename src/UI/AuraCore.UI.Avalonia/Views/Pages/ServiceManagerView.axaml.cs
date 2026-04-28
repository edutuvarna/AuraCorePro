using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;
using AuraCore.Module.ServiceManager;
using AuraCore.UI.Avalonia.Helpers;
using AuraCore.UI.Avalonia.Views.Dialogs;
using global::Avalonia.Controls;
using global::Avalonia.Interactivity;
using global::Avalonia.Media;
using Microsoft.Extensions.DependencyInjection;

namespace AuraCore.UI.Avalonia.Views.Pages;

public record ServiceDisplayItem(string DisplayName, string ServiceName, string StartType,
    string Status, ISolidColorBrush StatusFg, ISolidColorBrush StatusBg, string Pid);

public partial class ServiceManagerView : UserControl
{
    private List<ServiceDisplayItem> _allItems = new();
    private ServiceManagerEngine? _engine;

    public ServiceManagerView()
    {
        InitializeComponent();
        _engine = App.Services.GetService<ServiceManagerEngine>();
        Loaded += async (s, e) => { await RunScan(); ApplyLocalization(); };
        LocalizationService.LanguageChanged += () =>
            global::Avalonia.Threading.Dispatcher.UIThread.Post(ApplyLocalization);
    }

    private async Task RunScan()
    {
        if (!OperatingSystem.IsWindows()) { SubText.Text = LocalizationService._("svcMgr.windowsOnly"); return; }
        ScanLabel.Text = LocalizationService._("common.scanning");
        try
        {
            var rawData = await Task.Run(() =>
            {
                // Phase 6.16 Linux platform guard — defense in depth inside the Task.Run delegate.
                // RunScan's method-entry guard at the top already short-circuits on non-Windows,
                // but ServiceController.GetServices() is documented as Windows-only and throws
                // PlatformNotSupportedException on Linux/macOS. Inner guard keeps this lambda safe
                // to call from any future code path that bypasses the outer check.
                if (!OperatingSystem.IsWindows())
                    return new List<(string Name, string Svc, string Start, string Stat)>();

                var services = ServiceController.GetServices();
                return services.Select(s =>
                {
                    string startType;
                    try { startType = s.StartType.ToString(); } catch { startType = "Unknown"; }
                    return (Name: s.DisplayName, Svc: s.ServiceName, Start: startType, Stat: s.Status.ToString());
                }).OrderBy(s => s.Name).ToList();
            });
            _allItems = rawData.Select(s =>
            {
                var (fg, bg) = s.Stat == "Running"
                    ? (P("#22C55E"), P("#2022C55E"))
                    : (P("#8888A0"), P("#208888A0"));
                return new ServiceDisplayItem(s.Name, s.Svc, s.Start, s.Stat, fg, bg, "");
            }).ToList();
            ApplyFilter();
            TotalSvc.Text = _allItems.Count.ToString();
            RunningSvc.Text = _allItems.Count(s => s.Status == "Running").ToString();
            StoppedSvc.Text = _allItems.Count(s => s.Status == "Stopped").ToString();
            AutoSvc.Text = _allItems.Count(s => s.StartType == "Automatic").ToString();
        }
        catch (System.Exception ex) { SubText.Text = $"{LocalizationService._("common.errorPrefix")}{ex.Message}"; }
        finally { ScanLabel.Text = LocalizationService._("common.scan"); }
    }

    private void ApplyFilter()
    {
        var q = SearchBox.Text?.Trim() ?? "";
        var filtered = string.IsNullOrEmpty(q) ? _allItems
            : _allItems.Where(s => s.DisplayName.Contains(q, StringComparison.OrdinalIgnoreCase)
                || s.ServiceName.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();
        SvcList.ItemsSource = filtered;
    }

    private static SolidColorBrush P(string h) => new(Color.Parse(h));
    private async void Scan_Click(object? s, RoutedEventArgs e) => await RunScan();
    private void Search_Changed(object? s, TextChangedEventArgs e) => ApplyFilter();

    private void ContextMenu_Opening(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (sender is ContextMenu menu) ServiceContextMenu_Opening(menu);
    }

    private void ApplyLocalization()
    {
        PageTitle.Text = LocalizationService._("nav.serviceManager");
        ModuleHdr.Title = LocalizationService._("nav.serviceManager");
        ModuleHdr.Subtitle = LocalizationService._("svcMgr.subtitle");
        ScanLabel.Text = LocalizationService._("common.scan");
        SearchBox.Watermark = LocalizationService._("svcMgr.searchWatermark");
        StatTotal.Label   = LocalizationService._("common.statTotal");
        StatRunning.Label = LocalizationService._("svcMgr.statRunning");
        StatStopped.Label = LocalizationService._("svcMgr.statStopped");
        StatAuto.Label    = LocalizationService._("svcMgr.statAuto");
        // ContextMenu MenuItems are inside DataTemplate — they cannot be accessed by name
        // from code-behind; they re-populate each time the template is applied.
        // The Headers are set via ApplyContextMenuLocalization called from the context
        // menu opening event wired in the item template.
    }

    /// <summary>
    /// Called when the service item context menu opens so we can apply localized headers.
    /// </summary>
    private void ServiceContextMenu_Opening(ContextMenu menu)
    {
        if (menu.Items.Count < 4) return;
        if (menu.Items[0] is MenuItem start)   start.Header   = LocalizationService._("common.start");
        if (menu.Items[1] is MenuItem stop)    stop.Header    = LocalizationService._("common.stop");
        if (menu.Items[2] is MenuItem restart) restart.Header = LocalizationService._("common.restart");
        if (menu.Items[3] is MenuItem sep)     { /* separator */ }
        if (menu.Items.Count > 4 && menu.Items[4] is MenuItem startupType)
        {
            startupType.Header = LocalizationService._("svcMgr.menu.startupType");
            if (startupType.Items.Count >= 3)
            {
                if (startupType.Items[0] is MenuItem auto)     auto.Header     = LocalizationService._("svcMgr.menu.automatic");
                if (startupType.Items[1] is MenuItem manual)   manual.Header   = LocalizationService._("svcMgr.menu.manual");
                if (startupType.Items[2] is MenuItem disabled) disabled.Header = LocalizationService._("svcMgr.menu.disabled");
            }
        }
    }

    // ── Context menu helpers ──────────────────────────────────────────────────

    private ServiceDisplayItem? GetContextItem(object? sender)
    {
        if (sender is MenuItem mi && mi.Parent is ContextMenu cm)
            return cm.PlacementTarget?.DataContext as ServiceDisplayItem;
        return null;
    }

    private async Task<bool> EnsurePrivilegedHelperInstalledAsync()
    {
        var installer = App.Services?.GetService<PrivilegedHelperInstaller>();
        if (installer is null) return true; // DI not wired (tests/design-time) — don't block

        if (await installer.IsInstalledAsync(CancellationToken.None))
            return true;

        // Prompt for consent + install
        var topWindow = TopLevel.GetTopLevel(this) as Window;
        if (topWindow is null) return false;

        var dialog = new PrivilegedHelperInstallDialog(installer);
        await dialog.ShowDialog(topWindow);
        return dialog.Outcome == PrivilegedHelperInstallOutcome.Success;
    }

    private async void ServiceStart_Click(object? sender, RoutedEventArgs e)
    {
        var item = GetContextItem(sender);
        if (item is null || _engine is null) return;
        if (!await EnsurePrivilegedHelperInstalledAsync())
        {
            ShowBanner(LocalizationService.Get("privhelper.notInstalled.toast"), isError: true);
            return;
        }
        await DispatchOp(() => _engine.StartAsync(item.ServiceName),
            string.Format(LocalizationService._("svcMgr.starting"), item.DisplayName));
    }

    private async void ServiceStop_Click(object? sender, RoutedEventArgs e)
    {
        var item = GetContextItem(sender);
        if (item is null || _engine is null) return;
        if (!await EnsurePrivilegedHelperInstalledAsync())
        {
            ShowBanner(LocalizationService.Get("privhelper.notInstalled.toast"), isError: true);
            return;
        }
        await DispatchOp(() => _engine.StopAsync(item.ServiceName),
            string.Format(LocalizationService._("svcMgr.stopping"), item.DisplayName));
    }

    private async void ServiceRestart_Click(object? sender, RoutedEventArgs e)
    {
        var item = GetContextItem(sender);
        if (item is null || _engine is null) return;
        if (!await EnsurePrivilegedHelperInstalledAsync())
        {
            ShowBanner(LocalizationService.Get("privhelper.notInstalled.toast"), isError: true);
            return;
        }
        await DispatchOp(() => _engine.RestartAsync(item.ServiceName),
            string.Format(LocalizationService._("svcMgr.restarting"), item.DisplayName));
    }

    private async void ServiceStartupAuto_Click(object? sender, RoutedEventArgs e)
    {
        var item = GetContextItem(sender);
        if (item is null || _engine is null) return;
        if (!await EnsurePrivilegedHelperInstalledAsync())
        {
            ShowBanner(LocalizationService.Get("privhelper.notInstalled.toast"), isError: true);
            return;
        }
        await DispatchOp(() => _engine.SetStartupAsync(item.ServiceName, "auto"),
            string.Format(LocalizationService._("svcMgr.setStartupAuto"), item.DisplayName));
    }

    private async void ServiceStartupManual_Click(object? sender, RoutedEventArgs e)
    {
        var item = GetContextItem(sender);
        if (item is null || _engine is null) return;
        if (!await EnsurePrivilegedHelperInstalledAsync())
        {
            ShowBanner(LocalizationService.Get("privhelper.notInstalled.toast"), isError: true);
            return;
        }
        await DispatchOp(() => _engine.SetStartupAsync(item.ServiceName, "demand"),
            string.Format(LocalizationService._("svcMgr.setStartupManual"), item.DisplayName));
    }

    private async void ServiceStartupDisabled_Click(object? sender, RoutedEventArgs e)
    {
        var item = GetContextItem(sender);
        if (item is null || _engine is null) return;
        if (!await EnsurePrivilegedHelperInstalledAsync())
        {
            ShowBanner(LocalizationService.Get("privhelper.notInstalled.toast"), isError: true);
            return;
        }
        await DispatchOp(() => _engine.SetStartupAsync(item.ServiceName, "disabled"),
            string.Format(LocalizationService._("svcMgr.setStartupDisabled"), item.DisplayName));
    }

    private async Task DispatchOp(Func<Task<ServiceOperationOutcome>> op, string progressMsg)
    {
        ShowBanner(progressMsg, isError: false);
        try
        {
            var outcome = await op();
            if (outcome.HelperMissing)
                ShowBanner(LocalizationService._("svcMgr.helperMissing"), isError: true);
            else if (!outcome.Success)
                ShowBanner($"{LocalizationService._("common.errorPrefix")}{outcome.Error ?? "unknown"}", isError: true);
            else
            {
                ShowBanner(LocalizationService._("svcMgr.done"), isError: false);
                await RunScan();
            }
        }
        catch (System.Exception ex)
        {
            ShowBanner($"Exception: {ex.Message}", isError: true);
        }
    }

    private void ShowBanner(string message, bool isError)
    {
        StatusBannerText.Text = message;
        StatusBannerText.Foreground = isError ? P("#EF4444") : P("#22C55E");
        StatusBanner.IsVisible = true;
    }
}
