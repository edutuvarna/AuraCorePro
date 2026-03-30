using global::Avalonia.Controls;
using global::Avalonia.Interactivity;
using global::Avalonia.Media;

namespace AuraCore.UI.Avalonia.Views.Pages;

public record FolderDisplayItem(string Name, string Path, string Size, string Icon, string ItemCount,
    ISolidColorBrush BarBrush, double BarWidth);

public partial class SpaceAnalyzerView : UserControl
{
    private readonly Stack<string> _history = new();
    private string _currentPath = "";
    public SpaceAnalyzerView()
    {
        InitializeComponent();
        Loaded += (s, e) => { ShowDrives(); ApplyLocalization(); };
        LocalizationService.LanguageChanged += () =>
            global::Avalonia.Threading.Dispatcher.UIThread.Post(ApplyLocalization);
    }

    private void ShowDrives()
    {
        _currentPath = "";
        PathLabel.Text = "Select a drive to analyze";
        BackBtn.IsVisible = false;
        TotalSize.Text = "--"; UsedSize.Text = "--"; FreeSize.Text = "--";
        try
        {
            var drives = DriveInfo.GetDrives().Where(d => d.IsReady).Select(d =>
            {
                var totalGb = d.TotalSize / (1024.0 * 1024 * 1024);
                var freeGb = d.AvailableFreeSpace / (1024.0 * 1024 * 1024);
                var usedGb = totalGb - freeGb;
                return new FolderDisplayItem(
                    $"{d.Name} ({d.VolumeLabel})", d.Name, $"{totalGb:F1} GB",
                    "\u1F4BE", $"{freeGb:F1} GB free",
                    new SolidColorBrush(Color.Parse("#3B82F6")), 60 * (usedGb / totalGb)
                );
            }).ToList();
            FolderList.ItemsSource = drives;
        }
        catch { }
    }

    private async void AnalyzePath(string path)
    {
        _currentPath = path;
        PathLabel.Text = path;
        BackBtn.IsVisible = true;
        ScanLabel.Text = "Scanning...";

        try
        {
            var di = new DirectoryInfo(path);
            if (di.Parent == null) // Drive root
            {
                var drive = new DriveInfo(path);
                var tGb = drive.TotalSize / (1024.0 * 1024 * 1024);
                var fGb = drive.AvailableFreeSpace / (1024.0 * 1024 * 1024);
                TotalSize.Text = $"{tGb:F1} GB";
                UsedSize.Text = $"{tGb - fGb:F1} GB";
                FreeSize.Text = $"{fGb:F1} GB";
            }

            var items = await Task.Run(() =>
            {
                var folders = new List<(string Name, string FullPath, long Size, int Count)>();
                try
                {
                    foreach (var dir in di.GetDirectories())
                    {
                        try
                        {
                            long size = 0; int count = 0;
                            foreach (var f in dir.EnumerateFiles("*", SearchOption.AllDirectories))
                            {
                                try { size += f.Length; count++; } catch { }
                            }
                            folders.Add((dir.Name, dir.FullName, size, count));
                        }
                        catch { folders.Add((dir.Name, dir.FullName, 0, 0)); }
                    }
                }
                catch { }
                return folders.OrderByDescending(f => f.Size).ToList();
            });

            var maxSize = items.Count > 0 ? items.Max(i => i.Size) : 1;
            var colors = new[] { "#4285F4", "#EA4335", "#FBBC04", "#34A853", "#9C27B0", "#FF7043", "#00ACC1", "#795548" };
            FolderList.ItemsSource = items.Select((f, i) => new FolderDisplayItem(
                f.Name, f.FullPath, FormatBytes(f.Size), "\u1F4C1",
                $"{f.Count} files",
                new SolidColorBrush(Color.Parse(colors[i % colors.Length])),
                maxSize > 0 ? 60.0 * f.Size / maxSize : 0
            )).ToList();
        }
        catch { PathLabel.Text = $"Access denied: {path}"; }
        finally { ScanLabel.Text = "Scan"; }
    }

    private static string FormatBytes(long b) => b switch
    {
        >= 1073741824 => $"{b / 1073741824.0:F1} GB",
        >= 1048576    => $"{b / 1048576.0:F1} MB",
        >= 1024       => $"{b / 1024.0:F1} KB",
        _             => $"{b} B"
    };

    private void Folder_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string path)
        {
            if (!string.IsNullOrEmpty(_currentPath)) _history.Push(_currentPath);
            AnalyzePath(path);
        }
    }

    private void Back_Click(object? sender, RoutedEventArgs e)
    {
        if (_history.Count > 0) AnalyzePath(_history.Pop());
        else ShowDrives();
    }

    private void Scan_Click(object? sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(_currentPath)) AnalyzePath(_currentPath);
        else ShowDrives();
}

    private void ApplyLocalization()
    {
        PageTitle.Text = LocalizationService._("nav.spaceAnalyzer");
    }
}