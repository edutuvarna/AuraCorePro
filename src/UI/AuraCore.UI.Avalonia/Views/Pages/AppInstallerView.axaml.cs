using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.RegularExpressions;
using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Input;
using global::Avalonia.Interactivity;
using global::Avalonia.Layout;
using global::Avalonia.Media;
using AuraCore.Module.AppInstaller;
using AuraCore.Module.AppInstaller.Models;
using Microsoft.Extensions.DependencyInjection;
using AuraCore.Application.Interfaces.Modules;

namespace AuraCore.UI.Avalonia.Views.Pages;

public partial class AppInstallerView : UserControl
{
    private record AppInfo(string Name, string Id, string Version, string Available, string Source);

    private string _activeTab = "search";
    private readonly AppInstallerModule? _module;

    // ── Queue system ─────────────────────────────────────
    private readonly Queue<QueueItem> _installQueue = new();
    private bool _queueRunning;
    private CancellationTokenSource? _queueCts;

    private record QueueItem(string AppId, string DisplayName);

    // ── Bundle state ─────────────────────────────────────
    private AppBundle? _selectedBundle;
    private readonly HashSet<string> _selectedBundleApps = new();

    // ── Dependency detection ─────────────────────────────
    private static readonly Dictionary<string, string[]> CommonDependencies = new()
    {
        ["Microsoft.DotNet.DesktopRuntime.8"] = new[] { "JetBrains.Rider", "Microsoft.VisualStudioCode" },
        ["Microsoft.VCRedist.2015+.x64"] = new[] { "Valve.Steam", "Discord.Discord", "EpicGames.EpicGamesLauncher",
            "GOG.Galaxy", "OBSProject.OBSStudio", "BlenderFoundation.Blender" },
        ["Microsoft.DotNet.Runtime.8"] = new[] { "Microsoft.PowerToys" },
    };

    public AppInstallerView()
    {
        InitializeComponent();

        // Resolve the module from DI
        var modules = App.Services.GetServices<IOptimizationModule>();
        _module = modules.OfType<AppInstallerModule>().FirstOrDefault();
    }

    // ── Tab switching ────────────────────────────────────────

    private void Tab_Click(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Border border) return;
        var tag = border.Tag?.ToString();
        if (string.IsNullOrEmpty(tag) || tag == _activeTab) return;

        _activeTab = tag;
        UpdateTabVisuals();

        switch (tag)
        {
            case "search":
                break;
            case "bundles":
                LoadBundles();
                break;
            case "installed":
                _ = LoadInstalledAsync();
                break;
            case "updates":
                _ = LoadUpdatesAsync();
                break;
        }
    }

    private void UpdateTabVisuals()
    {
        SetTabActive(SearchTab, _activeTab == "search");
        SetTabActive(BundlesTab, _activeTab == "bundles");
        SetTabActive(InstalledTab, _activeTab == "installed");
        SetTabActive(UpdatesTab, _activeTab == "updates");

        SearchPanel.IsVisible = _activeTab == "search";
        BundlesPanel.IsVisible = _activeTab == "bundles";
        InstalledPanel.IsVisible = _activeTab == "installed";
        UpdatesPanel.IsVisible = _activeTab == "updates";
    }

    private static void SetTabActive(Border tab, bool active)
    {
        tab.Background = new SolidColorBrush(
            active ? Color.Parse("#00D4AA") : Color.Parse("#FFFFFF"),
            active ? 0.15 : 0.04);

        if (tab.Child is TextBlock tb)
        {
            tb.Foreground = active
                ? new SolidColorBrush(Color.Parse("#00D4AA"))
                : new SolidColorBrush(Color.Parse("#8888A0"));
        }
    }

    // ── Search ───────────────────────────────────────────────

    private void SearchBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            var query = SearchBox.Text?.Trim();
            if (!string.IsNullOrEmpty(query))
                _ = SearchAsync(query);
        }
    }

    private async Task SearchAsync(string query)
    {
        SetStatus($"Searching for \"{query}\"...");
        SearchEmpty.IsVisible = false;
        SearchResults.ItemsSource = null;

        try
        {
            var output = await RunWinget($"search \"{query}\" --accept-source-agreements");
            var apps = ParseWingetTable(output, includeAvailable: false);

            if (apps.Count == 0)
            {
                SearchEmpty.Text = "No results found";
                SearchEmpty.IsVisible = true;
                SetStatus("No results");
                return;
            }

            var items = new List<Control>();
            foreach (var app in apps.Take(25))
                items.Add(BuildSearchResultRow(app));

            SearchResults.ItemsSource = items;
            SetStatus($"{apps.Count} result(s) found");
        }
        catch (Exception ex)
        {
            SearchEmpty.Text = "Search failed - is winget installed?";
            SearchEmpty.IsVisible = true;
            SetStatus($"Error: {ex.Message}");
        }
    }

    private Control BuildSearchResultRow(AppInfo app)
    {
        var row = new Border
        {
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10, 8),
            Margin = new Thickness(0, 2),
            Background = new SolidColorBrush(Color.Parse("#FFFFFF"), 0.02),
        };

        var grid = new Grid
        {
            ColumnDefinitions = ColumnDefinitions.Parse("*,Auto,Auto"),
        };

        var info = new StackPanel { Spacing = 2 };
        info.Children.Add(new TextBlock
        {
            Text = app.Name, FontSize = 12, FontWeight = FontWeight.SemiBold,
            Foreground = new SolidColorBrush(Color.Parse("#E0E0F0"))
        });
        info.Children.Add(new TextBlock
        {
            Text = $"{app.Id}  v{app.Version}", FontSize = 10,
            Foreground = new SolidColorBrush(Color.Parse("#8888A0"))
        });

        // Queue button (adds to queue instead of direct install)
        var queueBtn = new Button
        {
            Content = "Add to Queue", FontSize = 10, Padding = new Thickness(10, 4),
            VerticalAlignment = VerticalAlignment.Center,
            Tag = $"{app.Id}|{app.Name}",
            Margin = new Thickness(6, 0, 0, 0)
        };
        queueBtn.Click += QueueBtn_Click;
        Grid.SetColumn(queueBtn, 1);

        var installBtn = new Button
        {
            Content = "Install", FontSize = 10, Padding = new Thickness(10, 4),
            VerticalAlignment = VerticalAlignment.Center,
            Tag = app.Id,
        };
        installBtn.Classes.Add("accent");
        installBtn.Click += InstallBtn_Click;
        Grid.SetColumn(installBtn, 2);

        grid.Children.Add(info);
        grid.Children.Add(queueBtn);
        grid.Children.Add(installBtn);

        row.Child = grid;
        return row;
    }

    private async void InstallBtn_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button btn) return;
        var appId = btn.Tag?.ToString();
        if (string.IsNullOrEmpty(appId)) return;

        btn.IsEnabled = false;
        btn.Content = "Installing...";
        var name = appId.Split('.').Last();
        SetStatus($"Installing {name}...");
        StatusBarService.SetStatus($"Installing {name}...");

        try
        {
            await RunWinget($"install --id {appId} --accept-source-agreements --accept-package-agreements --silent");
            btn.Content = "Installed";
            SetStatus($"{name} installed successfully");
            StatusBarService.SetStatus($"{name} installed successfully");
        }
        catch
        {
            btn.Content = "Failed";
            btn.IsEnabled = true;
            SetStatus($"Failed to install {name}");
            StatusBarService.SetStatus($"Failed to install {name}");
        }
    }

    // ── Queue system ─────────────────────────────────────────

    private void QueueBtn_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button btn) return;
        var tag = btn.Tag?.ToString();
        if (string.IsNullOrEmpty(tag)) return;

        var parts = tag.Split('|', 2);
        var appId = parts[0];
        var appName = parts.Length > 1 ? parts[1] : appId.Split('.').Last();

        // Avoid duplicates
        if (_installQueue.Any(q => q.AppId == appId)) return;

        _installQueue.Enqueue(new QueueItem(appId, appName));
        btn.Content = "Queued";
        btn.IsEnabled = false;
        RefreshQueueUI();

        // Auto-start queue if not running
        if (!_queueRunning)
            _ = ProcessQueueAsync();
    }

    private void EnqueueApps(IEnumerable<(string Id, string Name)> apps)
    {
        foreach (var (id, name) in apps)
        {
            if (!_installQueue.Any(q => q.AppId == id))
                _installQueue.Enqueue(new QueueItem(id, name));
        }
        RefreshQueueUI();
        if (!_queueRunning)
            _ = ProcessQueueAsync();
    }

    private async Task ProcessQueueAsync()
    {
        if (_queueRunning) return;
        _queueRunning = true;
        _queueCts = new CancellationTokenSource();
        CancelQueueBtn.IsVisible = true;

        int totalInBatch = _installQueue.Count;
        int processed = 0;

        // Check dependencies first
        await CheckAndOfferDependencies();

        while (_installQueue.Count > 0)
        {
            if (_queueCts.Token.IsCancellationRequested) break;

            var item = _installQueue.Dequeue();
            processed++;

            SetStatus($"Installing {item.DisplayName} ({processed}/{totalInBatch})...");
            StatusBarService.SetStatus($"Installing {item.DisplayName} ({processed}/{totalInBatch})...");
            StatusBarService.SetProgress($"{processed}/{totalInBatch}", (double)processed / totalInBatch);

            RefreshQueueUI(item.AppId, "installing");

            try
            {
                await RunWinget($"install --id {item.AppId} --accept-source-agreements --accept-package-agreements --silent");
                RefreshQueueUI(item.AppId, "done");
            }
            catch
            {
                RefreshQueueUI(item.AppId, "failed");
            }
        }

        _queueRunning = false;
        CancelQueueBtn.IsVisible = false;
        StatusBarService.ClearProgress();

        if (_queueCts.Token.IsCancellationRequested)
        {
            _installQueue.Clear();
            SetStatus("Queue cancelled");
            StatusBarService.SetStatus("Install queue cancelled");
        }
        else
        {
            SetStatus($"Queue complete - {processed} app(s) processed");
            StatusBarService.SetStatus($"Installed {processed} app(s)");
        }

        RefreshQueueUI();
    }

    private void CancelQueue_Click(object? sender, RoutedEventArgs e)
    {
        _queueCts?.Cancel();
    }

    private readonly List<(string AppId, string Name, string Status)> _queueDisplayItems = new();

    private void RefreshQueueUI(string? currentId = null, string? newStatus = null)
    {
        // Update status of current item
        if (currentId != null && newStatus != null)
        {
            var existing = _queueDisplayItems.FindIndex(x => x.AppId == currentId);
            if (existing >= 0)
                _queueDisplayItems[existing] = (_queueDisplayItems[existing].AppId,
                    _queueDisplayItems[existing].Name, newStatus);
        }

        // Add any new queue items not yet in display list
        foreach (var qi in _installQueue)
        {
            if (!_queueDisplayItems.Any(d => d.AppId == qi.AppId))
                _queueDisplayItems.Add((qi.AppId, qi.DisplayName, "pending"));
        }

        QueuePanel.IsVisible = _queueDisplayItems.Count > 0;
        QueueTitle.Text = $"Install Queue ({_queueDisplayItems.Count} app(s))";

        var controls = new List<Control>();
        foreach (var (appId, name, status) in _queueDisplayItems)
        {
            var row = new Border
            {
                Padding = new Thickness(8, 4),
                Margin = new Thickness(0, 1),
                CornerRadius = new CornerRadius(4),
                Background = new SolidColorBrush(Color.Parse("#FFFFFF"), 0.02),
            };

            var grid = new Grid { ColumnDefinitions = ColumnDefinitions.Parse("Auto,*,Auto") };

            // Status icon
            var (icon, color) = status switch
            {
                "installing" => ("\u25B6", "#00D4AA"),
                "done" => ("\u2714", "#22C55E"),
                "failed" => ("\u2716", "#EF4444"),
                _ => ("\u25CB", "#8888A0")  // pending
            };

            grid.Children.Add(new TextBlock
            {
                Text = icon, FontSize = 10, Foreground = new SolidColorBrush(Color.Parse(color)),
                VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0)
            });

            var nameBlock = new TextBlock
            {
                Text = name, FontSize = 11,
                Foreground = new SolidColorBrush(Color.Parse("#E0E0F0")),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(nameBlock, 1);
            grid.Children.Add(nameBlock);

            var statusText = new TextBlock
            {
                Text = status switch
                {
                    "installing" => "Installing...",
                    "done" => "Installed",
                    "failed" => "Failed",
                    _ => "Waiting"
                },
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.Parse(color)),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(statusText, 2);
            grid.Children.Add(statusText);

            row.Child = grid;
            controls.Add(row);
        }

        QueueList.ItemsSource = controls;

        // Clean up completed items after display is refreshed (keep them visible for context)
        if (!_queueRunning && _installQueue.Count == 0)
        {
            // Will be cleared on next enqueue
        }
    }

    // ── Dependency detection ─────────────────────────────────

    private async Task CheckAndOfferDependencies()
    {
        if (!OperatingSystem.IsWindows()) return;

        var queuedIds = _installQueue.Select(q => q.AppId).ToHashSet();
        var neededDeps = new List<string>();

        foreach (var (depId, appsNeedingIt) in CommonDependencies)
        {
            if (queuedIds.Overlaps(appsNeedingIt))
            {
                // Check if dependency is installed
                try
                {
                    var output = await RunWinget($"list --id {depId} --accept-source-agreements");
                    if (!output.Contains(depId, StringComparison.OrdinalIgnoreCase))
                        neededDeps.Add(depId);
                }
                catch { /* skip check */ }
            }
        }

        if (neededDeps.Count > 0)
        {
            var depNames = string.Join(", ", neededDeps.Select(d => d.Split('.').Last()));
            SetStatus($"Installing dependencies: {depNames}...");
            StatusBarService.SetStatus($"Installing dependencies: {depNames}...");

            foreach (var depId in neededDeps)
            {
                try
                {
                    await RunWinget($"install --id {depId} --accept-source-agreements --accept-package-agreements --silent");
                }
                catch { /* continue */ }
            }

            SetStatus("Dependencies installed. Proceeding with queue...");
        }
    }

    // ── Bundles ──────────────────────────────────────────────

    private void LoadBundles()
    {
        if (BundleCards.Children.Count > 0) return; // already loaded

        var bundles = AppBundles.GetAll();
        foreach (var bundle in bundles)
        {
            var card = new Border
            {
                Width = 150, Padding = new Thickness(12, 10),
                Margin = new Thickness(0, 0, 8, 8),
                CornerRadius = new CornerRadius(8),
                Cursor = new Cursor(StandardCursorType.Hand),
                Background = new SolidColorBrush(Color.Parse("#FFFFFF"), 0.03),
                BorderBrush = new SolidColorBrush(Color.Parse("#FFFFFF"), 0.06),
                BorderThickness = new Thickness(1),
                Tag = bundle,
            };
            card.PointerPressed += BundleCard_Click;

            var stack = new StackPanel { Spacing = 4 };
            stack.Children.Add(new TextBlock
            {
                Text = bundle.Name, FontSize = 12, FontWeight = FontWeight.SemiBold,
                Foreground = new SolidColorBrush(Color.Parse("#E0E0F0"))
            });
            stack.Children.Add(new TextBlock
            {
                Text = $"{bundle.Apps.Count} apps", FontSize = 10,
                Foreground = new SolidColorBrush(Color.Parse("#8888A0"))
            });
            stack.Children.Add(new TextBlock
            {
                Text = bundle.Description, FontSize = 9,
                Foreground = new SolidColorBrush(Color.Parse("#6666A0")),
                TextWrapping = global::Avalonia.Media.TextWrapping.Wrap
            });

            card.Child = stack;
            BundleCards.Children.Add(card);
        }
    }

    private void BundleCard_Click(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Border border || border.Tag is not AppBundle bundle) return;

        _selectedBundle = bundle;
        _selectedBundleApps.Clear();

        // Select all non-installed apps by default
        foreach (var app in bundle.Apps.Where(a => !a.IsInstalled))
            _selectedBundleApps.Add(app.WinGetId);

        BundleDetailTitle.Text = $"{bundle.Name} ({bundle.Apps.Count} apps)";
        BundleDetailPanel.IsVisible = true;
        RefreshBundleAppList();
    }

    private void RefreshBundleAppList()
    {
        if (_selectedBundle is null) return;

        var controls = new List<Control>();
        foreach (var app in _selectedBundle.Apps)
        {
            var row = new Border
            {
                Padding = new Thickness(8, 5), Margin = new Thickness(0, 1),
                CornerRadius = new CornerRadius(4),
                Background = new SolidColorBrush(Color.Parse("#FFFFFF"), 0.02),
            };

            var grid = new Grid { ColumnDefinitions = ColumnDefinitions.Parse("Auto,*,Auto") };

            var cb = new CheckBox
            {
                IsChecked = _selectedBundleApps.Contains(app.WinGetId),
                IsEnabled = !app.IsInstalled,
                Tag = app.WinGetId,
                VerticalAlignment = VerticalAlignment.Center
            };
            cb.IsCheckedChanged += BundleAppCheckChanged;
            grid.Children.Add(cb);

            var info = new StackPanel { Spacing = 1 };
            info.Children.Add(new TextBlock
            {
                Text = app.Name, FontSize = 11, FontWeight = FontWeight.SemiBold,
                Foreground = new SolidColorBrush(Color.Parse(app.IsInstalled ? "#6666A0" : "#E0E0F0"))
            });
            info.Children.Add(new TextBlock
            {
                Text = app.Description, FontSize = 9,
                Foreground = new SolidColorBrush(Color.Parse("#8888A0"))
            });
            Grid.SetColumn(info, 1);
            grid.Children.Add(info);

            if (app.IsInstalled)
            {
                var badge = new Border
                {
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(6, 2),
                    Background = new SolidColorBrush(Color.Parse("#22C55E"), 0.15),
                    VerticalAlignment = VerticalAlignment.Center,
                };
                badge.Child = new TextBlock
                {
                    Text = "Installed", FontSize = 9, FontWeight = FontWeight.SemiBold,
                    Foreground = new SolidColorBrush(Color.Parse("#22C55E"))
                };
                Grid.SetColumn(badge, 2);
                grid.Children.Add(badge);
            }

            row.Child = grid;
            controls.Add(row);
        }

        BundleAppList.ItemsSource = controls;
    }

    private void BundleAppCheckChanged(object? sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox cb) return;
        var id = cb.Tag?.ToString();
        if (string.IsNullOrEmpty(id)) return;

        if (cb.IsChecked == true)
            _selectedBundleApps.Add(id);
        else
            _selectedBundleApps.Remove(id);
    }

    private void BundleSelectAll_Click(object? sender, RoutedEventArgs e)
    {
        if (_selectedBundle is null) return;
        _selectedBundleApps.Clear();
        foreach (var app in _selectedBundle.Apps.Where(a => !a.IsInstalled))
            _selectedBundleApps.Add(app.WinGetId);
        RefreshBundleAppList();
    }

    private void BundleInstall_Click(object? sender, RoutedEventArgs e)
    {
        if (_selectedBundle is null || _selectedBundleApps.Count == 0) return;

        // Clear old queue display items for a fresh batch
        _queueDisplayItems.Clear();

        var appsToInstall = _selectedBundle.Apps
            .Where(a => _selectedBundleApps.Contains(a.WinGetId))
            .Select(a => (a.WinGetId, a.Name));

        EnqueueApps(appsToInstall);
    }

    // ── Installed ────────────────────────────────────────────

    private async Task LoadInstalledAsync()
    {
        SetStatus("Loading installed apps...");
        InstalledList.ItemsSource = null;

        try
        {
            var output = await RunWinget("list --accept-source-agreements");
            var apps = ParseWingetTable(output, includeAvailable: false);

            InstalledCount.Text = $"({apps.Count})";

            var items = new List<Control>();
            foreach (var app in apps)
                items.Add(BuildInstalledRow(app));

            InstalledList.ItemsSource = items;
            SetStatus($"{apps.Count} installed app(s)");
        }
        catch (Exception ex)
        {
            SetStatus($"Error loading installed apps: {ex.Message}");
        }
    }

    private static Control BuildInstalledRow(AppInfo app)
    {
        var row = new Border
        {
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10, 6),
            Margin = new Thickness(0, 1),
            Background = new SolidColorBrush(Color.Parse("#FFFFFF"), 0.02),
        };

        var grid = new Grid
        {
            ColumnDefinitions = ColumnDefinitions.Parse("*,Auto,Auto"),
        };

        var nameBlock = new TextBlock
        {
            Text = app.Name, FontSize = 11, FontWeight = FontWeight.SemiBold,
            Foreground = new SolidColorBrush(Color.Parse("#E0E0F0")),
            VerticalAlignment = VerticalAlignment.Center
        };

        var versionBlock = new TextBlock
        {
            Text = app.Version, FontSize = 10,
            Foreground = new SolidColorBrush(Color.Parse("#8888A0")),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0)
        };
        Grid.SetColumn(versionBlock, 1);

        var sourceBlock = new TextBlock
        {
            Text = app.Source, FontSize = 10,
            Foreground = new SolidColorBrush(Color.Parse("#6666A0")),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(sourceBlock, 2);

        grid.Children.Add(nameBlock);
        grid.Children.Add(versionBlock);
        grid.Children.Add(sourceBlock);

        row.Child = grid;
        return row;
    }

    // ── Updates ──────────────────────────────────────────────

    private async Task LoadUpdatesAsync()
    {
        SetStatus("Checking for updates...");
        UpdateList.ItemsSource = null;

        try
        {
            var output = await RunWinget("upgrade --accept-source-agreements");
            var apps = ParseWingetTable(output, includeAvailable: true);

            apps = apps.Where(a =>
                !string.IsNullOrEmpty(a.Id) &&
                !a.Name.Contains("upgrade(s) available", StringComparison.OrdinalIgnoreCase))
                .ToList();

            UpdateCount.Text = $"({apps.Count})";
            UpdateAllBtn.IsVisible = apps.Count > 0;

            var items = new List<Control>();
            foreach (var app in apps)
                items.Add(BuildUpdateRow(app));

            UpdateList.ItemsSource = items;
            SetStatus(apps.Count > 0 ? $"{apps.Count} update(s) available" : "All apps are up to date");
        }
        catch (Exception ex)
        {
            SetStatus($"Error checking updates: {ex.Message}");
        }
    }

    private Control BuildUpdateRow(AppInfo app)
    {
        var row = new Border
        {
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10, 6),
            Margin = new Thickness(0, 2),
            Background = new SolidColorBrush(Color.Parse("#FFFFFF"), 0.02),
        };

        var grid = new Grid
        {
            ColumnDefinitions = ColumnDefinitions.Parse("*,Auto,Auto,Auto"),
        };

        var nameBlock = new TextBlock
        {
            Text = app.Name, FontSize = 11, FontWeight = FontWeight.SemiBold,
            Foreground = new SolidColorBrush(Color.Parse("#E0E0F0")),
            VerticalAlignment = VerticalAlignment.Center
        };

        var currentBlock = new TextBlock
        {
            Text = app.Version, FontSize = 10,
            Foreground = new SolidColorBrush(Color.Parse("#8888A0")),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0)
        };
        Grid.SetColumn(currentBlock, 1);

        var arrow = new TextBlock
        {
            Text = "\u2192", FontSize = 10,
            Foreground = new SolidColorBrush(Color.Parse("#00D4AA")),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(4, 0)
        };
        Grid.SetColumn(arrow, 2);

        var availBlock = new TextBlock
        {
            Text = app.Available, FontSize = 10, FontWeight = FontWeight.SemiBold,
            Foreground = new SolidColorBrush(Color.Parse("#00D4AA")),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(4, 0, 8, 0)
        };

        var updateBtn = new Button
        {
            Content = "Update", FontSize = 10, Padding = new Thickness(10, 4),
            VerticalAlignment = VerticalAlignment.Center,
            Tag = app.Id,
        };
        updateBtn.Classes.Add("accent");
        updateBtn.Click += UpdateBtn_Click;

        var rightPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal, Spacing = 2,
            VerticalAlignment = VerticalAlignment.Center
        };
        rightPanel.Children.Add(availBlock);
        rightPanel.Children.Add(updateBtn);
        Grid.SetColumn(rightPanel, 3);

        grid.Children.Add(nameBlock);
        grid.Children.Add(currentBlock);
        grid.Children.Add(arrow);
        grid.Children.Add(rightPanel);

        row.Child = grid;
        return row;
    }

    private async void UpdateBtn_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button btn) return;
        var appId = btn.Tag?.ToString();
        if (string.IsNullOrEmpty(appId)) return;

        btn.IsEnabled = false;
        btn.Content = "Updating...";
        var name = appId.Split('.').Last();
        SetStatus($"Updating {name}...");
        StatusBarService.SetStatus($"Updating {name}...");

        try
        {
            await RunWinget($"upgrade --id {appId} --accept-source-agreements --accept-package-agreements --silent");
            btn.Content = "Done";
            SetStatus($"{name} updated successfully");
            StatusBarService.SetStatus($"{name} updated");
        }
        catch
        {
            btn.Content = "Failed";
            btn.IsEnabled = true;
            SetStatus($"Failed to update {name}");
            StatusBarService.SetStatus($"Failed to update {name}");
        }
    }

    private async void UpdateAll_Click(object? sender, RoutedEventArgs e)
    {
        UpdateAllBtn.IsEnabled = false;
        UpdateAllBtn.Content = "Updating all...";
        SetStatus("Updating all apps...");
        StatusBarService.SetStatus("Updating all apps...");

        try
        {
            await RunWinget("upgrade --all --accept-source-agreements --accept-package-agreements --silent");
            UpdateAllBtn.Content = "Done";
            SetStatus("All apps updated");
            StatusBarService.SetStatus("All apps updated");
        }
        catch
        {
            UpdateAllBtn.Content = "Failed";
            UpdateAllBtn.IsEnabled = true;
            SetStatus("Update all failed");
            StatusBarService.SetStatus("Update all failed");
        }
    }

    // ── Helpers ──────────────────────────────────────────────

    private static async Task<string> RunWinget(string args)
    {
        if (!OperatingSystem.IsWindows()) return "ERROR: winget is Windows-only";
        var psi = new ProcessStartInfo("winget", args)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8
        };
        using var proc = Process.Start(psi);
        if (proc == null) return "ERROR: Failed to start winget. Is it installed?";
        var output = await proc.StandardOutput.ReadToEndAsync();
        await proc.WaitForExitAsync();
        return output;
    }

    private static List<AppInfo> ParseWingetTable(string output, bool includeAvailable)
    {
        var results = new List<AppInfo>();
        if (string.IsNullOrEmpty(output)) return results;

        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        bool headerPassed = false;

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd();
            if (line.StartsWith("---"))
            {
                headerPassed = true;
                continue;
            }
            if (!headerPassed || line.StartsWith("Name", StringComparison.Ordinal))
                continue;
            if (string.IsNullOrWhiteSpace(line)) continue;
            if (line.Contains("upgrade(s) available") || line.Contains("winget"))
                continue;

            var parts = Regex.Split(line, @"\s{2,}")
                .Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();

            if (parts.Length >= 2)
            {
                results.Add(new AppInfo(
                    Name: parts[0].Trim(),
                    Id: parts.Length > 1 ? parts[1].Trim() : "",
                    Version: parts.Length > 2 ? parts[2].Trim() : "",
                    Available: includeAvailable && parts.Length > 3 ? parts[3].Trim() : "",
                    Source: parts.Length > (includeAvailable ? 4 : 3) ? parts[includeAvailable ? 4 : 3].Trim() : ""
                ));
            }
        }

        return results;
    }

    private void SetStatus(string text)
    {
        global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            StatusText.Text = text;
        });
    }
}
