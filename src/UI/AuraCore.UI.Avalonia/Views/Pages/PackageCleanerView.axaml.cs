using global::Avalonia.Controls;
using global::Avalonia.Interactivity;
using global::Avalonia.Media;
using AuraCore.Application;

namespace AuraCore.UI.Avalonia.Views.Pages;

public partial class PackageCleanerView : UserControl
{
    public PackageCleanerView()
    {
        InitializeComponent();
        Loaded += (s, e) => ApplyLocalization();
        LocalizationService.LanguageChanged += OnLanguageChanged;
        Unloaded += OnUnloaded;
    }

    private void OnLanguageChanged() =>
        global::Avalonia.Threading.Dispatcher.UIThread.Post(ApplyLocalization);

    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        LocalizationService.LanguageChanged -= OnLanguageChanged;
    }

    private async void Scan_Click(object? sender, RoutedEventArgs e)
    {
        if (!OperatingSystem.IsLinux()) { SubText.Text = LocalizationService._("common.linuxOnly"); return; }
        ScanBtn.IsEnabled = false;
        SubText.Text = LocalizationService._("pkgCleaner.scanning");

        var categories = await Task.Run(() =>
        {
            var list = new List<(string Name, string Path, long Size, string Desc)>();
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            // APT cache
            ScanDir(list, "/var/cache/apt/archives", "APT Package Cache", "Downloaded .deb packages");
            // DNF/YUM cache
            ScanDir(list, "/var/cache/dnf", "DNF Cache", "Downloaded RPM packages and metadata");
            ScanDir(list, "/var/cache/yum", "YUM Cache", "Downloaded RPM packages and metadata");
            // Snap cache
            ScanDir(list, "/var/lib/snapd/cache", "Snap Cache", "Snap package cache");
            // Pip cache
            ScanDir(list, Path.Combine(home, ".cache/pip"), "Pip Cache", "Python package downloads");
            // npm cache
            ScanDir(list, Path.Combine(home, ".npm/_cacache"), "npm Cache", "Node.js package cache");
            // Flatpak
            ScanDir(list, "/var/tmp/flatpak-cache", "Flatpak Cache", "Flatpak temporary data");
            // Thumbnail cache
            ScanDir(list, Path.Combine(home, ".cache/thumbnails"), "Thumbnail Cache", "Image thumbnails");
            // Journal logs
            ScanDir(list, "/var/log/journal", "Journal Logs", "Systemd journal log files");
            // Old kernels indicator
            ScanDir(list, "/var/log", "System Logs", "Application and system log files");

            return list;
        });

        var aptSize = categories.Where(c => c.Name.Contains("APT")).Sum(c => c.Size);
        var snapSize = categories.Where(c => c.Name.Contains("Snap")).Sum(c => c.Size);
        var pipSize = categories.Where(c => c.Name.Contains("Pip")).Sum(c => c.Size);

        AptCacheSize.Text = FormatSize(aptSize);
        SnapCacheSize.Text = FormatSize(snapSize);
        PipCacheSize.Text = FormatSize(pipSize);

        var totalSize = categories.Sum(c => c.Size);
        SubText.Text = $"Found {categories.Count} categories, {FormatSize(totalSize)} reclaimable";

        ScanBtn.IsEnabled = true;
        CacheList.ItemsSource = categories.Where(c => c.Size > 0).OrderByDescending(c => c.Size).Select(c =>
        {
            var sizeStr = FormatSize(c.Size);
            var severity = c.Size > 500 * 1024 * 1024 ? "#EF4444" : c.Size > 100 * 1024 * 1024 ? "#F59E0B" : "#22C55E";

            return new Border
            {
                CornerRadius = new global::Avalonia.CornerRadius(6),
                Background = new SolidColorBrush(Color.Parse("#12FFFFFF")),
                Padding = new global::Avalonia.Thickness(12, 10),
                Margin = new global::Avalonia.Thickness(0, 0, 0, 4),
                Child = new Grid
                {
                    ColumnDefinitions = global::Avalonia.Controls.ColumnDefinitions.Parse("*,Auto"),
                    Children =
                    {
                        new StackPanel { Children = {
                            new TextBlock { Text = c.Name, FontSize = 13, FontWeight = FontWeight.SemiBold,
                                Foreground = new SolidColorBrush(Color.Parse("#E8E8F0")) },
                            new TextBlock { Text = c.Desc, FontSize = 10, Foreground = new SolidColorBrush(Color.Parse("#8888A0")) },
                            new TextBlock { Text = c.Path, FontSize = 9, Foreground = new SolidColorBrush(Color.Parse("#555570")) },
                        }},
                        new TextBlock { [Grid.ColumnProperty] = 1, Text = sizeStr, FontSize = 14, FontWeight = FontWeight.Bold,
                            Foreground = new SolidColorBrush(Color.Parse(severity)),
                            VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center,
                            Margin = new global::Avalonia.Thickness(12, 0, 0, 0) }
                    }
                }
            };
        }).ToList();
    }

    private static void ScanDir(List<(string, string, long, string)> list, string path, string name, string desc)
    {
        if (!Directory.Exists(path)) return;
        try
        {
            long size = 0;
            foreach (var f in Directory.EnumerateFiles(path, "*", new EnumerationOptions
            { IgnoreInaccessible = true, RecurseSubdirectories = true }))
            {
                try { size += new FileInfo(f).Length; } catch { }
            }
            list.Add((name, path, size, desc));
        }
        catch { }
    }

    private static string FormatSize(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:F0} KB",
        < 1024L * 1024 * 1024 => $"{bytes / (1024.0 * 1024):F1} MB",
        _ => $"{bytes / (1024.0 * 1024 * 1024):F2} GB"
    };

    private void ApplyLocalization()
    {
        string L(string k) => LocalizationService._(k);
        PageTitle.Text = L("nav.packageCleaner");
        PkgHeader.Title    = L("nav.packageCleaner");
        PkgHeader.Subtitle = L("packageCleaner.subtitle");
        AptCacheStat.Label  = L("packageCleaner.stat.aptCache");
        SnapCacheStat.Label = L("packageCleaner.stat.snapCache");
        PipCacheStat.Label  = L("packageCleaner.stat.pipCache");
        CacheCategHeading.Text = L("packageCleaner.heading.cacheCategories");
        ScanBtn.Content = L("packageCleaner.action.scan");
        SubText.Text    = L("packageCleaner.subtext.initial");
        PrivilegeWarning.Text = L("packageCleaner.warning.privilege");
    }

    /// <summary>
    /// Phase 6.17 Wave F — surfaces an OperationResult into the post-action banner.
    /// Wired to clean/autoremove actions when invoked.
    /// </summary>
    internal void ApplyPostActionBanner(OperationResult opResult)
    {
        PostActionBanner.IsVisible = true;
        PostActionBanner.Foreground = opResult.Status switch
        {
            OperationStatus.Success => new SolidColorBrush(Color.Parse("#10B981")),
            OperationStatus.Skipped => new SolidColorBrush(Color.Parse("#F59E0B")),
            OperationStatus.Failed  => new SolidColorBrush(Color.Parse("#EF4444")),
            _                       => new SolidColorBrush(Color.Parse("#9CA3AF")),
        };
        PostActionBanner.Text = opResult.Status switch
        {
            OperationStatus.Success => string.Format(LocalizationService._("op.result.success"),
                                          FormatBytesNew(opResult.BytesFreed), opResult.ItemsAffected, opResult.Duration.TotalSeconds),
            OperationStatus.Skipped => string.Format(LocalizationService._("op.result.skipped"), opResult.Reason ?? string.Empty),
            OperationStatus.Failed  => string.Format(LocalizationService._("op.result.failed"), opResult.Reason ?? string.Empty),
            _                       => string.Empty,
        };
    }

    private static string FormatBytesNew(long b) => b switch
    {
        < 1024 => $"{b} B",
        < 1024 * 1024 => $"{b / 1024.0:F1} KB",
        < 1024L * 1024 * 1024 => $"{b / (1024.0 * 1024):F1} MB",
        _ => $"{b / (1024.0 * 1024 * 1024):F2} GB"
    };
}
