using AuraCore.Desktop.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using AuraCore.Application;
using AuraCore.Application.Interfaces.Modules;
using AuraCore.Module.BatteryOptimizer;
using AuraCore.Module.BatteryOptimizer.Models;

namespace AuraCore.Desktop.Pages;

public sealed partial class BatteryOptimizerPage : Page
{
    private BatteryOptimizerModule? _module;

    private static readonly Windows.UI.Color Green = Windows.UI.Color.FromArgb(255, 46, 125, 50);
    private static readonly Windows.UI.Color Red = Windows.UI.Color.FromArgb(255, 198, 40, 40);
    private static readonly Windows.UI.Color Amber = Windows.UI.Color.FromArgb(255, 230, 81, 0);
    private static readonly Windows.UI.Color Blue = Windows.UI.Color.FromArgb(255, 33, 150, 243);

    public BatteryOptimizerPage()
    {
        InitializeComponent();
        var modules = App.Current.Services.GetServices<IOptimizationModule>();
        _module = modules.FirstOrDefault(m => m.Id == "battery-optimizer") as BatteryOptimizerModule;

        _ = RefreshAsync();

        ApplyLocalization();
        S.LanguageChanged += () => DispatcherQueue?.TryEnqueue(ApplyLocalization);
    }

    private async void ScanBtn_Click(object sender, RoutedEventArgs e) => await RefreshAsync();

    private async Task RefreshAsync()
    {
        if (_module is null) return;
        ScanBtn.IsEnabled = false;
        Progress.IsActive = true; Progress.Visibility = Visibility.Visible;
        StatusText.Text = S._("bat.scanning");

        try
        {
            await _module.ScanAsync(new ScanOptions());
            var status = _module.LastStatus;

            if (status is null || !status.HasBattery)
            {
                NoBatteryCard.Visibility = Visibility.Visible;
                HealthBanner.Visibility = Visibility.Collapsed;
                PowerPlanCard.Visibility = Visibility.Collapsed;
                DrainCard.Visibility = Visibility.Collapsed;
                StatusText.Text = S._("bat.noBattery");
                return;
            }

            NoBatteryCard.Visibility = Visibility.Collapsed;
            BuildHealthBanner(status);
            BuildPowerPlans(_module.LastPowerPlans);
            BuildDrainApps(_module.LastDrainApps);
            StatusText.Text = $"{status.HealthGrade} - {status.ChargePercent}%";
        }
        catch (Exception ex) { StatusText.Text = $"Error: {ex.Message}"; }
        finally
        {
            ScanBtn.IsEnabled = true;
            Progress.IsActive = false; Progress.Visibility = Visibility.Collapsed;
        }
    }

    private void BuildHealthBanner(BatteryStatus status)
    {
        var healthColor = status.HealthColor switch
        {
            "Green" => Green, "Blue" => Blue, "Amber" => Amber, "Red" => Red, _ => Blue
        };

        HealthBanner.Background = new SolidColorBrush(
            Windows.UI.Color.FromArgb(20, healthColor.R, healthColor.G, healthColor.B));
        HealthBanner.BorderBrush = new SolidColorBrush(
            Windows.UI.Color.FromArgb(60, healthColor.R, healthColor.G, healthColor.B));

        ChargePercentText.Text = $"{status.ChargePercent}%";
        ChargePercentText.Foreground = new SolidColorBrush(healthColor);
        ChargeStatusText.Text = status.ChargeStatus;
        BatteryIcon.Foreground = new SolidColorBrush(healthColor);

        HealthText.Text = $"{status.HealthPercent}% ({status.HealthGrade})";
        HealthText.Foreground = new SolidColorBrush(healthColor);
        WearText.Text = $"{status.WearPercent}%";
        WearText.Foreground = new SolidColorBrush(status.WearPercent > 30 ? Amber : Green);

        DesignCapText.Text = status.DesignCapacityDisplay;
        FullCapText.Text = status.FullChargeCapacityDisplay;
        EstTimeText.Text = status.EstRemainingDisplay;
        CycleCountText.Text = status.CycleCount > 0 ? status.CycleCount.ToString() : "N/A";

        BatteryNameText.Text = status.BatteryName;
        ChemistryText.Text = status.Chemistry;
        MfrText.Text = status.Manufacturer;

        HealthBanner.Visibility = Visibility.Visible;
    }

    private void BuildPowerPlans(List<PowerPlanInfo> plans)
    {
        PowerPlanList.Children.Clear();
        if (plans.Count == 0) { PowerPlanCard.Visibility = Visibility.Collapsed; return; }

        foreach (var plan in plans)
        {
            var row = new Grid { ColumnSpacing = 12, Padding = new Thickness(0, 4, 0, 4) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var nameStack = new StackPanel { Spacing = 1, VerticalAlignment = VerticalAlignment.Center };
            nameStack.Children.Add(new TextBlock
            {
                Text = plan.Name,
                FontWeight = plan.IsActive ? Microsoft.UI.Text.FontWeights.Bold : Microsoft.UI.Text.FontWeights.Normal,
                FontSize = 13
            });
            row.Children.Add(nameStack);

            if (plan.IsActive)
            {
                var activeBadge = new Border
                {
                    Background = new SolidColorBrush(Windows.UI.Color.FromArgb(30, Green.R, Green.G, Green.B)),
                    CornerRadius = new CornerRadius(4), Padding = new Thickness(8, 3, 8, 3),
                    VerticalAlignment = VerticalAlignment.Center
                };
                activeBadge.Child = new TextBlock
                {
                    Text = "ACTIVE", FontSize = 9, FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                    Foreground = new SolidColorBrush(Green)
                };
                Grid.SetColumn(activeBadge, 1);
                row.Children.Add(activeBadge);
            }
            else
            {
                var switchBtn = new Button
                {
                    Content = S._("bat.switchPlan"), FontSize = 11,
                    Padding = new Thickness(10, 4, 10, 4), VerticalAlignment = VerticalAlignment.Center
                };
                var capturedId = plan.PlanId;
                switchBtn.Click += async (s, ev) =>
                {
                    if (_module is null) return;
                    switchBtn.IsEnabled = false;
                    var ok = await _module.SetPowerPlanAsync(capturedId);
                    if (ok) { StatusText.Text = $"Switched to {plan.Name}"; await RefreshAsync(); }
                    else { StatusText.Text = "Failed to switch plan"; switchBtn.IsEnabled = true; }
                };
                Grid.SetColumn(switchBtn, 2);
                row.Children.Add(switchBtn);
            }

            PowerPlanList.Children.Add(row);

            PowerPlanList.Children.Add(new Border
            {
                Height = 1,
                Background = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["CardStrokeColorDefaultBrush"],
                Opacity = 0.2
            });
        }

        // Battery Saver button
        var saverBtn = new Button
        {
            Content = S._("bat.enableSaver"),
            Padding = new Thickness(16, 6, 16, 6),
            Margin = new Thickness(0, 4, 0, 0)
        };
        saverBtn.Click += async (s, ev) =>
        {
            if (_module is null) return;
            saverBtn.IsEnabled = false;
            var ok = await _module.EnableBatterySaverAsync();
            StatusText.Text = ok ? S._("bat.saverEnabled") : "Failed";
            if (ok) await RefreshAsync();
            else saverBtn.IsEnabled = true;
        };
        PowerPlanList.Children.Add(saverBtn);

        PowerPlanCard.Visibility = Visibility.Visible;
    }

    private void BuildDrainApps(List<PowerDrainApp> apps)
    {
        DrainList.Children.Clear();
        if (apps.Count == 0) { DrainCard.Visibility = Visibility.Collapsed; return; }

        foreach (var app in apps.Take(10))
        {
            var impactColor = app.ImpactColor switch
            {
                "Red" => Red, "Amber" => Amber, _ => Green
            };

            var row = new Grid { ColumnSpacing = 12, Padding = new Thickness(0, 3, 0, 3) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            row.Children.Add(new TextBlock
            {
                Text = app.Name, FontSize = 13,
                FontWeight = app.Impact == "High" ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal,
                VerticalAlignment = VerticalAlignment.Center
            });

            var cpuText = new TextBlock
            {
                Text = $"CPU: {app.CpuPercent:F0}s", FontSize = 11,
                Opacity = 0.5, VerticalAlignment = VerticalAlignment.Center, MinWidth = 70
            };
            Grid.SetColumn(cpuText, 1);
            row.Children.Add(cpuText);

            var memText = new TextBlock
            {
                Text = $"RAM: {app.WorkingSetMB} MB", FontSize = 11,
                Opacity = 0.5, VerticalAlignment = VerticalAlignment.Center, MinWidth = 80
            };
            Grid.SetColumn(memText, 2);
            row.Children.Add(memText);

            var badge = new Border
            {
                Background = new SolidColorBrush(Windows.UI.Color.FromArgb(25, impactColor.R, impactColor.G, impactColor.B)),
                CornerRadius = new CornerRadius(4), Padding = new Thickness(8, 2, 8, 2),
                VerticalAlignment = VerticalAlignment.Center
            };
            badge.Child = new TextBlock
            {
                Text = app.Impact.ToUpper(), FontSize = 9,
                FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                Foreground = new SolidColorBrush(impactColor)
            };
            Grid.SetColumn(badge, 3);
            row.Children.Add(badge);

            DrainList.Children.Add(row);
        }

        DrainCard.Visibility = Visibility.Visible;
    }

    private async void ReportBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_module is null) return;
        ReportBtn.IsEnabled = false;
        StatusText.Text = S._("bat.generatingReport");
        var result = await _module.GenerateBatteryReportAsync();
        StatusText.Text = result.Success ? S._("bat.reportDone") : (result.Error ?? "Failed");
        ReportBtn.IsEnabled = true;
    }

    private void SettingsBtn_Click(object sender, RoutedEventArgs e)
    {
        _module?.OpenPowerSettings();
    }

    private void ApplyLocalization()
    {
        try
        {
            PageTitle.Text = S._("bat.title");
            PageSubtitle.Text = S._("bat.subtitle");
            ScanBtn.Content = S._("bat.scanBtn");
            ReportBtn.Content = S._("bat.reportBtn");
            SettingsBtn.Content = S._("bat.settingsBtn");
            StatusText.Text = S._("bat.scanStatus");
        }
        catch { }
    }
}
