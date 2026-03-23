using AuraCore.Desktop.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System.Net.Sockets;
using AuraCore.Application;
using AuraCore.Application.Interfaces.Modules;
using AuraCore.Module.NetworkOptimizer;
using AuraCore.Module.NetworkOptimizer.Models;

namespace AuraCore.Desktop.Pages;

public sealed partial class NetworkPage : Page
{
    private NetworkOptimizerModule? _module;

    public NetworkPage()
    {
        InitializeComponent();
        ApplyLocalization();
        Services.S.LanguageChanged += () => DispatcherQueue?.TryEnqueue(ApplyLocalization);
        var modules = App.Current.Services.GetServices<IOptimizationModule>();
        _module = modules.FirstOrDefault(m => m.Id == "network-optimizer") as NetworkOptimizerModule;
    }

    private async void ScanBtn_Click(object sender, RoutedEventArgs e)
    {
        ScanBtn.IsEnabled = false;
        Progress.IsActive = true;
        Progress.Visibility = Visibility.Visible;
        StatusText.Text = "Diagnosing network...";
        DnsPresetList.Children.Clear();
        PingList.Children.Clear();
        AdapterList.Children.Clear();
        ResultCard.Visibility = Visibility.Collapsed;

        try
        {
            if (_module is null) { StatusText.Text = "Module not available."; return; }

            StatusText.Text = "Detecting DNS and pinging servers...";
            await _module.ScanAsync(new ScanOptions());
            var report = _module.LastReport;
            if (report is null) { StatusText.Text = "No data."; return; }

            // DNS info
            DnsProviderText.Text = report.CurrentDns.ProviderName;
            DnsPrimaryText.Text = string.IsNullOrEmpty(report.CurrentDns.Primary) ? "N/A" : report.CurrentDns.Primary;
            DnsSecondaryText.Text = string.IsNullOrEmpty(report.CurrentDns.Secondary) ? "N/A" : report.CurrentDns.Secondary;
            DnsLatencyText.Text = report.CurrentDns.ResponseTimeMs > 0 ? $"{report.CurrentDns.ResponseTimeMs:F0} ms" : "N/A";
            DnsCard.Visibility = Visibility.Visible;

            // Ping results
            foreach (var ping in report.PingResults)
            {
                var grid = new Grid { ColumnSpacing = 12 };
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });

                var label = new TextBlock { Text = ping.Label, FontSize = 13, VerticalAlignment = VerticalAlignment.Center };
                Grid.SetColumn(label, 0);
                grid.Children.Add(label);

                var host = new TextBlock { Text = ping.Host, FontSize = 12, Opacity = 0.5, VerticalAlignment = VerticalAlignment.Center };
                Grid.SetColumn(host, 1);
                grid.Children.Add(host);

                var latColor = !ping.Success ? Windows.UI.Color.FromArgb(255, 198, 40, 40) :
                    ping.LatencyMs < 30 ? Windows.UI.Color.FromArgb(255, 46, 125, 50) :
                    ping.LatencyMs < 80 ? Windows.UI.Color.FromArgb(255, 230, 81, 0) :
                    Windows.UI.Color.FromArgb(255, 198, 40, 40);

                var latText = new TextBlock
                {
                    Text = ping.Success ? $"{ping.LatencyMs:F0} ms" : "Failed",
                    FontSize = 13,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(latColor),
                    TextAlignment = TextAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(latText, 2);
                grid.Children.Add(latText);

                PingList.Children.Add(grid);
            }
            PingCard.Visibility = Visibility.Visible;

            // DNS Presets
            DnsPresetsHeader.Visibility = Visibility.Visible;
            foreach (var preset in report.AvailableDnsPresets)
            {
                var card = new Border
                {
                    Background = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(16, 12, 16, 12),
                    BorderBrush = preset.IsCurrentlyActive
                        ? new SolidColorBrush(Windows.UI.Color.FromArgb(255, 46, 125, 50))
                        : (Brush)Microsoft.UI.Xaml.Application.Current.Resources["CardStrokeColorDefaultBrush"],
                    BorderThickness = new Thickness(preset.IsCurrentlyActive ? 2 : 0.5)
                };

                var grid = new Grid { ColumnSpacing = 12 };
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var infoStack = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
                var nameRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
                nameRow.Children.Add(new TextBlock
                {
                    Text = preset.Name,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    FontSize = 14
                });

                // Category badge
                var catBadge = new Border
                {
                    Background = new SolidColorBrush(Windows.UI.Color.FromArgb(25, 33, 150, 243)),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(6, 2, 6, 2)
                };
                catBadge.Child = new TextBlock { Text = preset.Category, FontSize = 10, Opacity = 0.8 };
                nameRow.Children.Add(catBadge);

                if (preset.IsCurrentlyActive)
                {
                    var activeBadge = new Border
                    {
                        Background = new SolidColorBrush(Windows.UI.Color.FromArgb(30, 46, 125, 50)),
                        CornerRadius = new CornerRadius(4),
                        Padding = new Thickness(6, 2, 6, 2)
                    };
                    activeBadge.Child = new TextBlock
                    {
                        Text = "ACTIVE",
                        FontSize = 10,
                        Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 46, 125, 50)),
                        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
                    };
                    nameRow.Children.Add(activeBadge);
                }

                infoStack.Children.Add(nameRow);
                infoStack.Children.Add(new TextBlock { Text = preset.Description, FontSize = 12, Opacity = 0.6 });
                infoStack.Children.Add(new TextBlock { Text = $"{preset.Primary}  /  {preset.Secondary}", FontSize = 11, Opacity = 0.4 });

                Grid.SetColumn(infoStack, 0);
                grid.Children.Add(infoStack);

                if (!preset.IsCurrentlyActive)
                {
                    var btn = new Button
                    {
                        Content = "Apply",
                        Padding = new Thickness(16, 6, 16, 6),
                        Tag = $"{preset.Primary}|{preset.Secondary}|{preset.Name}",
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    btn.Click += ApplyDns_Click;
                    Grid.SetColumn(btn, 2);
                    grid.Children.Add(btn);
                }

                card.Child = grid;
                DnsPresetList.Children.Add(card);
            }

            // Quick actions
            ActionsHeader.Visibility = Visibility.Visible;
            ActionsPanel.Visibility = Visibility.Visible;

            // Custom DNS card
            CustomDnsCard.Visibility = Visibility.Visible;

            // Adapters
            foreach (var adapter in report.Adapters)
            {
                var stack = new StackPanel { Spacing = 2 };
                var statusColor = adapter.Status == "Up"
                    ? Windows.UI.Color.FromArgb(255, 46, 125, 50)
                    : Windows.UI.Color.FromArgb(255, 158, 158, 158);

                var headerRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
                headerRow.Children.Add(new TextBlock
                {
                    Text = adapter.Name,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    FontSize = 13
                });
                var statusBadge = new Border
                {
                    Background = new SolidColorBrush(Windows.UI.Color.FromArgb(30, statusColor.R, statusColor.G, statusColor.B)),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(6, 2, 6, 2)
                };
                statusBadge.Child = new TextBlock
                {
                    Text = adapter.Status,
                    FontSize = 10,
                    Foreground = new SolidColorBrush(statusColor)
                };
                headerRow.Children.Add(statusBadge);
                stack.Children.Add(headerRow);

                stack.Children.Add(new TextBlock { Text = adapter.Description, FontSize = 12, Opacity = 0.6 });
                stack.Children.Add(new TextBlock
                {
                    Text = $"IP: {adapter.IpAddress}  |  MAC: {adapter.MacAddress}  |  Speed: {adapter.Speed}  |  Type: {adapter.AdapterType}",
                    FontSize = 11, Opacity = 0.4
                });
                AdapterList.Children.Add(stack);
            }
            AdapterCard.Visibility = Visibility.Visible;

            var issueText = report.IssuesFound > 0 ? $" — {report.IssuesFound} issue(s) detected" : " — no issues";
            StatusText.Text = $"Diagnosis complete{issueText}";
        }
        catch (Exception ex) { StatusText.Text = $"Error: {ex.Message}"; }
        finally
        {
            ScanBtn.IsEnabled = true;
            Progress.IsActive = false;
            Progress.Visibility = Visibility.Collapsed;
        }
    }

    private async void ApplyDns_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not string tag) return;
        var parts = tag.Split('|');
        if (parts.Length < 3) return;

        var dialog = new ContentDialog
        {
            Title = $"Switch DNS to {parts[2]}?",
            Content = $"This will change your DNS servers to:\n  Primary:   {parts[0]}\n  Secondary: {parts[1]}\n\nYour internet will briefly disconnect during the switch.",
            PrimaryButtonText = "Apply",
            CloseButtonText = "Cancel",
            XamlRoot = this.XamlRoot,
            DefaultButton = ContentDialogButton.Primary
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
        await RunAction("change-dns", parts[0], parts[1]);
        ResultText.Text = $"DNS changed to {parts[2]} ({parts[0]} / {parts[1]}). Run Diagnose again to verify.";
        ResultCard.Visibility = Visibility.Visible;
    }

    private async void FlushDns_Click(object sender, RoutedEventArgs e)
    {
        await RunAction("flush-dns");
        ResultText.Text = "DNS cache flushed successfully.";
        ResultCard.Visibility = Visibility.Visible;
    }

    private async void ResetAdapter_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            Title = "Reset network adapter?",
            Content = "This will briefly disconnect your internet (2-3 seconds) while the adapter restarts.",
            PrimaryButtonText = "Reset",
            CloseButtonText = "Cancel",
            XamlRoot = this.XamlRoot
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        await RunAction("reset-adapter");
        ResultText.Text = "Network adapter reset. Connection should be restored.";
        ResultCard.Visibility = Visibility.Visible;
    }

    private async void ResetWinsock_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            Title = "Reset Winsock catalog?",
            Content = "This resets the Windows network stack to defaults.\nA restart may be required for full effect.",
            PrimaryButtonText = "Reset",
            CloseButtonText = "Cancel",
            XamlRoot = this.XamlRoot
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        await RunAction("reset-winsock");
        ResultText.Text = "Winsock catalog reset. Restart your PC if network issues persist.";
        ResultCard.Visibility = Visibility.Visible;
    }

    private async Task RunAction(string action, params string[] args)
    {
        if (_module is null) return;
        Progress.IsActive = true;
        Progress.Visibility = Visibility.Visible;
        StatusText.Text = "Applying...";

        var ids = new List<string> { action };
        ids.AddRange(args);
        var plan = new OptimizationPlan("network-optimizer", ids);
        var progress = new Progress<TaskProgress>(p =>
        {
            DispatcherQueue.TryEnqueue(() => StatusText.Text = p.StatusText);
        });

        await _module.OptimizeAsync(plan, progress);
        Progress.IsActive = false;
        Progress.Visibility = Visibility.Collapsed;
        StatusText.Text = "Done";
    }

    private async void FixAllNetwork_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            Title = "Fix common network issues?",
            Content = "This will run the following repairs in sequence:\n\n" +
                "1. Flush DNS cache — clears stale DNS entries\n" +
                "2. Release and renew IP address\n" +
                "3. Reset network adapter\n" +
                "4. Reset Winsock catalog\n\n" +
                "Your internet connection may drop briefly during this process.",
            PrimaryButtonText = "Fix All",
            CloseButtonText = "Cancel",
            XamlRoot = this.XamlRoot,
            DefaultButton = ContentDialogButton.Primary
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        FixAllNetworkBtn.IsEnabled = false;
        Progress.IsActive = true; Progress.Visibility = Visibility.Visible;

        var steps = new (string action, string label)[]
        {
            ("flush-dns", "Flushing DNS cache..."),
            ("release-renew-ip", "Releasing and renewing IP address..."),
            ("reset-adapter", "Resetting network adapter..."),
            ("reset-winsock", "Resetting Winsock catalog..."),
        };

        int completed = 0;
        foreach (var (action, label) in steps)
        {
            StatusText.Text = $"[{completed + 1}/{steps.Length}] {label}";
            await RunAction(action);
            completed++;
            await Task.Delay(500); // Brief pause between steps
        }

        ResultText.Text = $"All {steps.Length} network repairs completed.\n" +
            "If issues persist, restart your PC to apply Winsock changes.";
        ResultCard.Visibility = Visibility.Visible;
        StatusText.Text = "Network repair complete!";

        FixAllNetworkBtn.IsEnabled = true;
        Progress.IsActive = false; Progress.Visibility = Visibility.Collapsed;
    }

    private async void BenchmarkBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_module is null) return;
        BenchmarkBtn.IsEnabled = false;
        Progress.IsActive = true; Progress.Visibility = Visibility.Visible;
        StatusText.Text = "Benchmarking DNS providers (pinging 5 servers x 3 attempts each)...";
        BenchmarkList.Children.Clear();
        BenchmarkCard.Visibility = Visibility.Collapsed;

        try
        {
            var result = await _module.BenchmarkDnsAsync();

            if (result.Rankings.Count == 0)
            {
                StatusText.Text = "Benchmark failed — check your internet connection.";
                return;
            }

            BenchmarkCard.Visibility = Visibility.Visible;

            // Recommendation text
            if (result.Recommended is not null && result.ImprovementMs.HasValue && result.ImprovementMs > 5)
            {
                BenchmarkRecommendation.Text = $"Recommended: Switch to {result.Recommended.Name} — " +
                    $"{result.ImprovementMs:F0}ms faster than your current DNS ({result.Current?.Name ?? "ISP Default"})";
                BenchmarkRecommendation.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 46, 125, 50));
            }
            else
            {
                BenchmarkRecommendation.Text = "Your current DNS is already among the fastest. No change needed!";
                BenchmarkRecommendation.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 33, 150, 243));
            }

            // Rankings
            int rank = 1;
            foreach (var preset in result.Rankings)
            {
                var isBest = rank == 1;
                var isCurrent = preset.IsCurrentlyActive;

                var row = new Grid
                {
                    ColumnSpacing = 12, Padding = new Thickness(12, 8, 12, 8),
                    Background = isBest
                        ? new SolidColorBrush(Windows.UI.Color.FromArgb(15, 46, 125, 50))
                        : null
                };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                // Rank number
                var rankText = new TextBlock
                {
                    Text = $"#{rank}", FontSize = 14, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    VerticalAlignment = VerticalAlignment.Center, MinWidth = 30,
                    Foreground = isBest ? new SolidColorBrush(Windows.UI.Color.FromArgb(255, 46, 125, 50)) : null
                };
                Grid.SetColumn(rankText, 0);
                row.Children.Add(rankText);

                // Name + description
                var info = new StackPanel { Spacing = 1, VerticalAlignment = VerticalAlignment.Center };
                var nameRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
                nameRow.Children.Add(new TextBlock { Text = preset.Name, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, FontSize = 13 });
                if (isCurrent)
                {
                    var badge = new Border
                    {
                        Background = new SolidColorBrush(Windows.UI.Color.FromArgb(30, 33, 150, 243)),
                        CornerRadius = new CornerRadius(4), Padding = new Thickness(6, 2, 6, 2)
                    };
                    badge.Child = new TextBlock { Text = "CURRENT", FontSize = 9, Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 33, 150, 243)) };
                    nameRow.Children.Add(badge);
                }
                if (isBest)
                {
                    var bestBadge = new Border
                    {
                        Background = new SolidColorBrush(Windows.UI.Color.FromArgb(30, 46, 125, 50)),
                        CornerRadius = new CornerRadius(4), Padding = new Thickness(6, 2, 6, 2)
                    };
                    bestBadge.Child = new TextBlock { Text = "FASTEST", FontSize = 9, Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 46, 125, 50)) };
                    nameRow.Children.Add(bestBadge);
                }
                info.Children.Add(nameRow);
                info.Children.Add(new TextBlock { Text = $"{preset.Primary} / {preset.Secondary} — {preset.Category}", FontSize = 11, Opacity = 0.5 });
                Grid.SetColumn(info, 1);
                row.Children.Add(info);

                // Latency bar
                var maxLatency = result.Rankings.Max(r => r.LatencyMs > 0 && r.LatencyMs < 9999 ? r.LatencyMs : 100);
                var barPct = preset.LatencyMs < 9999 ? (preset.LatencyMs / maxLatency * 100) : 100;
                var bar = new ProgressBar { Minimum = 0, Maximum = 100, Value = barPct, Width = 80, Height = 4, CornerRadius = new CornerRadius(2), VerticalAlignment = VerticalAlignment.Center };
                Grid.SetColumn(bar, 2);
                row.Children.Add(bar);

                // Latency text
                var latText = new TextBlock
                {
                    Text = preset.LatencyMs < 9999 ? $"{preset.LatencyMs:F0}ms" : "Timeout",
                    FontSize = 13, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    MinWidth = 60, TextAlignment = TextAlignment.Right, VerticalAlignment = VerticalAlignment.Center,
                    Foreground = isBest ? new SolidColorBrush(Windows.UI.Color.FromArgb(255, 46, 125, 50)) : null
                };
                Grid.SetColumn(latText, 3);
                row.Children.Add(latText);

                // Quick switch button
                if (!isCurrent && preset.LatencyMs < 9999)
                {
                    var switchBtn = new Button
                    {
                        Content = "Switch", Padding = new Thickness(10, 4, 10, 4), FontSize = 12,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    if (isBest) switchBtn.Style = (Style)Microsoft.UI.Xaml.Application.Current.Resources["AccentButtonStyle"];
                    var capturedPreset = preset;
                    switchBtn.Click += async (s, ev) =>
                    {
                        await RunAction("change-dns", capturedPreset.Primary, capturedPreset.Secondary);
                        ResultText.Text = $"Switched to {capturedPreset.Name} ({capturedPreset.Primary})";
                        ResultCard.Visibility = Visibility.Visible;
                    };
                    Grid.SetColumn(switchBtn, 4);
                    row.Children.Add(switchBtn);
                }

                BenchmarkList.Children.Add(row);
                rank++;
            }

            StatusText.Text = $"Benchmark complete — {result.Rankings.Count} providers tested";
        }
        catch (Exception ex) { StatusText.Text = $"Error: {ex.Message}"; }
        finally
        {
            BenchmarkBtn.IsEnabled = true;
            Progress.IsActive = false; Progress.Visibility = Visibility.Collapsed;
        }
    }

    // ── SPEED TEST ────────────────────────────────────────────

    private async void SpeedTestBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_module is null) return;

        SpeedTestBtn.IsEnabled = false;
        ScanBtn.IsEnabled = false;
        BenchmarkBtn.IsEnabled = false;
        Progress.IsActive = true; Progress.Visibility = Visibility.Visible;
        StatusText.Text = "Starting speed test...";

        SpeedTestCard.Visibility = Visibility.Visible;
        SpeedProgressBar.Visibility = Visibility.Visible;
        SpeedProgressBar.Value = 0;
        SpeedDownloadText.Text = "—";
        SpeedLatencyText.Text = "—";
        SpeedBytesText.Text = "—";
        SpeedServerText.Text = "—";

        try
        {
            var progress = new Progress<TaskProgress>(p =>
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    StatusText.Text = p.StatusText;
                    SpeedProgressBar.Value = p.Percentage;
                });
            });

            var result = await _module.RunSpeedTestAsync(progress);

            if (result.Success)
            {
                SpeedDownloadText.Text = $"{result.DownloadMbps:F1} Mbps";
                SpeedLatencyText.Text = result.LatencyMs > 0 ? $"{result.LatencyMs:F0} ms" : "N/A";
                SpeedBytesText.Text = result.BytesDownloaded switch
                {
                    < 1024 * 1024 => $"{result.BytesDownloaded / 1024.0:F0} KB",
                    _ => $"{result.BytesDownloaded / (1024.0 * 1024):F1} MB"
                };
                SpeedServerText.Text = result.ServerUsed;
                StatusText.Text = $"Speed test complete — {result.DownloadMbps:F1} Mbps download";
            }
            else
            {
                SpeedDownloadText.Text = "Failed";
                SpeedLatencyText.Text = result.LatencyMs > 0 ? $"{result.LatencyMs:F0} ms" : "N/A";
                StatusText.Text = $"Speed test failed: {result.Error}";
            }
        }
        catch (Exception ex) { StatusText.Text = $"Error: {ex.Message}"; }
        finally
        {
            SpeedTestBtn.IsEnabled = true;
            ScanBtn.IsEnabled = true;
            BenchmarkBtn.IsEnabled = true;
            Progress.IsActive = false; Progress.Visibility = Visibility.Collapsed;
            SpeedProgressBar.Visibility = Visibility.Collapsed;
        }
    }

    // ── CUSTOM DNS ────────────────────────────────────────────

    private async void ApplyCustomDns_Click(object sender, RoutedEventArgs e)
    {
        var primary = CustomPrimaryDns.Text.Trim();
        var secondary = CustomSecondaryDns.Text.Trim();

        if (string.IsNullOrEmpty(primary))
        {
            ResultText.Text = "Please enter at least a primary DNS address.";
            ResultCard.Visibility = Visibility.Visible;
            return;
        }

        // Basic IPv4 validation
        if (!System.Net.IPAddress.TryParse(primary, out var parsedPrimary)
            || parsedPrimary.AddressFamily != AddressFamily.InterNetwork)
        {
            ResultText.Text = "Invalid primary DNS address. Please enter a valid IPv4 address (e.g. 1.1.1.1).";
            ResultCard.Visibility = Visibility.Visible;
            return;
        }

        if (!string.IsNullOrEmpty(secondary)
            && (!System.Net.IPAddress.TryParse(secondary, out var parsedSecondary)
                || parsedSecondary.AddressFamily != AddressFamily.InterNetwork))
        {
            ResultText.Text = "Invalid secondary DNS address. Please enter a valid IPv4 address.";
            ResultCard.Visibility = Visibility.Visible;
            return;
        }

        if (string.IsNullOrEmpty(secondary)) secondary = primary; // fallback

        var dialog = new ContentDialog
        {
            Title = "Apply custom DNS?",
            Content = $"This will change your DNS servers to:\n  Primary:   {primary}\n  Secondary: {secondary}\n\nYour internet will briefly disconnect during the switch.",
            PrimaryButtonText = "Apply",
            CloseButtonText = "Cancel",
            XamlRoot = this.XamlRoot,
            DefaultButton = ContentDialogButton.Primary
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        await RunAction("custom-dns", primary, secondary);
        ResultText.Text = $"Custom DNS applied: {primary} / {secondary}. Run Diagnose again to verify.";
        ResultCard.Visibility = Visibility.Visible;
    }

    private void ApplyLocalization()
    {
        if (FindName("PageTitle") is Microsoft.UI.Xaml.Controls.TextBlock title)
            title.Text = S._("net.title");
        if (FindName("PageSubtitle") is Microsoft.UI.Xaml.Controls.TextBlock subtitle)
            subtitle.Text = S._("net.subtitle");
    }
}
