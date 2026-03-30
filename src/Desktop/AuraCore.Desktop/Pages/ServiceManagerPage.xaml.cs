using AuraCore.Desktop.Services;
using System.Diagnostics;
using System.Management;
using System.Security.Principal;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace AuraCore.Desktop.Pages;

public sealed partial class ServiceManagerPage : Page
{
    private sealed record ServiceInfo
    {
        public string Name { get; init; } = "";
        public string DisplayName { get; init; } = "";
        public string State { get; init; } = "";
        public string StartMode { get; init; } = "";
        public int ProcessId { get; init; }
        public string Description { get; init; } = "";
        public string PathName { get; init; } = "";
    }

    private List<ServiceInfo> _allServices = new();
    private string _activeFilter = "all";

    private static readonly Windows.UI.Color Green  = Windows.UI.Color.FromArgb(255,  46, 125,  50);
    private static readonly Windows.UI.Color Red    = Windows.UI.Color.FromArgb(255, 198,  40,  40);
    private static readonly Windows.UI.Color Amber  = Windows.UI.Color.FromArgb(255, 230,  81,   0);
    private static readonly Windows.UI.Color Blue   = Windows.UI.Color.FromArgb(255,  33, 150, 243);
    private static readonly Windows.UI.Color Gray   = Windows.UI.Color.FromArgb(255, 117, 117, 117);

    private static bool IsAdmin => new WindowsPrincipal(WindowsIdentity.GetCurrent())
        .IsInRole(WindowsBuiltInRole.Administrator);

    public ServiceManagerPage()
    {
        InitializeComponent();
        if (!IsAdmin) AdminWarning.IsOpen = true;
        ApplyLocalization();
        S.LanguageChanged += () => DispatcherQueue?.TryEnqueue(ApplyLocalization);
    }

    // ── SCAN ─────────────────────────────────────────────────

    private async void ScanBtn_Click(object sender, RoutedEventArgs e)
    {
        ScanBtn.IsEnabled = false;
        Progress.IsActive = true; Progress.Visibility = Visibility.Visible;
        StatusText.Text = S._("svc.scanning");
        ServiceList.Children.Clear();
        SummaryCard.Visibility = Visibility.Collapsed;
        FilterBar.Visibility = Visibility.Collapsed;

        try
        {
            _allServices = await Task.Run(ScanServices);

            if (_allServices.Count == 0)
            {
                StatusText.Text = S._("svc.noServices");
                return;
            }

            // Stats
            var running  = _allServices.Count(s => s.State == "Running");
            var stopped  = _allServices.Count(s => s.State == "Stopped");
            var disabled = _allServices.Count(s => s.StartMode == "Disabled");

            TotalText.Text    = _allServices.Count.ToString();
            RunningText.Text  = running.ToString();
            StoppedText.Text  = stopped.ToString();
            DisabledText.Text = disabled.ToString();
            SummaryCard.Visibility = Visibility.Visible;

            FilterBar.Visibility = Visibility.Visible;
            RefreshBtn.Visibility = Visibility.Visible;

            _activeFilter = "all";
            SetFilterActive(FilterAll);
            BuildList();
            StatusText.Text = string.Format(S._("svc.found"), _allServices.Count, running);
        }
        catch (Exception ex) { StatusText.Text = $"Error: {ex.Message}"; }
        finally
        {
            ScanBtn.IsEnabled = true;
            Progress.IsActive = false; Progress.Visibility = Visibility.Collapsed;
        }
    }

    // ── WMI QUERY ────────────────────────────────────────────

    private static List<ServiceInfo> ScanServices()
    {
        var list = new List<ServiceInfo>();
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT Name, DisplayName, State, StartMode, ProcessId, Description, PathName FROM Win32_Service");
            foreach (ManagementObject obj in searcher.Get())
            {
                list.Add(new ServiceInfo
                {
                    Name        = obj["Name"]?.ToString() ?? "",
                    DisplayName = obj["DisplayName"]?.ToString() ?? "",
                    State       = obj["State"]?.ToString() ?? "Unknown",
                    StartMode   = obj["StartMode"]?.ToString() ?? "Unknown",
                    ProcessId   = obj["ProcessId"] is uint pid ? (int)pid : 0,
                    Description = obj["Description"]?.ToString() ?? "",
                    PathName    = obj["PathName"]?.ToString() ?? "",
                });
            }
        }
        catch { }
        return list.OrderBy(s => s.DisplayName).ToList();
    }

    // ── FILTER & SEARCH ───────────────────────────────────────

    private void Filter_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn) return;
        _activeFilter = btn.Tag?.ToString() ?? "all";
        SetFilterActive(btn);
        BuildList();
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => BuildList();

    private void SetFilterActive(Button active)
    {
        foreach (var btn in new[] { FilterAll, FilterRunning, FilterStopped, FilterDisabled, FilterAuto })
        {
            if (btn == active)
                btn.Style = (Style)Microsoft.UI.Xaml.Application.Current.Resources["AccentButtonStyle"];
            else
                btn.ClearValue(Button.StyleProperty);
        }
    }

    private IEnumerable<ServiceInfo> ApplyFilters()
    {
        var query = _allServices.AsEnumerable();
        query = _activeFilter switch
        {
            "running"  => query.Where(s => s.State == "Running"),
            "stopped"  => query.Where(s => s.State == "Stopped"),
            "disabled" => query.Where(s => s.StartMode == "Disabled"),
            "auto"     => query.Where(s => s.StartMode == "Auto"),
            _          => query
        };
        var search = SearchBox?.Text?.Trim() ?? "";
        if (!string.IsNullOrEmpty(search))
            query = query.Where(s =>
                s.DisplayName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                s.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                s.Description.Contains(search, StringComparison.OrdinalIgnoreCase));
        return query;
    }

    // ── BUILD LIST ────────────────────────────────────────────

    private void BuildList()
    {
        ServiceList.Children.Clear();
        var services = ApplyFilters().ToList();

        if (services.Count == 0)
        {
            ServiceList.Children.Add(new TextBlock
            {
                Text = "No services match this filter.", FontSize = 13, Opacity = 0.5,
                Margin = new Thickness(0, 8, 0, 0)
            });
            return;
        }

        foreach (var svc in services)
            ServiceList.Children.Add(BuildServiceCard(svc));
    }

    private Border BuildServiceCard(ServiceInfo svc)
    {
        var isRunning  = svc.State == "Running";
        var isStopped  = svc.State == "Stopped";
        var isDisabled = svc.StartMode == "Disabled";

        var stateColor = isRunning ? Green : isDisabled ? Red : Gray;

        var card = new Border
        {
            Background = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(16, 10, 16, 10),
            BorderBrush = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["CardStrokeColorDefaultBrush"],
            BorderThickness = new Thickness(0.5),
            Margin = new Thickness(0, 0, 0, 0)
        };

        var grid = new Grid { ColumnSpacing = 12 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });          // status dot
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // info
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });          // startup badge
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });          // pid
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });          // state badge
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });          // actions

        // Status dot
        var dot = new Border
        {
            Width = 10, Height = 10, CornerRadius = new CornerRadius(5),
            Background = new SolidColorBrush(stateColor),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 2, 0)
        };
        Grid.SetColumn(dot, 0); grid.Children.Add(dot);

        // Display name + service name
        var info = new StackPanel { Spacing = 1, VerticalAlignment = VerticalAlignment.Center };
        info.Children.Add(new TextBlock
        {
            Text = svc.DisplayName,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            FontSize = 13,
            TextTrimming = TextTrimming.CharacterEllipsis
        });
        var detail = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        detail.Children.Add(new TextBlock
        {
            Text = svc.Name, FontSize = 10,
            FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas"),
            Opacity = 0.4, VerticalAlignment = VerticalAlignment.Center
        });
        if (!string.IsNullOrEmpty(svc.Description))
            detail.Children.Add(new TextBlock
            {
                Text = svc.Description.Length > 80 ? svc.Description[..80] + "..." : svc.Description,
                FontSize = 10, Opacity = 0.35,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 340, VerticalAlignment = VerticalAlignment.Center
            });
        info.Children.Add(detail);
        Grid.SetColumn(info, 1); grid.Children.Add(info);

        // Startup type badge
        var startColor = svc.StartMode switch
        {
            "Auto"     => Blue,
            "Manual"   => Amber,
            "Disabled" => Red,
            _          => Gray
        };
        var startBadge = new Border
        {
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(25, startColor.R, startColor.G, startColor.B)),
            CornerRadius = new CornerRadius(4), Padding = new Thickness(7, 2, 7, 2),
            VerticalAlignment = VerticalAlignment.Center
        };
        startBadge.Child = new TextBlock
        {
            Text = svc.StartMode.ToUpper(), FontSize = 9,
            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            Foreground = new SolidColorBrush(startColor)
        };
        Grid.SetColumn(startBadge, 2); grid.Children.Add(startBadge);

        // PID (if running)
        var pidText = new TextBlock
        {
            Text = isRunning && svc.ProcessId > 0 ? $"PID {svc.ProcessId}" : "",
            FontSize = 10, FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas"),
            Opacity = 0.4, VerticalAlignment = VerticalAlignment.Center, MinWidth = 60
        };
        Grid.SetColumn(pidText, 3); grid.Children.Add(pidText);

        // State badge
        var stateBadge = new Border
        {
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(25, stateColor.R, stateColor.G, stateColor.B)),
            CornerRadius = new CornerRadius(4), Padding = new Thickness(7, 2, 7, 2),
            VerticalAlignment = VerticalAlignment.Center, MinWidth = 65
        };
        stateBadge.Child = new TextBlock
        {
            Text = svc.State.ToUpper(), FontSize = 9,
            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            Foreground = new SolidColorBrush(stateColor),
            TextAlignment = TextAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        Grid.SetColumn(stateBadge, 4); grid.Children.Add(stateBadge);

        // Action buttons
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, VerticalAlignment = VerticalAlignment.Center };

        if (isStopped && !isDisabled)
        {
            var startBtn = new Button
            {
                Content = S._("common.start"), Padding = new Thickness(10, 3, 10, 3), FontSize = 11,
                IsEnabled = IsAdmin
            };
            startBtn.Click += async (s, ev) => await ControlServiceAsync(svc.Name, "start", startBtn);
            actions.Children.Add(startBtn);
        }

        if (isRunning)
        {
            var stopBtn = new Button
            {
                Content = S._("common.stop"), Padding = new Thickness(10, 3, 10, 3), FontSize = 11,
                IsEnabled = IsAdmin
            };
            stopBtn.Click += async (s, ev) => await ControlServiceAsync(svc.Name, "stop", stopBtn);
            actions.Children.Add(stopBtn);

            var restartBtn = new Button
            {
                Content = S._("common.restart"), Padding = new Thickness(10, 3, 10, 3), FontSize = 11,
                IsEnabled = IsAdmin
            };
            restartBtn.Click += async (s, ev) => await RestartServiceAsync(svc.Name, restartBtn);
            actions.Children.Add(restartBtn);
        }

        // Startup type selector (compact)
        var startupBox = new ComboBox
        {
            FontSize = 11, Padding = new Thickness(8, 2, 8, 2),
            IsEnabled = IsAdmin, MinWidth = 90
        };
        startupBox.Items.Add(new ComboBoxItem { Content = S._("svc.startupAuto"),     Tag = "auto" });
        startupBox.Items.Add(new ComboBoxItem { Content = S._("svc.startupManual"),   Tag = "demand" });
        startupBox.Items.Add(new ComboBoxItem { Content = S._("svc.startupDisabled"), Tag = "disabled" });
        // Select current
        var currentTag = svc.StartMode switch { "Auto" => "auto", "Disabled" => "disabled", _ => "demand" };
        foreach (ComboBoxItem ci in startupBox.Items)
            if (ci.Tag?.ToString() == currentTag) startupBox.SelectedItem = ci;
        var capturedName = svc.Name;
        startupBox.SelectionChanged += async (s, ev) =>
        {
            if (startupBox.SelectedItem is ComboBoxItem sel && sel.Tag?.ToString() is string mode)
                await ChangeStartupTypeAsync(capturedName, mode, startupBox);
        };
        actions.Children.Add(startupBox);

        Grid.SetColumn(actions, 5); grid.Children.Add(actions);
        card.Child = grid;
        return card;
    }

    // ── SERVICE CONTROL ───────────────────────────────────────

    private async Task ControlServiceAsync(string name, string action, Button btn)
    {
        btn.IsEnabled = false;
        StatusText.Text = $"{action.First().ToString().ToUpper() + action[1..]}ing {name}...";
        try
        {
            var result = await Task.Run(() =>
            {
                var p = Process.Start(new ProcessStartInfo
                {
                    FileName = "sc.exe",
                    Arguments = $"{action} \"{name}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
                p?.WaitForExit(10000);
                return p?.ExitCode ?? -1;
            });
            StatusText.Text = result == 0
                ? $"{name} {action}ped successfully."
                : $"Failed to {action} {name} (exit code {result}).";
            await Task.Delay(500);
            ScanBtn_Click(ScanBtn, new RoutedEventArgs());
        }
        catch (Exception ex) { StatusText.Text = $"Error: {ex.Message}"; btn.IsEnabled = IsAdmin; }
    }

    private async Task RestartServiceAsync(string name, Button btn)
    {
        btn.IsEnabled = false;
        StatusText.Text = $"Restarting {name}...";
        try
        {
            await Task.Run(() =>
            {
                RunSc($"stop \"{name}\"");
                System.Threading.Thread.Sleep(2000);
                RunSc($"start \"{name}\"");
            });
            StatusText.Text = $"{name} restarted.";
            await Task.Delay(500);
            ScanBtn_Click(ScanBtn, new RoutedEventArgs());
        }
        catch (Exception ex) { StatusText.Text = $"Error: {ex.Message}"; btn.IsEnabled = IsAdmin; }
    }

    private async Task ChangeStartupTypeAsync(string name, string mode, ComboBox box)
    {
        if (!IsAdmin) return;
        box.IsEnabled = false;
        try
        {
            await Task.Run(() => RunSc($"config \"{name}\" start= {mode}"));
            StatusText.Text = $"Startup type for {name} changed to {mode}.";
        }
        catch (Exception ex) { StatusText.Text = $"Error: {ex.Message}"; }
        finally { box.IsEnabled = IsAdmin; }
    }

    private static void RunSc(string args)
    {
        var p = Process.Start(new ProcessStartInfo
        {
            FileName = "sc.exe", Arguments = args,
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true
        });
        p?.WaitForExit(10000);
    }

    // ── LOCALIZATION ─────────────────────────────────────────

    private void ApplyLocalization()
    {
        try
        {
            PageTitle.Text    = S._("svc.title");
            PageSubtitle.Text = S._("svc.subtitle");
            ScanBtn.Content   = S._("svc.scanBtn");
            StatusText.Text   = S._("svc.scanStatus");
        }
        catch { }
    }
}
