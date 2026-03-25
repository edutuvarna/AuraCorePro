using AuraCore.Desktop.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using AuraCore.Application;
using AuraCore.Application.Interfaces.Modules;
using AuraCore.Module.DefenderManager;
using AuraCore.Module.DefenderManager.Models;
using System.Security.Principal;

namespace AuraCore.Desktop.Pages;

public sealed partial class DefenderPage : Page
{
    private DefenderManagerModule? _module;
    private static bool IsAdmin => new WindowsPrincipal(WindowsIdentity.GetCurrent())
        .IsInRole(WindowsBuiltInRole.Administrator);

    private static readonly Windows.UI.Color Green = Windows.UI.Color.FromArgb(255, 46, 125, 50);
    private static readonly Windows.UI.Color Red = Windows.UI.Color.FromArgb(255, 198, 40, 40);
    private static readonly Windows.UI.Color Amber = Windows.UI.Color.FromArgb(255, 230, 81, 0);
    private static readonly Windows.UI.Color Blue = Windows.UI.Color.FromArgb(255, 33, 150, 243);

    public DefenderPage()
    {
        InitializeComponent();
        var modules = App.Current.Services.GetServices<IOptimizationModule>();
        _module = modules.FirstOrDefault(m => m.Id == "defender-manager") as DefenderManagerModule;

        if (!IsAdmin) AdminWarning.IsOpen = true;

        // Auto-refresh on load
        _ = RefreshStatusAsync();

        ApplyDefLocalization();
        Services.S.LanguageChanged += () => DispatcherQueue?.TryEnqueue(ApplyDefLocalization);
    }

    private async void RefreshBtn_Click(object sender, RoutedEventArgs e) => await RefreshStatusAsync();

    private async Task RefreshStatusAsync()
    {
        if (_module is null) return;
        RefreshBtn.IsEnabled = false;
        Progress.IsActive = true; Progress.Visibility = Visibility.Visible;
        StatusText.Text = "Reading Defender status...";

        try
        {
            await _module.ScanAsync(new ScanOptions());
            var status = _module.LastStatus;
            if (status is null || status.Error is not null)
            {
                StatusText.Text = status?.Error ?? "Could not read Defender status";
                return;
            }

            // Protection banner
            var level = status.ProtectionLevel;
            var bannerColor = level switch
            {
                "Excellent" => Green,
                "Good" => Blue,
                "Partial" => Amber,
                _ => Red
            };
            ProtectionBanner.Background = new SolidColorBrush(
                Windows.UI.Color.FromArgb(20, bannerColor.R, bannerColor.G, bannerColor.B));
            ProtectionBanner.BorderBrush = new SolidColorBrush(
                Windows.UI.Color.FromArgb(60, bannerColor.R, bannerColor.G, bannerColor.B));
            ShieldIcon.Foreground = new SolidColorBrush(bannerColor);
            ProtectionLevelText.Text = $"Protection: {level}";
            ProtectionLevelText.Foreground = new SolidColorBrush(bannerColor);
            ProtectionDetailText.Text = $"{status.EnabledCount}/6 features enabled";
            SignatureText.Text = $"Signatures: {status.AntivirusSignatureVersion}";
            EngineText.Text = $"Engine: {status.EngineVersion}";
            if (status.SignaturesOutdated)
                SignatureText.Foreground = new SolidColorBrush(Red);
            ProtectionBanner.Visibility = Visibility.Visible;

            // Toggles
            BuildToggles(status);

            // Firewall
            BuildFirewall(status);

            // Threats
            BuildThreats(_module.LastThreats);

            // Exclusions
            BuildExclusions(_module.LastExclusions);

            StatusText.Text = $"Defender status loaded - {level} protection";
        }
        catch (Exception ex) { StatusText.Text = $"Error: {ex.Message}"; }
        finally
        {
            RefreshBtn.IsEnabled = true;
            Progress.IsActive = false; Progress.Visibility = Visibility.Collapsed;
        }
    }

    private void BuildToggles(DefenderStatus status)
    {
        ToggleList.Children.Clear();
        var features = new[]
        {
            ("Real-Time Protection", "RealTime", status.RealTimeProtection, "Continuously scans files and programs"),
            ("Cloud-Delivered Protection", "CloudProtection", status.CloudProtection, "Fast threat identification via cloud"),
            ("Behavior Monitoring", "BehaviorMonitoring", status.BehaviorMonitoring, "Monitors process behavior for threats"),
            ("PUA Protection", "PUA", status.PotentiallyUnwantedApps, "Blocks potentially unwanted applications"),
        };

        foreach (var (name, key, enabled, desc) in features)
        {
            var row = new Grid { ColumnSpacing = 12 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var info = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
            info.Children.Add(new TextBlock { Text = name, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, FontSize = 13 });
            info.Children.Add(new TextBlock { Text = desc, FontSize = 11, Opacity = 0.5 });
            grid_SetColumn(info, 0);
            row.Children.Add(info);

            var toggle = new ToggleSwitch
            {
                IsOn = enabled,
                IsEnabled = IsAdmin,
                OnContent = "ON",
                OffContent = "OFF",
                VerticalAlignment = VerticalAlignment.Center
            };
            var capturedKey = key;
            toggle.Toggled += async (s, ev) =>
            {
                if (_module is null) return;
                var success = await _module.SetProtectionAsync(capturedKey, toggle.IsOn);
                if (!success)
                {
                    // Revert
                    toggle.IsOn = !toggle.IsOn;
                    StatusText.Text = "Failed - admin required";
                }
                else
                {
                    StatusText.Text = $"{name}: {(toggle.IsOn ? "Enabled" : "Disabled")}";
                }
            };
            grid_SetColumn(toggle, 1);
            row.Children.Add(toggle);

            ToggleList.Children.Add(row);

            // Separator
            ToggleList.Children.Add(new Border
            {
                Height = 1,
                Background = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["CardStrokeColorDefaultBrush"],
                Opacity = 0.3
            });
        }

        // Network + Tamper (read-only, can't be changed via Set-MpPreference)
        AddReadOnlyRow("Network Protection", status.NetworkProtection, "Blocks malicious network connections");
        AddReadOnlyRow("Tamper Protection", status.TamperProtection, "Prevents unauthorized changes to security settings");

        ProtectionCard.Visibility = Visibility.Visible;
    }

    private void AddReadOnlyRow(string name, bool enabled, string desc)
    {
        var row = new Grid { ColumnSpacing = 12 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var info = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        info.Children.Add(new TextBlock { Text = name, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, FontSize = 13 });
        info.Children.Add(new TextBlock { Text = desc, FontSize = 11, Opacity = 0.5 });
        grid_SetColumn(info, 0);
        row.Children.Add(info);

        var badge = new Border
        {
            Background = new SolidColorBrush(
                Windows.UI.Color.FromArgb(30, enabled ? Green.R : Red.R, enabled ? Green.G : Red.G, enabled ? Green.B : Red.B)),
            CornerRadius = new CornerRadius(4), Padding = new Thickness(10, 4, 10, 4),
            VerticalAlignment = VerticalAlignment.Center
        };
        badge.Child = new TextBlock
        {
            Text = enabled ? "ON" : "OFF", FontSize = 11,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = new SolidColorBrush(enabled ? Green : Red)
        };
        grid_SetColumn(badge, 1);
        row.Children.Add(badge);

        ToggleList.Children.Add(row);
    }

    private void BuildFirewall(DefenderStatus status)
    {
        FirewallList.Children.Clear();
        var profiles = new[]
        {
            ("Domain", status.FirewallDomain),
            ("Private", status.FirewallPrivate),
            ("Public", status.FirewallPublic),
        };

        foreach (var (name, enabled) in profiles)
        {
            var row = new Grid { ColumnSpacing = 12 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            row.Children.Add(new TextBlock
            {
                Text = $"{name} Profile",
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                FontSize = 13,
                VerticalAlignment = VerticalAlignment.Center
            });

            var badge = new Border
            {
                Background = new SolidColorBrush(
                    Windows.UI.Color.FromArgb(30, enabled ? Green.R : Red.R, enabled ? Green.G : Red.G, enabled ? Green.B : Red.B)),
                CornerRadius = new CornerRadius(4), Padding = new Thickness(10, 4, 10, 4),
                VerticalAlignment = VerticalAlignment.Center
            };
            badge.Child = new TextBlock
            {
                Text = enabled ? "ENABLED" : "DISABLED",
                FontSize = 11, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = new SolidColorBrush(enabled ? Green : Red)
            };
            Grid.SetColumn(badge, 1);
            row.Children.Add(badge);

            FirewallList.Children.Add(row);
        }
        FirewallCard.Visibility = Visibility.Visible;
    }

    private void BuildThreats(List<ThreatInfo> threats)
    {
        ThreatList.Children.Clear();
        ThreatTitle.Text = threats.Count > 0 ? $"Threat History ({threats.Count})" : "Threat History";

        if (threats.Count == 0)
        {
            ThreatList.Children.Add(new TextBlock
            {
                Text = "No threats detected - your system is clean!",
                FontSize = 12, Opacity = 0.5, Foreground = new SolidColorBrush(Green)
            });
        }
        else
        {
            foreach (var t in threats.Take(10))
            {
                var sevColor = t.Severity switch
                {
                    "Severe" or "High" => Red,
                    "Medium" => Amber,
                    _ => Blue
                };

                var card = new Border
                {
                    Background = new SolidColorBrush(Windows.UI.Color.FromArgb(8, sevColor.R, sevColor.G, sevColor.B)),
                    CornerRadius = new CornerRadius(6), Padding = new Thickness(12, 8, 12, 8),
                    BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(30, sevColor.R, sevColor.G, sevColor.B)),
                    BorderThickness = new Thickness(0.5)
                };

                var grid = new Grid { ColumnSpacing = 12 };
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var info = new StackPanel { Spacing = 2 };
                info.Children.Add(new TextBlock
                {
                    Text = t.ThreatName, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, FontSize = 13
                });
                if (!string.IsNullOrEmpty(t.Path))
                    info.Children.Add(new TextBlock
                    {
                        Text = t.Path, FontSize = 10, Opacity = 0.4,
                        FontFamily = new FontFamily("Consolas"),
                        TextTrimming = TextTrimming.CharacterEllipsis
                    });
                info.Children.Add(new TextBlock
                {
                    Text = t.DetectedAt != DateTimeOffset.MinValue ? t.DetectedAt.ToLocalTime().ToString("g") : "",
                    FontSize = 10, Opacity = 0.35
                });
                grid.Children.Add(info);

                var badges = new StackPanel { Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
                var sevBadge = new Border
                {
                    Background = new SolidColorBrush(Windows.UI.Color.FromArgb(30, sevColor.R, sevColor.G, sevColor.B)),
                    CornerRadius = new CornerRadius(3), Padding = new Thickness(6, 2, 6, 2)
                };
                sevBadge.Child = new TextBlock
                {
                    Text = t.Severity.ToUpper(), FontSize = 9,
                    FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                    Foreground = new SolidColorBrush(sevColor)
                };
                badges.Children.Add(sevBadge);

                var statusColor = t.Status == "Active" ? Red : Green;
                var statusBadge = new Border
                {
                    Background = new SolidColorBrush(Windows.UI.Color.FromArgb(30, statusColor.R, statusColor.G, statusColor.B)),
                    CornerRadius = new CornerRadius(3), Padding = new Thickness(6, 2, 6, 2)
                };
                statusBadge.Child = new TextBlock
                {
                    Text = t.Status.ToUpper(), FontSize = 9,
                    FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                    Foreground = new SolidColorBrush(statusColor)
                };
                badges.Children.Add(statusBadge);
                Grid.SetColumn(badges, 1);
                grid.Children.Add(badges);

                card.Child = grid;
                ThreatList.Children.Add(card);
            }
        }
        ThreatCard.Visibility = Visibility.Visible;
    }

    private void BuildExclusions(List<ExclusionInfo> exclusions)
    {
        ExclusionList.Children.Clear();
        if (exclusions.Count == 0)
        {
            NoExclusionsText.Visibility = Visibility.Visible;
        }
        else
        {
            NoExclusionsText.Visibility = Visibility.Collapsed;
            foreach (var ex in exclusions)
            {
                var row = new Grid { ColumnSpacing = 8 };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var typeBadge = new Border
                {
                    Background = new SolidColorBrush(Windows.UI.Color.FromArgb(20, Blue.R, Blue.G, Blue.B)),
                    CornerRadius = new CornerRadius(3), Padding = new Thickness(6, 2, 6, 2),
                    VerticalAlignment = VerticalAlignment.Center
                };
                typeBadge.Child = new TextBlock
                {
                    Text = ex.Type.ToUpper(), FontSize = 9,
                    FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                    Foreground = new SolidColorBrush(Blue)
                };
                row.Children.Add(typeBadge);

                var valText = new TextBlock
                {
                    Text = ex.Value, FontSize = 12,
                    FontFamily = new FontFamily("Consolas"),
                    VerticalAlignment = VerticalAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis
                };
                Grid.SetColumn(valText, 1);
                row.Children.Add(valText);

                if (IsAdmin)
                {
                    var removeBtn = new Button
                    {
                        Content = "Remove", FontSize = 11,
                        Padding = new Thickness(8, 3, 8, 3)
                    };
                    var capturedType = ex.Type;
                    var capturedValue = ex.Value;
                    removeBtn.Click += async (s, ev) =>
                    {
                        if (_module is null) return;
                        var success = await _module.RemoveExclusionAsync(capturedType, capturedValue);
                        if (success) await RefreshStatusAsync();
                        else StatusText.Text = "Failed to remove exclusion";
                    };
                    Grid.SetColumn(removeBtn, 2);
                    row.Children.Add(removeBtn);
                }

                ExclusionList.Children.Add(row);
            }
        }
        ExclusionCard.Visibility = Visibility.Visible;
    }

    private static void grid_SetColumn(FrameworkElement el, int col) => Grid.SetColumn(el, col);

    private async void QuickScanBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_module is null) return;
        QuickScanBtn.IsEnabled = false;
        StatusText.Text = "Starting Quick Scan (this runs in background)...";
        var ok = await _module.StartQuickScanAsync();
        StatusText.Text = ok ? "Quick Scan started - check Windows Security for progress" : "Failed to start scan - admin required";
        QuickScanBtn.IsEnabled = true;
    }

    private async void FullScanBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_module is null) return;
        var dialog = new ContentDialog
        {
            Title = "Start Full Scan?",
            Content = "A full system scan can take 30+ minutes. It will run in the background.",
            PrimaryButtonText = "Start",
            CloseButtonText = "Cancel",
            XamlRoot = this.XamlRoot
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        FullScanBtn.IsEnabled = false;
        StatusText.Text = "Starting Full Scan...";
        var ok = await _module.StartFullScanAsync();
        StatusText.Text = ok ? "Full Scan started - check Windows Security for progress" : "Failed - admin required";
        FullScanBtn.IsEnabled = true;
    }

    private async void UpdateDefsBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_module is null) return;
        UpdateDefsBtn.IsEnabled = false;
        Progress.IsActive = true; Progress.Visibility = Visibility.Visible;
        StatusText.Text = "Updating virus definitions...";

        var ok = await _module.UpdateDefinitionsAsync();
        StatusText.Text = ok ? "Definitions updated successfully!" : "Update failed - check internet connection";

        Progress.IsActive = false; Progress.Visibility = Visibility.Collapsed;
        UpdateDefsBtn.IsEnabled = true;
        if (ok) await RefreshStatusAsync();
    }

    private void ApplyDefLocalization()
    {
        try
        {
            PageTitle.Text = S._("def.title");
            AdminWarning.Title = S._("def.adminWarningTitle");
            AdminWarning.Message = S._("def.adminWarningMsg");
            RefreshBtn.Content = S._("def.refreshBtn");
            QuickScanBtn.Content = S._("def.quickScan");
            FullScanBtn.Content = S._("def.fullScan");
            UpdateDefsBtn.Content = S._("def.updateDefs");
            StatusText.Text = S._("def.refreshStatus");
        }
        catch { }
    }
}
