using System.ServiceProcess;
using global::Avalonia.Controls;
using global::Avalonia.Interactivity;
using global::Avalonia.Media;

namespace AuraCore.UI.Avalonia.Views.Pages;

public record ServiceDisplayItem(string DisplayName, string ServiceName, string StartType,
    string Status, ISolidColorBrush StatusFg, ISolidColorBrush StatusBg, string Pid);

public partial class ServiceManagerView : UserControl
{
    private List<ServiceDisplayItem> _allItems = new();
    public ServiceManagerView()
    {
        InitializeComponent();
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
            _allItems = await Task.Run(() =>
            {
                var services = ServiceController.GetServices();
                return services.Select(s =>
                {
                    var (fg, bg) = s.Status == ServiceControllerStatus.Running
                        ? (P("#22C55E"), P("#2022C55E"))
                        : (P("#8888A0"), P("#208888A0"));
                    string startType;
                    try { startType = s.StartType.ToString(); } catch { startType = "Unknown"; }
                    return new ServiceDisplayItem(s.DisplayName, s.ServiceName, startType,
                        s.Status.ToString(), fg, bg, "");
                }).OrderBy(s => s.DisplayName).ToList();
            });
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
}