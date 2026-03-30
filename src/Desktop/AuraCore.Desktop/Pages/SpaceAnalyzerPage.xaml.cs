using AuraCore.Desktop.Services;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.UI;
using IOPath = System.IO.Path;

namespace AuraCore.Desktop.Pages;

public sealed partial class SpaceAnalyzerPage : Page
{
    private sealed record FolderEntry(string Name, string FullPath, long SizeBytes, int Depth);

    private static readonly Color[] Palette = new[]
    {
        Color.FromArgb(255, 66, 133, 244),  Color.FromArgb(255, 234, 67, 53),
        Color.FromArgb(255, 251, 188, 4),   Color.FromArgb(255, 52, 168, 83),
        Color.FromArgb(255, 156, 39, 176),  Color.FromArgb(255, 255, 112, 67),
        Color.FromArgb(255, 0, 172, 193),   Color.FromArgb(255, 121, 85, 72),
        Color.FromArgb(255, 233, 30, 99),   Color.FromArgb(255, 63, 81, 181),
        Color.FromArgb(255, 205, 220, 57),  Color.FromArgb(255, 255, 87, 34),
    };

    private readonly Stack<string> _pathHistory = new();
    private string _currentDrive = "";

    public SpaceAnalyzerPage()
    {
        InitializeComponent();
        LoadDrives();
        ApplyLocalization();
        S.LanguageChanged += () => DispatcherQueue?.TryEnqueue(ApplyLocalization);
    }

    private void ApplyLocalization()
    {
        if (FindName("PageTitle") is TextBlock title) title.Text = S._("space.title");
        if (FindName("PageSubtitle") is TextBlock subtitle) subtitle.Text = S._("space.subtitle");
    }

    private void LoadDrives()
    {
        foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady))
        {
            var label = string.IsNullOrEmpty(drive.VolumeLabel) ? "Local Disk" : drive.VolumeLabel;
            var freeGb = drive.AvailableFreeSpace / (1024.0 * 1024 * 1024);
            var totalGb = drive.TotalSize / (1024.0 * 1024 * 1024);
            DriveSelector.Items.Add(new ComboBoxItem
            {
                Content = $"{drive.Name.TrimEnd('\\')} {label} ({freeGb:F0} GB free / {totalGb:F0} GB)",
                Tag = drive.Name
            });
        }
        if (DriveSelector.Items.Count > 0) DriveSelector.SelectedIndex = 0;
    }

    private async void ScanBtn_Click(object sender, RoutedEventArgs e)
    {
        if (DriveSelector.SelectedItem is not ComboBoxItem item || item.Tag is not string drivePath) return;
        _currentDrive = drivePath;
        _pathHistory.Clear();
        await ScanPathAsync(drivePath);
    }

    private async Task ScanPathAsync(string path)
    {
        ScanBtn.IsEnabled = false;
        Progress.IsActive = true; Progress.Visibility = Visibility.Visible;
        StatusText.Text = string.Format(S._("space.scanningPath"), path);
        TopFoldersList.Children.Clear();
        TreeMapCanvas.Children.Clear();
        FileTypesList.Children.Clear();

        try
        {
            var (folders, fileTypes) = await Task.Run(() => ScanFolder(path));

            if (folders.Count == 0)
            {
                StatusText.Text = S._("space.noFolders");
                return;
            }

            // Drive info (only for root)
            if (path == _currentDrive)
            {
                var drive = new DriveInfo(path.TrimEnd('\\'));
                var totalGb = drive.TotalSize / (1024.0 * 1024 * 1024);
                var freeGb = drive.AvailableFreeSpace / (1024.0 * 1024 * 1024);
                var usedGb = totalGb - freeGb;
                TotalSizeText.Text = $"{totalGb:F1} GB";
                UsedSizeText.Text = $"{usedGb:F1} GB";
                FreeSizeText.Text = $"{freeGb:F1} GB";
                FolderCountText.Text = folders.Count.ToString();
                SummaryCard.Visibility = Visibility.Visible;
                UsageBar.Value = (int)((usedGb / totalGb) * 100);
                UsageBar.Visibility = Visibility.Visible;
            }

            // Breadcrumb
            UpdateBreadcrumb(path);

            var sorted = folders.OrderByDescending(f => f.SizeBytes).ToList();
            var top = sorted.Take(30).ToList();

            // Treemap
            TreeMapHeader.Text = $"Disk Usage Map — {top.Count} folders in {IOPath.GetFileName(path.TrimEnd('\\')) ?? path}";
            TreeMapHeader.Visibility = Visibility.Visible;
            RenderTreeMap(top);
            TreeMapContainer.Visibility = Visibility.Visible;

            // File type distribution
            if (fileTypes.Count > 0)
            {
                FileTypesHeader.Text = S._("space.fileTypesHeader");
                FileTypesHeader.Visibility = Visibility.Visible;
                RenderFileTypes(fileTypes);
                FileTypesList.Visibility = Visibility.Visible;
            }

            // Folder list with drill-down + open in explorer
            TopFoldersHeader.Text = $"Largest Folders ({sorted.Count} total)";
            TopFoldersHeader.Visibility = Visibility.Visible;
            var totalUsed = sorted.Sum(f => f.SizeBytes);
            RenderFolderList(sorted.Take(25).ToList(), totalUsed);

            StatusText.Text = $"Scan complete — {folders.Count} folders analyzed";
        }
        catch (Exception ex) { StatusText.Text = S._("common.errorPrefix") + ex.Message; }
        finally
        {
            ScanBtn.IsEnabled = true;
            Progress.IsActive = false; Progress.Visibility = Visibility.Collapsed;
        }
    }

    // ── BREADCRUMB ────────────────────────────────────────────

    private void UpdateBreadcrumb(string path)
    {
        BreadcrumbBar.Children.Clear();

        var parts = new List<(string name, string fullPath)>();
        var current = path.TrimEnd('\\');
        while (!string.IsNullOrEmpty(current))
        {
            var name = IOPath.GetFileName(current);
            if (string.IsNullOrEmpty(name)) name = current; // drive root
            parts.Insert(0, (name, current + "\\"));
            var parent = IOPath.GetDirectoryName(current);
            if (parent == current) break;
            current = parent ?? "";
        }

        for (int i = 0; i < parts.Count; i++)
        {
            var (name, fullPath) = parts[i];
            var isLast = i == parts.Count - 1;

            if (!isLast)
            {
                var btn = new HyperlinkButton { Content = name, Padding = new Thickness(4, 2, 4, 2), FontSize = 12 };
                var capturedPath = fullPath;
                btn.Click += async (s, ev) => await ScanPathAsync(capturedPath);
                BreadcrumbBar.Children.Add(btn);
                BreadcrumbBar.Children.Add(new TextBlock { Text = "›", Opacity = 0.4, VerticalAlignment = VerticalAlignment.Center, FontSize = 14 });
            }
            else
            {
                BreadcrumbBar.Children.Add(new TextBlock
                {
                    Text = name, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    VerticalAlignment = VerticalAlignment.Center, FontSize = 12
                });
            }
        }

        BreadcrumbBar.Visibility = Visibility.Visible;
    }

    // ── SCANNING ──────────────────────────────────────────────

    private static (List<FolderEntry> folders, Dictionary<string, long> fileTypes) ScanFolder(string path)
    {
        var results = new List<FolderEntry>();
        var fileTypes = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var dirs = Directory.GetDirectories(path);
            foreach (var dir in dirs)
            {
                try
                {
                    var name = IOPath.GetFileName(dir);
                    var attrs = File.GetAttributes(dir);
                    if (attrs.HasFlag(FileAttributes.ReparsePoint)) continue;

                    var size = GetFolderSize(dir, maxDepth: 4);
                    if (size > 1024 * 1024) // > 1 MB
                        results.Add(new FolderEntry(name, dir, size, 0));
                }
                catch { }
            }

            // Collect file types from current directory's files
            try
            {
                foreach (var file in Directory.EnumerateFiles(path))
                {
                    try
                    {
                        var ext = IOPath.GetExtension(file).ToLowerInvariant();
                        if (string.IsNullOrEmpty(ext)) ext = "(no extension)";
                        var size = new FileInfo(file).Length;
                        fileTypes[ext] = fileTypes.GetValueOrDefault(ext) + size;
                    }
                    catch { }
                }
            }
            catch { }

            // Also collect file types from subfolders (first level only)
            foreach (var dir in dirs)
            {
                try
                {
                    var attrs = File.GetAttributes(dir);
                    if (attrs.HasFlag(FileAttributes.ReparsePoint)) continue;
                    foreach (var file in Directory.EnumerateFiles(dir))
                    {
                        try
                        {
                            var ext = IOPath.GetExtension(file).ToLowerInvariant();
                            if (string.IsNullOrEmpty(ext)) ext = "(no extension)";
                            var size = new FileInfo(file).Length;
                            fileTypes[ext] = fileTypes.GetValueOrDefault(ext) + size;
                        }
                        catch { }
                    }
                }
                catch { }
            }
        }
        catch { }

        return (results, fileTypes);
    }

    private static long GetFolderSize(string path, int maxDepth, int currentDepth = 0)
    {
        long total = 0;
        try
        {
            foreach (var file in Directory.EnumerateFiles(path))
            {
                try { total += new FileInfo(file).Length; } catch { }
            }
            if (currentDepth < maxDepth)
            {
                foreach (var sub in Directory.EnumerateDirectories(path))
                {
                    try
                    {
                        var attrs = File.GetAttributes(sub);
                        if (!attrs.HasFlag(FileAttributes.ReparsePoint))
                            total += GetFolderSize(sub, maxDepth, currentDepth + 1);
                    }
                    catch { }
                }
            }
        }
        catch { }
        return total;
    }

    // ── FILE TYPE DISTRIBUTION ─────────────────────────────────

    private void RenderFileTypes(Dictionary<string, long> fileTypes)
    {
        FileTypesList.Children.Clear();
        var sorted = fileTypes.OrderByDescending(kv => kv.Value).Take(10).ToList();
        if (sorted.Count == 0) return;
        var totalSize = sorted.Sum(kv => kv.Value);

        foreach (var (ext, size) in sorted)
        {
            var pct = totalSize > 0 ? (double)size / totalSize * 100 : 0;
            var colorIdx = Math.Abs(ext.GetHashCode()) % Palette.Length;
            var color = Palette[colorIdx];

            var row = new Grid { ColumnSpacing = 8, Padding = new Thickness(8, 4, 8, 4) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(50) });

            var extText = new TextBlock { Text = ext, FontSize = 12, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(extText, 0); row.Children.Add(extText);

            var sizeText = new TextBlock { Text = FormatBytes(size), FontSize = 11, Opacity = 0.6, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(sizeText, 1); row.Children.Add(sizeText);

            var bar = new ProgressBar { Minimum = 0, Maximum = 100, Value = pct, Height = 6, CornerRadius = new CornerRadius(3),
                Foreground = new SolidColorBrush(color), VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(bar, 2); row.Children.Add(bar);

            var pctText = new TextBlock { Text = $"{pct:F0}%", FontSize = 11, Opacity = 0.5, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(pctText, 3); row.Children.Add(pctText);

            FileTypesList.Children.Add(row);
        }
    }

    // ── TREEMAP RENDERING ─────────────────────────────────────

    private void RenderTreeMap(List<FolderEntry> folders)
    {
        TreeMapCanvas.Children.Clear();
        if (folders.Count == 0) return;

        double canvasW = 920, canvasH = 300;
        TreeMapCanvas.Width = canvasW;
        TreeMapCanvas.Height = canvasH;

        var totalSize = folders.Sum(f => f.SizeBytes);
        if (totalSize == 0) return;

        LayoutStrip(folders, 0, 0, canvasW, canvasH, totalSize, true);
    }

    private void LayoutStrip(List<FolderEntry> items, double x, double y, double w, double h,
        long totalSize, bool horizontal)
    {
        if (items.Count == 0 || w < 2 || h < 2) return;

        if (items.Count == 1)
        {
            AddRect(items[0], x, y, w, h);
            return;
        }

        long halfTarget = totalSize / 2;
        long runningSum = 0;
        int splitIdx = 0;
        for (int i = 0; i < items.Count; i++)
        {
            runningSum += items[i].SizeBytes;
            if (runningSum >= halfTarget) { splitIdx = i + 1; break; }
        }
        splitIdx = Math.Clamp(splitIdx, 1, items.Count - 1);

        var group1 = items.Take(splitIdx).ToList();
        var group2 = items.Skip(splitIdx).ToList();
        var size1 = group1.Sum(f => f.SizeBytes);
        var size2 = group2.Sum(f => f.SizeBytes);
        var ratio = (double)size1 / totalSize;

        if (horizontal)
        {
            var w1 = w * ratio;
            LayoutStrip(group1, x, y, w1, h, size1, !horizontal);
            LayoutStrip(group2, x + w1, y, w - w1, h, size2, !horizontal);
        }
        else
        {
            var h1 = h * ratio;
            LayoutStrip(group1, x, y, w, h1, size1, !horizontal);
            LayoutStrip(group2, x, y + h1, w, h - h1, size2, !horizontal);
        }
    }

    private void AddRect(FolderEntry folder, double x, double y, double w, double h)
    {
        if (w < 3 || h < 3) return;

        var colorIdx = Math.Abs(folder.Name.GetHashCode()) % Palette.Length;
        var color = Palette[colorIdx];

        var rect = new Rectangle
        {
            Width = Math.Max(w - 2, 1), Height = Math.Max(h - 2, 1),
            Fill = new SolidColorBrush(Color.FromArgb(180, color.R, color.G, color.B)),
            RadiusX = 3, RadiusY = 3,
            Stroke = new SolidColorBrush(Color.FromArgb(60, 0, 0, 0)), StrokeThickness = 0.5
        };

        // Click to drill down
        rect.Tapped += async (s, ev) =>
        {
            ev.Handled = true;
            await ScanPathAsync(folder.FullPath);
        };

        Canvas.SetLeft(rect, x + 1);
        Canvas.SetTop(rect, y + 1);
        TreeMapCanvas.Children.Add(rect);

        if (w > 50 && h > 25)
        {
            var sizeStr = FormatBytes(folder.SizeBytes);
            var label = new TextBlock
            {
                Text = w > 100 ? $"{folder.Name}\n{sizeStr}" : folder.Name,
                FontSize = w > 120 ? 11 : 9,
                Foreground = new SolidColorBrush(Colors.White),
                MaxWidth = w - 8, TextTrimming = TextTrimming.CharacterEllipsis,
                TextWrapping = TextWrapping.NoWrap, Opacity = 0.95, IsHitTestVisible = false
            };
            Canvas.SetLeft(label, x + 5);
            Canvas.SetTop(label, y + 4);
            TreeMapCanvas.Children.Add(label);
        }

        ToolTipService.SetToolTip(rect, $"{folder.Name}\n{FormatBytes(folder.SizeBytes)}\n{folder.FullPath}\n\nClick to drill down");
    }

    // ── FOLDER LIST ───────────────────────────────────────────

    private void RenderFolderList(List<FolderEntry> folders, long totalUsed)
    {
        TopFoldersList.Children.Clear();

        // Header
        var header = new Grid { ColumnSpacing = 8, Padding = new Thickness(12, 8, 12, 8),
            Background = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["CardBackgroundFillColorSecondaryBrush"],
            CornerRadius = new CornerRadius(6) };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        AddText(header, "#", 0, true, 24);
        AddText(header, "Folder", 1, true);
        AddText(header, "Size", 2, true);
        AddText(header, "% Used", 3, true);
        AddText(header, "Bar", 4, true);
        AddText(header, "", 5, true);
        TopFoldersList.Children.Add(header);

        int rank = 1;
        foreach (var folder in folders)
        {
            var pct = totalUsed > 0 ? (double)folder.SizeBytes / totalUsed * 100 : 0;
            var colorIdx = Math.Abs(folder.Name.GetHashCode()) % Palette.Length;
            var color = Palette[colorIdx];

            var row = new Grid { ColumnSpacing = 8, Padding = new Thickness(12, 6, 12, 6),
                BorderBrush = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["CardStrokeColorDefaultBrush"],
                BorderThickness = new Thickness(0, 0, 0, 0.5) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Rank with color dot
            var rankPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, MinWidth = 24 };
            rankPanel.Children.Add(new Border { Width = 10, Height = 10, CornerRadius = new CornerRadius(2),
                Background = new SolidColorBrush(color), VerticalAlignment = VerticalAlignment.Center });
            rankPanel.Children.Add(new TextBlock { Text = $"{rank}", FontSize = 12, Opacity = 0.5, VerticalAlignment = VerticalAlignment.Center });
            Grid.SetColumn(rankPanel, 0); row.Children.Add(rankPanel);

            // Name (clickable for drill-down)
            var nameBtn = new HyperlinkButton { Padding = new Thickness(0) };
            var nameStack = new StackPanel { Spacing = 1 };
            nameStack.Children.Add(new TextBlock { Text = folder.Name, FontSize = 13, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
            nameStack.Children.Add(new TextBlock { Text = folder.FullPath, FontSize = 10, Opacity = 0.35 });
            nameBtn.Content = nameStack;
            var capturedPath = folder.FullPath;
            nameBtn.Click += async (s, ev) => await ScanPathAsync(capturedPath);
            Grid.SetColumn(nameBtn, 1); row.Children.Add(nameBtn);

            AddText(row, FormatBytes(folder.SizeBytes), 2, false);
            AddText(row, $"{pct:F1}%", 3, false, opacity: 0.6);

            var bar = new ProgressBar { Minimum = 0, Maximum = 100, Value = pct, Height = 6, CornerRadius = new CornerRadius(3),
                VerticalAlignment = VerticalAlignment.Center, Foreground = new SolidColorBrush(color) };
            Grid.SetColumn(bar, 4); row.Children.Add(bar);

            // Open in Explorer button
            var openBtn = new Button { Content = "Open", Padding = new Thickness(8, 2, 8, 2), FontSize = 10,
                VerticalAlignment = VerticalAlignment.Center };
            var openPath = folder.FullPath;
            openBtn.Click += (s, ev) =>
            {
                try { System.Diagnostics.Process.Start("explorer.exe", openPath); } catch { }
            };
            Grid.SetColumn(openBtn, 5); row.Children.Add(openBtn);

            TopFoldersList.Children.Add(row);
            rank++;
        }
    }

    private static void AddText(Grid g, string text, int col, bool header, double minWidth = 0, double opacity = 1)
    {
        var tb = new TextBlock { Text = text, FontSize = header ? 11 : 12,
            FontWeight = header ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal,
            VerticalAlignment = VerticalAlignment.Center, Opacity = opacity };
        if (minWidth > 0) tb.MinWidth = minWidth;
        Grid.SetColumn(tb, col); g.Children.Add(tb);
    }

    private static string FormatBytes(long b) => b switch
    {
        < 1024 => $"{b} B",
        < 1024 * 1024 => $"{b / 1024.0:F0} KB",
        < 1024L * 1024 * 1024 => $"{b / (1024.0 * 1024):F1} MB",
        _ => $"{b / (1024.0 * 1024 * 1024):F2} GB"
    };
}
