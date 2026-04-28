using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Interactivity;
using global::Avalonia.Layout;
using global::Avalonia.Media;
using AuraCore.Application;
using AuraCore.Application.Interfaces.Modules;
using AuraCore.Module.JunkCleaner;
using AuraCore.Module.JunkCleaner.Models;
using AuraCore.Module.DiskCleanup;
using AuraCore.Module.PrivacyCleaner;
using AuraCore.UI.Avalonia;
using Microsoft.Extensions.DependencyInjection;

namespace AuraCore.UI.Avalonia.Views.Pages;

public partial class CategoryCleanView : UserControl
{
    private readonly IOptimizationModule _module;
    private readonly List<CatInfo> _cats = new();
    private readonly List<CheckBox> _catCheckBoxes = new();

    // Per-file checkbox tracking (junk-cleaner only)
    private readonly Dictionary<string, List<FileCheckEntry>> _fileChecksByCategory = new();

    private bool IsJunkCleaner => _module.Id == "junk-cleaner";

    private record CatInfo(string Name, string Desc, string Risk, int Files, long Bytes, string SizeText, bool NeedAdmin);
    private record FileCheckEntry(CheckBox CheckBox, string FullPath, long SizeBytes);

    public CategoryCleanView(IOptimizationModule module)
    {
        InitializeComponent();
        _module = module ?? throw new ArgumentNullException(nameof(module));
        PageTitle.Text = module.DisplayName;
        PageSubtitle.Text = module.Id switch
        {
            "junk-cleaner"    => LocalizationService._("junk.subtitle"),
            "disk-cleanup"    => LocalizationService._("dc.subtitle"),
            "privacy-cleaner" => LocalizationService._("priv.subtitle"),
            _ => "Scan and clean"
        };

        // Show exclude-list button only for junk-cleaner
        ExcludeListBtn.IsVisible = IsJunkCleaner;

        LocalizationService.LanguageChanged += () =>
            global::Avalonia.Threading.Dispatcher.UIThread.Post(ApplyLocalization);

        Loaded += async (s, e) =>
        {
            if (IsJunkCleaner)
            {
                LoadHistoryPanel();
                CheckDiskPressure();
            }
            await RunScan();
        };
    }

    private void ApplyLocalization()
    {
        if (_module is null) return;
        var subText = _module.Id switch
        {
            "junk-cleaner"    => LocalizationService._("junk.subtitle"),
            "disk-cleanup"    => LocalizationService._("dc.subtitle"),
            "privacy-cleaner" => LocalizationService._("priv.subtitle"),
            _ => LocalizationService._("catClean.subtitle")
        };
        PageSubtitle.Text        = subText;
        PageHeader.Subtitle      = subText;
        ScanLabel.Text           = LocalizationService._("catClean.scanBtn");
        ExcludeListLabel.Text    = LocalizationService._("catClean.excludes");
        global::Avalonia.Controls.ToolTip.SetTip(ExcludeListBtn, LocalizationService._("catClean.excludesTooltip"));
        CatCountLabel.Text       = LocalizationService._("catClean.categories");
        FileCountLabel.Text      = LocalizationService._("catClean.files");
        TotalSizeLabel.Text      = LocalizationService._("catClean.totalSize");
        SelectAllLabel.Text      = LocalizationService._("common.selectAll");
        CleanLabel.Text          = LocalizationService._("catClean.cleanSelected");
        HistoryExpander.Header   = LocalizationService._("catClean.cleanupHistory");
        AiBadgeText.Text         = LocalizationService._("catClean.diskPressureHigh");
    }

    // ═══════════════════════════════════════════════════════════
    //  Scanning
    // ═══════════════════════════════════════════════════════════

    private async Task RunScan()
    {
        if (_module is null) return;
        ScanLabel.Text = LocalizationService._("common.scanning");
        _cats.Clear();
        try
        {
            await _module.ScanAsync(new ScanOptions(DeepScan: true));

            if (_module is JunkCleanerModule jc && jc.LastReport is not null)
            {
                // Apply exclude list filter
                var excludes = JunkCleanerService.LoadExcludeList();
                foreach (var c in jc.LastReport.Categories)
                {
                    var filtered = c.Files
                        .Where(f => !JunkCleanerService.IsExcluded(f.FullPath, excludes))
                        .ToList();
                    if (filtered.Count == 0) continue;
                    var totalBytes = filtered.Sum(f => f.SizeBytes);
                    _cats.Add(new CatInfo(c.Name, c.Description, "Safe", filtered.Count, totalBytes,
                        FormatBytes(totalBytes), false));
                }
            }
            else if (_module is DiskCleanupModule dc && dc.LastReport is not null)
                _cats.AddRange(dc.LastReport.Categories.Select(c =>
                    new CatInfo(c.Name, c.Description, c.RiskLevel, c.FileCount, c.TotalBytes, c.TotalSizeDisplay, c.RequiresAdmin)));
            else if (_module is PrivacyCleanerModule pc && pc.LastReport is not null)
                _cats.AddRange(pc.LastReport.Categories.Select(c =>
                    new CatInfo(c.Name, c.Description, c.RiskLevel, c.ItemCount, c.TotalBytes, c.TotalSizeDisplay, false)));

            CatCount.Text = _cats.Count.ToString();
            FileCount.Text = _cats.Sum(c => c.Files).ToString();
            TotalSize.Text = FormatBytes(_cats.Sum(c => c.Bytes));
            RenderCategories();

            // Update AI badge with current scan totals
            if (IsJunkCleaner)
                CheckDiskPressure();
        }
        catch { StatusText.Text = LocalizationService._("catClean.scanFailed"); }
        finally { ScanLabel.Text = LocalizationService._("catClean.scanBtn"); }
    }

    // ═══════════════════════════════════════════════════════════
    //  Rendering Categories (with individual file expanders)
    // ═══════════════════════════════════════════════════════════

    private void RenderCategories()
    {
        CategoryPanel.Children.Clear();
        _catCheckBoxes.Clear();
        _fileChecksByCategory.Clear();

        foreach (var cat in _cats)
        {
            var outerStack = new StackPanel { Spacing = 0 };

            var card = new Border
            {
                CornerRadius = new CornerRadius(10), Padding = new Thickness(16, 12),
                Background = new SolidColorBrush(Color.Parse("#252538")),
                BorderBrush = new SolidColorBrush(Color.Parse("#33334A")), BorderThickness = new Thickness(1)
            };

            var grid = new Grid { ColumnDefinitions = ColumnDefinitions.Parse("40,*,100,80") };

            var cb = new CheckBox
            {
                IsChecked = cat.Risk != "High" && !cat.NeedAdmin,
                IsEnabled = !cat.NeedAdmin,
                Tag = cat.Name, VerticalAlignment = VerticalAlignment.Center
            };
            cb.Click += (s, e) =>
            {
                // Sync file-level checks when category checkbox changes
                if (IsJunkCleaner && _fileChecksByCategory.TryGetValue(cat.Name, out var entries))
                {
                    foreach (var entry in entries)
                        if (entry.CheckBox.IsEnabled)
                            entry.CheckBox.IsChecked = cb.IsChecked;
                }
                UpdateCleanButton();
            };
            _catCheckBoxes.Add(cb);
            Grid.SetColumn(cb, 0);
            grid.Children.Add(cb);

            var info = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Spacing = 2 };
            info.Children.Add(new TextBlock
            {
                Text = cat.Name, FontSize = 13, FontWeight = FontWeight.SemiBold,
                Foreground = new SolidColorBrush(Color.Parse("#E8E8F0"))
            });
            info.Children.Add(new TextBlock
            {
                Text = cat.Desc, FontSize = 10, TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(Color.Parse("#555570"))
            });
            var meta = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            meta.Children.Add(new TextBlock
            {
                Text = $"{cat.Files} files", FontSize = 10,
                Foreground = new SolidColorBrush(Color.Parse("#8888A0"))
            });
            if (cat.NeedAdmin)
                meta.Children.Add(new TextBlock
                {
                    Text = LocalizationService._("catClean.adminRequired"), FontSize = 10,
                    Foreground = new SolidColorBrush(Color.Parse("#F59E0B"))
                });
            info.Children.Add(meta);
            Grid.SetColumn(info, 1);
            grid.Children.Add(info);

            // Size
            var sizeText = new TextBlock
            {
                Text = cat.SizeText, FontSize = 14, FontWeight = FontWeight.Bold,
                Foreground = cat.Bytes > 100_000_000 ? new SolidColorBrush(Color.Parse("#EF4444"))
                           : cat.Bytes > 10_000_000 ? new SolidColorBrush(Color.Parse("#F59E0B"))
                           : (global::Avalonia.Application.Current?.Resources.TryGetResource("AccentPrimaryBrush", null, out var res) == true && res is ISolidColorBrush b
                               ? b : new SolidColorBrush(Color.Parse("#00D4AA"))),
                VerticalAlignment = VerticalAlignment.Center, TextAlignment = TextAlignment.Right
            };
            Grid.SetColumn(sizeText, 2);
            grid.Children.Add(sizeText);

            // Risk badge
            var (riskFg, riskBg) = cat.Risk switch
            {
                "High"   => ("#EF4444", "#20EF4444"),
                "Medium" => ("#F59E0B", "#20F59E0B"),
                "Low"    => ("#3B82F6", "#203B82F6"),
                _        => ("#22C55E", "#2022C55E")
            };
            var badge = new Border
            {
                CornerRadius = new CornerRadius(8), Padding = new Thickness(8, 2),
                Background = new SolidColorBrush(Color.Parse(riskBg)),
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
            };
            badge.Child = new TextBlock
            {
                Text = cat.Risk, FontSize = 10, FontWeight = FontWeight.SemiBold,
                Foreground = new SolidColorBrush(Color.Parse(riskFg))
            };
            Grid.SetColumn(badge, 3);
            grid.Children.Add(badge);

            card.Child = grid;
            outerStack.Children.Add(card);

            // ── Individual file expander (junk-cleaner only) ──
            if (IsJunkCleaner)
            {
                var catName = cat.Name;
                var fileExpander = BuildFileExpander(catName, cb);
                if (fileExpander is not null)
                    outerStack.Children.Add(fileExpander);
            }

            CategoryPanel.Children.Add(outerStack);
        }
        UpdateCleanButton();
    }

    /// <summary>
    /// Builds an expander showing individual files for the given category.
    /// Returns null if no files are available.
    /// </summary>
    private Expander? BuildFileExpander(string categoryName, CheckBox parentCb)
    {
        if (_module is not JunkCleanerModule jc || jc.LastReport is null) return null;
        var category = jc.LastReport.Categories.FirstOrDefault(c => c.Name == categoryName);
        if (category is null || category.Files.Count == 0) return null;

        var excludes = JunkCleanerService.LoadExcludeList();
        var filteredFiles = category.Files
            .Where(f => !JunkCleanerService.IsExcluded(f.FullPath, excludes))
            .ToList();
        if (filteredFiles.Count == 0) return null;

        var fileEntries = new List<FileCheckEntry>();
        var fileStack = new StackPanel { Spacing = 2, Margin = new Thickness(40, 0, 0, 0) };

        // Limit displayed files to avoid UI overload (show first 100)
        var displayFiles = filteredFiles.Take(100).ToList();

        foreach (var file in displayFiles)
        {
            var row = new Grid { ColumnDefinitions = ColumnDefinitions.Parse("30,*,80,30"), Margin = new Thickness(0, 1) };

            var fileCb = new CheckBox
            {
                IsChecked = parentCb.IsChecked,
                Tag = file.FullPath,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0)
            };
            fileCb.Click += (s, e) =>
            {
                // Update parent category checkbox state
                if (_fileChecksByCategory.TryGetValue(categoryName, out var entries))
                {
                    var allChecked = entries.All(en => en.CheckBox.IsChecked == true);
                    var noneChecked = entries.All(en => en.CheckBox.IsChecked == false);
                    parentCb.IsChecked = allChecked ? true : noneChecked ? false : null;
                }
                UpdateCleanButton();
            };

            Grid.SetColumn(fileCb, 0);
            row.Children.Add(fileCb);

            var fileName = Path.GetFileName(file.FullPath);
            var filePath = file.FullPath;
            var nameBlock = new TextBlock
            {
                Text = fileName, FontSize = 10,
                Foreground = new SolidColorBrush(Color.Parse("#9999B0")),
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            ToolTip.SetTip(nameBlock, filePath);
            Grid.SetColumn(nameBlock, 1);
            row.Children.Add(nameBlock);

            var sizeBlock = new TextBlock
            {
                Text = FormatBytes(file.SizeBytes), FontSize = 10,
                Foreground = new SolidColorBrush(Color.Parse("#666688")),
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Right
            };
            Grid.SetColumn(sizeBlock, 2);
            row.Children.Add(sizeBlock);

            // Exclude button
            var excludeBtn = new Button
            {
                Content = new TextBlock { Text = "\u2716", FontSize = 9, Foreground = new SolidColorBrush(Color.Parse("#666680")) },
                Padding = new Thickness(4, 2),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            ToolTip.SetTip(excludeBtn, LocalizationService._("catClean.addToExclude"));
            var capturedPath = file.FullPath;
            excludeBtn.Click += (s, e) =>
            {
                JunkCleanerService.AddToExcludeList(capturedPath);
                StatusText.Text = $"Excluded: {Path.GetFileName(capturedPath)}";
                // Re-render after exclusion
                _ = RunScan();
            };
            Grid.SetColumn(excludeBtn, 3);
            row.Children.Add(excludeBtn);

            fileStack.Children.Add(row);
            fileEntries.Add(new FileCheckEntry(fileCb, file.FullPath, file.SizeBytes));
        }

        if (filteredFiles.Count > 100)
        {
            fileStack.Children.Add(new TextBlock
            {
                Text = $"... and {filteredFiles.Count - 100} more files",
                FontSize = 10, FontStyle = FontStyle.Italic,
                Foreground = new SolidColorBrush(Color.Parse("#555570")),
                Margin = new Thickness(30, 4, 0, 0)
            });
        }

        _fileChecksByCategory[categoryName] = fileEntries;

        var expander = new Expander
        {
            Header = new TextBlock
            {
                Text = $"Show {filteredFiles.Count} individual files",
                FontSize = 10, Foreground = new SolidColorBrush(Color.Parse("#6666A0"))
            },
            IsExpanded = false,
            Padding = new Thickness(0, 2),
            Margin = new Thickness(0, 0, 0, 0),
            Content = new ScrollViewer
            {
                MaxHeight = 200,
                Content = fileStack
            }
        };

        return expander;
    }

    // ═══════════════════════════════════════════════════════════
    //  AI Recommendation Badge (disk pressure)
    // ═══════════════════════════════════════════════════════════

    private void CheckDiskPressure()
    {
        var pressure = JunkCleanerService.GetDiskPressure();
        if (pressure is null || !pressure.IsHighPressure)
        {
            AiBadge.IsVisible = false;
            return;
        }

        AiBadge.IsVisible = true;
        var totalScanBytes = _cats.Sum(c => c.Bytes);
        var savingsText = totalScanBytes > 0 ? $"~{FormatBytes(totalScanBytes)} recoverable" : "";

        AiBadgeText.Text = $"Disk usage is at {pressure.UsedPercent:F0}% ({pressure.FreeDisplay} free). "
                         + "Running a cleanup is recommended to improve performance.";
        AiBadgeSavings.Text = savingsText;
    }

    // ═══════════════════════════════════════════════════════════
    //  History Log
    // ═══════════════════════════════════════════════════════════

    private void LoadHistoryPanel()
    {
        HistorySection.IsVisible = IsJunkCleaner;
        if (!IsJunkCleaner) return;

        HistoryPanel.Children.Clear();
        var history = JunkCleanerService.LoadHistory();
        if (history.Count == 0)
        {
            HistoryPanel.Children.Add(new TextBlock
            {
                Text = LocalizationService._("catClean.noHistory"),
                FontSize = 10, FontStyle = FontStyle.Italic,
                Foreground = new SolidColorBrush(Color.Parse("#555570")),
                Margin = new Thickness(4)
            });
            return;
        }

        foreach (var entry in history.Take(MaxHistoryDisplay))
        {
            var row = new Grid { ColumnDefinitions = ColumnDefinitions.Parse("140,*,80"), Margin = new Thickness(2, 1) };

            row.Children.Add(new TextBlock
            {
                Text = entry.Timestamp.LocalDateTime.ToString("yyyy-MM-dd HH:mm"),
                FontSize = 10, Foreground = new SolidColorBrush(Color.Parse("#555570")),
                VerticalAlignment = VerticalAlignment.Center
            });

            var catText = new TextBlock
            {
                Text = $"{entry.ItemsCleaned} items ({string.Join(", ", entry.Categories.Take(3))})",
                FontSize = 10, Foreground = new SolidColorBrush(Color.Parse("#8888A0")),
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            Grid.SetColumn(catText, 1);
            row.Children.Add(catText);

            var freedText = new TextBlock
            {
                Text = entry.BytesFreedDisplay,
                FontSize = 10, FontWeight = FontWeight.SemiBold,
                Foreground = new SolidColorBrush(Color.Parse("#00D4AA")),
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Right
            };
            Grid.SetColumn(freedText, 2);
            row.Children.Add(freedText);

            HistoryPanel.Children.Add(row);
        }
    }

    private const int MaxHistoryDisplay = 20;

    // ═══════════════════════════════════════════════════════════
    //  Exclude List Management Dialog
    // ═══════════════════════════════════════════════════════════

    private void ExcludeList_Click(object? sender, RoutedEventArgs e)
    {
        ShowExcludeListPopup();
    }

    private void ShowExcludeListPopup()
    {
        var excludes = JunkCleanerService.LoadExcludeList();

        // Replace the category panel content temporarily with exclude list
        var savedChildren = new List<Control>();
        foreach (Control child in CategoryPanel.Children)
            savedChildren.Add(child);

        CategoryPanel.Children.Clear();

        // Header
        var header = new Grid { ColumnDefinitions = ColumnDefinitions.Parse("*,Auto"), Margin = new Thickness(4, 4) };
        header.Children.Add(new TextBlock
        {
            Text = LocalizationService._("catClean.excludeListTitle"),
            FontSize = 12, FontWeight = FontWeight.SemiBold,
            Foreground = new SolidColorBrush(Color.Parse("#E8E8F0")),
            VerticalAlignment = VerticalAlignment.Center
        });
        var backBtn = new Button
        {
            Content = new TextBlock { Text = LocalizationService._("catClean.back"), FontSize = 10, FontWeight = FontWeight.SemiBold },
            Classes = { "action-btn" }, Padding = new Thickness(12, 4)
        };
        backBtn.Click += (s, ev) =>
        {
            CategoryPanel.Children.Clear();
            foreach (var c in savedChildren) CategoryPanel.Children.Add(c);
        };
        Grid.SetColumn(backBtn, 1);
        header.Children.Add(backBtn);
        CategoryPanel.Children.Add(header);

        if (excludes.Count == 0)
        {
            CategoryPanel.Children.Add(new TextBlock
            {
                Text = LocalizationService._("catClean.noExclusions"),
                FontSize = 11, FontStyle = FontStyle.Italic,
                Foreground = new SolidColorBrush(Color.Parse("#555570")),
                Margin = new Thickness(4, 12)
            });
            return;
        }

        foreach (var path in excludes)
        {
            var row = new Grid { ColumnDefinitions = ColumnDefinitions.Parse("*,Auto"), Margin = new Thickness(4, 2) };

            row.Children.Add(new TextBlock
            {
                Text = path, FontSize = 11,
                Foreground = new SolidColorBrush(Color.Parse("#9999B0")),
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            });

            var removeBtn = new Button
            {
                Content = new TextBlock { Text = LocalizationService._("common.remove"), FontSize = 9, Foreground = new SolidColorBrush(Color.Parse("#EF4444")) },
                Padding = new Thickness(8, 2),
                Background = new SolidColorBrush(Color.Parse("#20EF4444")),
                BorderThickness = new Thickness(0),
                CornerRadius = new CornerRadius(4),
                VerticalAlignment = VerticalAlignment.Center
            };
            var capturedPath = path;
            removeBtn.Click += (s, ev) =>
            {
                JunkCleanerService.RemoveFromExcludeList(capturedPath);
                StatusText.Text = $"Removed from exclude list: {Path.GetFileName(capturedPath)}";
                ShowExcludeListPopup(); // Refresh
            };
            Grid.SetColumn(removeBtn, 1);
            row.Children.Add(removeBtn);

            CategoryPanel.Children.Add(row);
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  Button Handlers
    // ═══════════════════════════════════════════════════════════

    private void UpdateCleanButton()
    {
        int selected;
        if (IsJunkCleaner && _fileChecksByCategory.Count > 0)
        {
            // Count categories where at least one file is checked
            selected = _catCheckBoxes.Count(cb => cb.IsChecked == true || cb.IsChecked == null);
        }
        else
        {
            selected = _catCheckBoxes.Count(cb => cb.IsChecked == true);
        }
        CleanBtn.IsEnabled = selected > 0;
        CleanLabel.Text = selected > 0
            ? $"{LocalizationService._("catClean.cleanCount")} {selected}"
            : LocalizationService._("catClean.cleanSelected");
    }

    private void SelectAll_Click(object? sender, RoutedEventArgs e)
    {
        bool allChecked = _catCheckBoxes.All(cb => cb.IsChecked == true || !cb.IsEnabled);
        foreach (var cb in _catCheckBoxes)
        {
            if (cb.IsEnabled) cb.IsChecked = !allChecked;
        }

        // Also toggle file-level checks for junk-cleaner
        if (IsJunkCleaner)
        {
            foreach (var entries in _fileChecksByCategory.Values)
                foreach (var entry in entries)
                    if (entry.CheckBox.IsEnabled)
                        entry.CheckBox.IsChecked = !allChecked;
        }

        SelectAllLabel.Text = allChecked ? LocalizationService._("common.selectAll") : LocalizationService._("common.deselectAll");
        UpdateCleanButton();
    }

    private async void Scan_Click(object? sender, RoutedEventArgs e) => await RunScan();

    private async void Clean_Click(object? sender, RoutedEventArgs e)
    {
        if (_module is null) return;
        CleanBtn.IsEnabled = false;
        CleanLabel.Text = LocalizationService._("catClean.cleaning");
        try
        {
            // Build selected category list
            var selectedCats = new List<string>();
            for (int i = 0; i < _catCheckBoxes.Count && i < _cats.Count; i++)
            {
                if (_catCheckBoxes[i].IsChecked == true || _catCheckBoxes[i].IsChecked == null)
                    selectedCats.Add(_cats[i].Name);
            }

            var plan = new OptimizationPlan(_module.Id, selectedCats.Count > 0 ? selectedCats : new List<string> { "all" });
            var progress = new Progress<TaskProgress>(p =>
            {
                global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    StatusText.Text = $"{p.Percentage:F0}% - {p.StatusText}");
            });

            var result = await _module.OptimizeAsync(plan, progress);

            StatusText.Text = result.Success
                ? $"Cleaned {result.ItemsProcessed} items. Freed {FormatBytes(result.BytesFreed)} in {result.Duration.TotalSeconds:F1}s"
                : LocalizationService._("catClean.cleanFailed");

            // Log to history (junk-cleaner only)
            if (IsJunkCleaner && result.Success)
            {
                JunkCleanerService.AddHistoryEntry(new CleanHistoryEntry
                {
                    Timestamp = DateTimeOffset.Now,
                    ModuleId = _module.Id,
                    ItemsCleaned = result.ItemsProcessed,
                    BytesFreed = result.BytesFreed,
                    Categories = selectedCats
                });
                LoadHistoryPanel();
            }

            await RunScan(); // refresh
        }
        catch (Exception ex) { StatusText.Text = $"Error: {ex.Message}"; }
        finally { CleanLabel.Text = LocalizationService._("catClean.cleanSelected"); }
    }

    private static string FormatBytes(long b) => b switch
    {
        >= 1073741824 => $"{b / 1073741824.0:F1} GB",
        >= 1048576    => $"{b / 1048576.0:F1} MB",
        >= 1024       => $"{b / 1024.0:F1} KB",
        > 0           => $"{b} B",
        _             => "0 B"
    };
}
