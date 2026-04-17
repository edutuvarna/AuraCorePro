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
        if (!OperatingSystem.IsWindows()) { SubText.Text = "Windows only"; return; }
        ScanLabel.Text = "Scanning...";
        try
        {
            var rawData = await Task.Run(() =>
            {
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
        catch (System.Exception ex) { SubText.Text = $"Error: {ex.Message}"; }
        finally { ScanLabel.Text = "Scan"; }
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

    private void ApplyLocalization()
    {
        PageTitle.Text = LocalizationService._("nav.serviceManager");
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
        await DispatchOp(() => _engine.StartAsync(item.ServiceName), $"Starting {item.DisplayName}…");
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
        await DispatchOp(() => _engine.StopAsync(item.ServiceName), $"Stopping {item.DisplayName}…");
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
        await DispatchOp(() => _engine.RestartAsync(item.ServiceName), $"Restarting {item.DisplayName}…");
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
            $"Setting {item.DisplayName} startup to Automatic…");
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
            $"Setting {item.DisplayName} startup to Manual…");
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
            $"Setting {item.DisplayName} startup to Disabled…");
    }

    private async Task DispatchOp(Func<Task<ServiceOperationOutcome>> op, string progressMsg)
    {
        ShowBanner(progressMsg, isError: false);
        try
        {
            var outcome = await op();
            if (outcome.HelperMissing)
                ShowBanner("Privileged helper not installed. Run scripts/install-privileged-service.ps1 as admin.", isError: true);
            else if (!outcome.Success)
                ShowBanner($"Error: {outcome.Error ?? "unknown"}", isError: true);
            else
            {
                ShowBanner("Done.", isError: false);
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
