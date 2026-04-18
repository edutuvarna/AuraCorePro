using global::Avalonia.Controls;
using global::Avalonia.Interactivity;
using AuraCore.UI.Avalonia.ViewModels.SpaceAnalyzer;

namespace AuraCore.UI.Avalonia.Views.Pages;

public partial class SpaceAnalyzerView : UserControl
{
    public SpaceAnalyzerView()
    {
        InitializeComponent();
        Loaded += (s, e) => { ShowDrives(); ApplyLocalization(); };
        LocalizationService.LanguageChanged += () =>
            global::Avalonia.Threading.Dispatcher.UIThread.Post(ApplyLocalization);
    }

    // -------------------------------------------------------------------------
    // Drive root
    // -------------------------------------------------------------------------

    private void ShowDrives()
    {
        PathLabel.Text = LocalizationService._("spaceAnalyzer.selectDrive");
        BackBtn.IsVisible = false;
        TotalSize.Text = "--"; UsedSize.Text = "--"; FreeSize.Text = "--";

        try
        {
            var drives = DriveInfo.GetDrives().Where(d => d.IsReady).Select(d =>
            {
                var totalGb = d.TotalSize / (1024.0 * 1024 * 1024);
                var freeGb  = d.AvailableFreeSpace / (1024.0 * 1024 * 1024);
                // Map each drive to a DirectoryNodeVM-compatible entry via the scanner
                // (drives are directories with children, so we show them as root nodes).
                return new DirectoryNodeVM(new AuraCore.UI.Avalonia.Helpers.DirectoryEntry(
                    d.Name, d.TotalSize, IsDirectory: true, HasChildren: true))
                {
                    // Override display to show friendly label — we expose a custom
                    // factory path via the plain ctor; DisplayName is readonly so we
                    // use DriveDisplayItem wrapper below instead.
                };
            }).ToList();
        }
        catch { }

        // Use a lightweight approach: populate the tree with top-level drive entries.
        // Because DirectoryNodeVM.DisplayName is set in the constructor from the Path,
        // drive roots ("C:\") show correctly.
        _ = LoadDriveRootsAsync();
    }

    private async Task LoadDriveRootsAsync()
    {
        try
        {
            var driveNodes = await Task.Run(() =>
            {
                var result = new List<DirectoryNodeVM>();
                foreach (var d in DriveInfo.GetDrives())
                {
                    if (!d.IsReady) continue;
                    var totalGb = d.TotalSize / (1024.0 * 1024 * 1024);
                    var freeGb  = d.AvailableFreeSpace / (1024.0 * 1024 * 1024);
                    var usedGb  = totalGb - freeGb;
                    var label   = string.IsNullOrEmpty(d.VolumeLabel)
                                  ? d.Name
                                  : $"{d.Name}({d.VolumeLabel})";
                    result.Add(new DirectoryNodeVM(new AuraCore.UI.Avalonia.Helpers.DirectoryEntry(
                        label, d.TotalSize, IsDirectory: true, HasChildren: true)));
                }
                return result;
            });

            // Store drive name → real path for later navigation
            _driveNameToPath.Clear();
            var readyDrives = DriveInfo.GetDrives().Where(d => d.IsReady).ToList();
            for (int i = 0; i < driveNodes.Count && i < readyDrives.Count; i++)
                _driveNameToPath[driveNodes[i].DisplayName] = readyDrives[i].Name;

            global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                DirectoryTree.ItemsSource = driveNodes;
            });
        }
        catch { }
    }

    // -------------------------------------------------------------------------
    // Directory analysis
    // -------------------------------------------------------------------------

    private async void AnalyzePath(string path)
    {
        PathLabel.Text = path;
        BackBtn.IsVisible = true;
        ScanLabel.Text = LocalizationService._("common.scanning");

        try
        {
            var di = new DirectoryInfo(path);
            if (di.Parent == null) // Drive root
            {
                var drive = new DriveInfo(path);
                var tGb = drive.TotalSize / (1024.0 * 1024 * 1024);
                var fGb = drive.AvailableFreeSpace / (1024.0 * 1024 * 1024);
                TotalSize.Text = $"{tGb:F1} GB";
                UsedSize.Text  = $"{tGb - fGb:F1} GB";
                FreeSize.Text  = $"{fGb:F1} GB";
            }

            var rootNodes = await DirectoryNodeVM.LoadRootAsync(path);

            global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                DirectoryTree.ItemsSource = rootNodes;
            });
        }
        catch { PathLabel.Text = string.Format(LocalizationService._("spaceAnalyzer.accessDenied"), path); }
        finally { ScanLabel.Text = LocalizationService._("common.scan"); }
    }

    // -------------------------------------------------------------------------
    // Scan button + Back button
    // -------------------------------------------------------------------------

    private void Scan_Click(object? sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(_currentPath)) AnalyzePath(_currentPath);
        else _ = LoadDriveRootsAsync();
    }

    private void Back_Click(object? sender, RoutedEventArgs e)
    {
        if (_history.Count > 0)
        {
            _currentPath = _history.Pop();
            AnalyzePath(_currentPath);
        }
        else
        {
            _currentPath = "";
            ShowDrives();
        }
    }

    // -------------------------------------------------------------------------
    // Localization
    // -------------------------------------------------------------------------

    private void ApplyLocalization()
    {
        PageTitle.Text = LocalizationService._("nav.spaceAnalyzer");
        ModuleHdr.Title = LocalizationService._("nav.spaceAnalyzer");
        ModuleHdr.Subtitle = LocalizationService._("spaceAnalyzer.subtitle");
        ScanLabel.Text = LocalizationService._("common.scan");
        BackBtn.Content = $"\u2190 {LocalizationService._("common.back")}";
        PathLabel.Text = LocalizationService._("spaceAnalyzer.selectDrive");
        LabelTotal.Text = LocalizationService._("spaceAnalyzer.labelTotal");
        LabelUsed.Text = LocalizationService._("spaceAnalyzer.labelUsed");
        LabelFree.Text = LocalizationService._("spaceAnalyzer.labelFree");
    }

    // -------------------------------------------------------------------------
    // State
    // -------------------------------------------------------------------------

    private readonly Stack<string>      _history        = new();
    private string                      _currentPath    = "";
    private readonly Dictionary<string, string> _driveNameToPath = new();
}
