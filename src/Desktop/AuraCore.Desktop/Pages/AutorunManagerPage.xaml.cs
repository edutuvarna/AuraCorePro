using AuraCore.Application;
using AuraCore.Desktop.Services;
using AuraCore.Module.AutorunManager;
using AuraCore.Module.AutorunManager.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace AuraCore.Desktop.Pages;

public sealed partial class AutorunManagerPage : Page
{
    private AutorunManagerModule? _module;
    private List<AutorunEntry> _allEntries = new();
    private static readonly bool IsAdmin = IsRunningAsAdmin();

    public AutorunManagerPage()
    {
        InitializeComponent();
        ApplyLocalization();
        S.LanguageChanged += () => DispatcherQueue?.TryEnqueue(ApplyLocalization);

        FilterBox.Items.Add(new ComboBoxItem { Content = "All Types", Tag = "all" });
        FilterBox.Items.Add(new ComboBoxItem { Content = "Registry", Tag = "Registry" });
        FilterBox.Items.Add(new ComboBoxItem { Content = "Startup Folder", Tag = "StartupFolder" });
        FilterBox.SelectedIndex = 0;

        Loaded += Page_Loaded;
    }

    private async void Page_Loaded(object sender, RoutedEventArgs e)
    {
        _module = App.Current.Services.GetServices<AuraCore.Application.Interfaces.Modules.IOptimizationModule>()
            .FirstOrDefault(m => m.Id == "autorun-manager") as AutorunManagerModule;

        if (!IsAdmin)
        {
            AdminWarning.Visibility = Visibility.Visible;
            AdminWarnText.Text = S._("autorun.adminWarning");
        }

        await RunScan();
    }

    private async void Scan_Click(object sender, RoutedEventArgs e) => await RunScan();

    private async Task RunScan()
    {
        if (_module is null) return;
        ScanBtn.IsEnabled = false;
        ScanProgress.IsActive = true;
        StatusText.Text = S._("autorun.scanning");
        EntryList.Children.Clear();

        await _module.ScanAsync(new ScanOptions());

        _allEntries = _module.LastReport?.Entries ?? new();
        RenderEntries(_allEntries);
        UpdateStats();

        ScanProgress.IsActive = false;
        ScanBtn.IsEnabled = true;
        StatusText.Text = string.Format(S._("autorun.found"), _allEntries.Count,
            _allEntries.Count(e => e.IsEnabled));
    }

    private void RenderEntries(List<AutorunEntry> entries)
    {
        EntryList.Children.Clear();
        if (entries.Count == 0)
        {
            EntryList.Children.Add(new TextBlock { Text = S._("common.noData"), Opacity = 0.5, Margin = new Thickness(0, 16, 0, 0) });
            return;
        }

        foreach (var entry in entries)
        {
            var row = new Grid
            {
                ColumnSpacing = 8, Padding = new Thickness(4, 8, 4, 8),
                BorderBrush = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["CardStrokeColorDefaultBrush"],
                BorderThickness = new Thickness(0, 0, 0, 0.5)
            };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) });

            // Name + Command
            var nameStack = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
            nameStack.Children.Add(new TextBlock
            {
                Text = entry.Name, FontSize = 13,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Opacity = entry.IsEnabled ? 1.0 : 0.45
            });
            nameStack.Children.Add(new TextBlock
            {
                Text = entry.Command, FontSize = 10, Opacity = 0.35,
                TextTrimming = TextTrimming.CharacterEllipsis, MaxLines = 1
            });
            Grid.SetColumn(nameStack, 0); row.Children.Add(nameStack);

            // Location
            var locText = new TextBlock { Text = entry.Location, FontSize = 11, Opacity = 0.55, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
            Grid.SetColumn(locText, 1); row.Children.Add(locText);

            // Risk badge
            var (riskColor, riskBg) = entry.RiskLevel switch
            {
                "Safe"   => ("#2E7D32", "#1A2E7D32"),
                "Low"    => ("#1565C0", "#1A1565C0"),
                "Medium" => ("#E65100", "#1AE65100"),
                "High"   => ("#B71C1C", "#1AB71C1C"),
                _        => ("#607D8B", "#1A607D8B")
            };
            var riskBadge = new Border
            {
                Background = new SolidColorBrush(ParseColor(riskBg)),
                CornerRadius = new CornerRadius(4), Padding = new Thickness(8, 3, 8, 3),
                VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Left
            };
            riskBadge.Child = new TextBlock
            {
                Text = entry.RiskLevel, FontSize = 10,
                Foreground = new SolidColorBrush(ParseColor(riskColor)),
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
            };
            Grid.SetColumn(riskBadge, 2); row.Children.Add(riskBadge);

            // Status toggle
            var toggle = new ToggleSwitch
            {
                IsOn = entry.IsEnabled, OnContent = "", OffContent = "",
                Margin = new Thickness(0, -4, 0, -4), VerticalAlignment = VerticalAlignment.Center,
                Tag = entry
            };
            toggle.Toggled += Toggle_Toggled;
            Grid.SetColumn(toggle, 3); row.Children.Add(toggle);

            // Actions
            var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
            var capturedEntry = entry;

            var deleteBtn = new Button { Content = S._("common.remove"), Padding = new Thickness(10, 3, 10, 3), FontSize = 11 };
            deleteBtn.Click += async (s, ev) =>
            {
                var dlg = new ContentDialog
                {
                    Title = S._("autorun.deleteTitle"),
                    Content = string.Format(S._("autorun.deleteMsg"), capturedEntry.Name),
                    PrimaryButtonText = S._("common.remove"),
                    CloseButtonText = S._("common.cancel"),
                    XamlRoot = this.XamlRoot, DefaultButton = ContentDialogButton.Close
                };
                if (await dlg.ShowAsync() != ContentDialogResult.Primary) return;
                var plan = new OptimizationPlan("autorun-manager", new List<string> { $"delete:{capturedEntry.Name}" });
                await _module!.OptimizeAsync(plan);
                await RunScan();
            };
            btnPanel.Children.Add(deleteBtn);
            Grid.SetColumn(btnPanel, 4); row.Children.Add(btnPanel);

            EntryList.Children.Add(row);
        }
    }

    private async void Toggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleSwitch ts || ts.Tag is not AutorunEntry entry || _module is null) return;
        var action = ts.IsOn ? "enable" : "disable";
        var plan = new OptimizationPlan("autorun-manager", new List<string> { $"{action}:{entry.Name}" });
        await _module.OptimizeAsync(plan);
        entry.IsEnabled = ts.IsOn;
        UpdateStats();
        StatusText.Text = string.Format(S._("autorun.toggled"), entry.Name,
            ts.IsOn ? S._("autorun.enabled") : S._("autorun.disabled"));
    }

    private void Filter_Changed(object sender, SelectionChangedEventArgs e)  => ApplyFilter();
    private void Search_Changed(object sender, TextChangedEventArgs e) => ApplyFilter();

    private void ApplyFilter()
    {
        var tag = (FilterBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "all";
        var q   = SearchBox.Text.Trim().ToLower();
        var filtered = _allEntries.Where(en =>
            (tag == "all" || en.Type.ToString() == tag) &&
            (string.IsNullOrEmpty(q) || en.Name.ToLower().Contains(q) || en.Command.ToLower().Contains(q))
        ).ToList();
        RenderEntries(filtered);
    }

    private void UpdateStats()
    {
        StatTotal.Text    = _allEntries.Count.ToString();
        StatEnabled.Text  = _allEntries.Count(e => e.IsEnabled).ToString();
        StatDisabled.Text = _allEntries.Count(e => !e.IsEnabled).ToString();
    }

    private void ApplyLocalization()
    {
        PageTitle.Text    = S._("autorun.title");
        PageSubtitle.Text = S._("autorun.subtitle");
        ScanBtn.Content   = S._("common.scan");
    }

    private static Windows.UI.Color ParseColor(string hex)
    {
        hex = hex.TrimStart('#');
        if (hex.Length == 8)
            return Windows.UI.Color.FromArgb(
                Convert.ToByte(hex[..2], 16), Convert.ToByte(hex[2..4], 16),
                Convert.ToByte(hex[4..6], 16), Convert.ToByte(hex[6..8], 16));
        return Windows.UI.Color.FromArgb(255,
            Convert.ToByte(hex[..2], 16), Convert.ToByte(hex[2..4], 16),
            Convert.ToByte(hex[4..6], 16));
    }

    private static bool IsRunningAsAdmin()
    {
        try
        {
            using var id = System.Security.Principal.WindowsIdentity.GetCurrent();
            return new System.Security.Principal.WindowsPrincipal(id)
                .IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
        }
        catch { return false; }
    }
}
