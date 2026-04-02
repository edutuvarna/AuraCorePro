using System.Diagnostics;
using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Interactivity;
using global::Avalonia.Layout;
using global::Avalonia.Media;
using AuraCore.Application;
using AuraCore.Application.Interfaces.Modules;
using AuraCore.Module.BloatwareRemoval;
using AuraCore.Module.BloatwareRemoval.Models;
using Microsoft.Extensions.DependencyInjection;

namespace AuraCore.UI.Avalonia.Views.Pages;

public record BloatDisplayItem(string Name, string Publisher, string Category, string Size,
    string RiskText, ISolidColorBrush RiskFg, ISolidColorBrush RiskBg, bool CanRemove, string PkgName,
    string PackageFamilyName);

public partial class BloatwareRemovalView : UserControl
{
    private readonly BloatwareRemovalModule? _module;
    private List<BloatDisplayItem> _currentItems = new();
    // Track which items are checked for removal
    private readonly HashSet<string> _checkedItems = new();
    // Track removed apps for reinstall
    private readonly HashSet<string> _removedAppIds = new();

    public BloatwareRemovalView()
    {
        InitializeComponent();
        Loaded += (s, e) => ApplyLocalization();
        LocalizationService.LanguageChanged += () =>
            global::Avalonia.Threading.Dispatcher.UIThread.Post(ApplyLocalization);
        _module = App.Services.GetServices<IOptimizationModule>()
            .OfType<BloatwareRemovalModule>().FirstOrDefault();
        Loaded += async (s, e) => await RunScan();
    }

    private async Task RunScan()
    {
        if (_module is null) return;
        ScanLabel.Text = "Scanning...";
        try
        {
            await _module.ScanAsync(new ScanOptions());
            var r = _module.LastReport;
            if (r is null) return;
            TotalApps.Text = r.TotalApps.ToString();
            RemovableApps.Text = r.RemovableApps.ToString();
            TotalSize.Text = FormatBytes(r.TotalRemovableBytes);
            _currentItems = r.Apps.Select(a =>
            {
                var canRemove = a.Risk != BloatRisk.System && !a.IsFramework;
                var (fg, bg) = a.Risk switch
                {
                    BloatRisk.Safe    => (P("#22C55E"), P("#2022C55E")),
                    BloatRisk.Caution => (P("#F59E0B"), P("#20F59E0B")),
                    BloatRisk.Warning => (P("#EF4444"), P("#20EF4444")),
                    _                 => (P("#8888A0"), P("#208888A0"))
                };
                // Derive package family name from full name (everything before the version hash)
                var familyName = DerivePackageFamilyName(a.PackageFullName, a.Name);
                return new BloatDisplayItem(a.DisplayName, a.Publisher, a.Category.ToString(),
                    a.SizeDisplay, a.Risk.ToString(), fg, bg, canRemove, a.PackageFullName, familyName);
            }).ToList();

            _checkedItems.Clear();
            foreach (var item in _currentItems.Where(i => i.CanRemove))
                _checkedItems.Add(item.PkgName);

            BuildAppListUI();
            RemoveBtn.IsEnabled = _currentItems.Any(i => i.CanRemove);
        }
        catch { SubText.Text = "Scan failed"; }
        finally { ScanLabel.Text = "Scan"; }
    }

    private void BuildAppListUI()
    {
        var controls = new List<Control>();
        foreach (var item in _currentItems)
        {
            var row = new Border
            {
                Padding = new Thickness(12, 6), Margin = new Thickness(0, 0, 0, 4),
                CornerRadius = new CornerRadius(6),
                BorderThickness = new Thickness(1),
                Background = new SolidColorBrush(Color.Parse("#FFFFFF"), 0.02),
                BorderBrush = new SolidColorBrush(Color.Parse("#FFFFFF"), 0.04),
            };

            var grid = new Grid { ColumnDefinitions = ColumnDefinitions.Parse("40,2*,100,80,80,70") };

            // Checkbox
            var cb = new CheckBox
            {
                IsChecked = _checkedItems.Contains(item.PkgName),
                IsEnabled = item.CanRemove,
                VerticalAlignment = VerticalAlignment.Center,
                Tag = item.PkgName
            };
            cb.IsCheckedChanged += (s, e) =>
            {
                if (s is CheckBox c && c.Tag is string pkg)
                {
                    if (c.IsChecked == true) _checkedItems.Add(pkg);
                    else _checkedItems.Remove(pkg);
                }
            };
            grid.Children.Add(cb);

            // Name + publisher
            var nameStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            nameStack.Children.Add(new TextBlock
            {
                Text = item.Name, FontSize = 11, FontWeight = FontWeight.SemiBold,
                Foreground = new SolidColorBrush(Color.Parse("#E0E0F0")),
                TextTrimming = global::Avalonia.Media.TextTrimming.CharacterEllipsis
            });
            nameStack.Children.Add(new TextBlock
            {
                Text = item.Publisher, FontSize = 9,
                Foreground = new SolidColorBrush(Color.Parse("#8888A0"))
            });
            Grid.SetColumn(nameStack, 1);
            grid.Children.Add(nameStack);

            // Category
            var catBlock = new TextBlock
            {
                Text = item.Category, FontSize = 10,
                Foreground = new SolidColorBrush(Color.Parse("#A0A0C0")),
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = global::Avalonia.Media.TextAlignment.Center
            };
            Grid.SetColumn(catBlock, 2);
            grid.Children.Add(catBlock);

            // Size
            var sizeBlock = new TextBlock
            {
                Text = item.Size, FontSize = 10,
                Foreground = new SolidColorBrush(Color.Parse("#A0A0C0")),
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = global::Avalonia.Media.TextAlignment.Right
            };
            Grid.SetColumn(sizeBlock, 3);
            grid.Children.Add(sizeBlock);

            // Risk badge
            var riskBorder = new Border
            {
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(6, 2),
                Background = item.RiskBg,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            riskBorder.Child = new TextBlock
            {
                Text = item.RiskText, FontSize = 9, FontWeight = FontWeight.SemiBold,
                Foreground = item.RiskFg
            };
            Grid.SetColumn(riskBorder, 4);
            grid.Children.Add(riskBorder);

            // Reinstall button (shown for removed apps, or hidden)
            var reinstallBtn = new Button
            {
                Content = "Reinstall", FontSize = 9,
                Padding = new Thickness(6, 2),
                VerticalAlignment = VerticalAlignment.Center,
                Tag = item.PkgName,
                IsVisible = _removedAppIds.Contains(item.PkgName)
            };
            reinstallBtn.Click += ReinstallBtn_Click;
            Grid.SetColumn(reinstallBtn, 5);
            grid.Children.Add(reinstallBtn);

            row.Child = grid;
            controls.Add(row);
        }
        AppList.ItemsSource = controls;
    }

    private static SolidColorBrush P(string hex) => new(Color.Parse(hex));

    private static string FormatBytes(long b) => b switch
    {
        >= 1073741824 => $"{b / 1073741824.0:F1} GB",
        >= 1048576    => $"{b / 1048576.0:F1} MB",
        >= 1024       => $"{b / 1024.0:F1} KB",
        _             => $"{b} B"
    };

    private static string DerivePackageFamilyName(string packageFullName, string name)
    {
        // PackageFullName format: Name_Version_Arch__PublisherId
        // PackageFamilyName format: Name_PublisherId
        if (string.IsNullOrEmpty(packageFullName)) return name;
        var parts = packageFullName.Split('_');
        if (parts.Length >= 5)
            return $"{parts[0]}_{parts[^1]}"; // Name_PublisherId
        return name;
    }

    private async void Scan_Click(object? sender, RoutedEventArgs e) => await RunScan();

    private async void RemoveSelected_Click(object? sender, RoutedEventArgs e)
    {
        if (_module is null) return;
        var toRemove = _currentItems.Where(i => _checkedItems.Contains(i.PkgName) && i.CanRemove).ToList();
        if (toRemove.Count == 0) return;

        RemoveBtn.IsEnabled = false;
        var removed = 0;
        long freed = 0;
        bool blockReturn = BlockReturnCheck.IsChecked == true;

        foreach (var item in toRemove)
        {
            StatusText.Text = $"Removing {item.Name}... ({removed + 1}/{toRemove.Count})";
            StatusBarService.SetStatus($"Removing {item.Name}... ({removed + 1}/{toRemove.Count})");
            StatusBarService.SetProgress($"{removed + 1}/{toRemove.Count}", (double)(removed + 1) / toRemove.Count);

            try
            {
                var plan = new OptimizationPlan(_module.Id, new[] { item.PkgName });
                var result = await _module.OptimizeAsync(plan);
                if (result.Success)
                {
                    removed++;
                    freed += result.BytesFreed;
                    _removedAppIds.Add(item.PkgName);

                    // Block from returning via registry
                    if (blockReturn && OperatingSystem.IsWindows())
                    {
                        await BlockAppFromReturning(item.PackageFamilyName);
                    }
                }
            }
            catch { }
        }

        StatusText.Text = $"Done! Removed {removed}/{toRemove.Count} apps. Freed {FormatBytes(freed)}";
        StatusBarService.SetStatus($"Removed {removed} bloatware app(s). Freed {FormatBytes(freed)}");
        StatusBarService.ClearProgress();

        await RunScan();
        RemoveBtn.IsEnabled = true;
    }

    /// <summary>
    /// Blocks an AppX package from being reinstalled by Windows Updates.
    /// Uses the Deprovisioned registry key approach.
    /// </summary>
    private static async Task BlockAppFromReturning(string packageFamilyName)
    {
        if (!OperatingSystem.IsWindows() || string.IsNullOrEmpty(packageFamilyName)) return;

        try
        {
            // Method: Add to deprovisioned apps list in registry
            // HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Appx\AppxAllUserStore\Deprovisioned\{PackageFamilyName}
            var regPath = $@"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Appx\AppxAllUserStore\Deprovisioned\{packageFamilyName}";
            var psi = new ProcessStartInfo
            {
                FileName = "reg.exe",
                Arguments = $"add \"{regPath}\" /f",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            if (proc != null)
                await proc.WaitForExitAsync();

            // Also ensure consumer features are disabled to prevent future reinstalls
            var consumerPath = @"HKLM\SOFTWARE\Policies\Microsoft\Windows\CloudContent";
            var psi2 = new ProcessStartInfo
            {
                FileName = "reg.exe",
                Arguments = $"add \"{consumerPath}\" /v DisableWindowsConsumerFeatures /t REG_DWORD /d 1 /f",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc2 = Process.Start(psi2);
            if (proc2 != null)
                await proc2.WaitForExitAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[bloatware-removal] Block failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Reinstall a previously removed app via winget.
    /// Derives a winget-compatible ID from the package name.
    /// </summary>
    private async void ReinstallBtn_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button btn) return;
        var pkgName = btn.Tag?.ToString();
        if (string.IsNullOrEmpty(pkgName)) return;

        btn.IsEnabled = false;
        btn.Content = "Reinstalling...";

        // Derive a search-friendly name from the package family/full name
        var searchName = pkgName.Split('_')[0]; // e.g., "Microsoft.BingWeather"

        StatusText.Text = $"Reinstalling {searchName} via winget...";
        StatusBarService.SetStatus($"Reinstalling {searchName}...");

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "winget",
                Arguments = $"install --name \"{searchName}\" --accept-source-agreements --accept-package-agreements --silent",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            if (proc != null)
            {
                await proc.WaitForExitAsync();
                if (proc.ExitCode == 0)
                {
                    btn.Content = "Reinstalled";
                    _removedAppIds.Remove(pkgName);
                    StatusText.Text = $"{searchName} reinstalled successfully";
                    StatusBarService.SetStatus($"{searchName} reinstalled");
                }
                else
                {
                    // Try with Microsoft Store source
                    var psi2 = new ProcessStartInfo
                    {
                        FileName = "winget",
                        Arguments = $"install --name \"{searchName}\" --source msstore --accept-source-agreements --accept-package-agreements --silent",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    using var proc2 = Process.Start(psi2);
                    if (proc2 != null)
                    {
                        await proc2.WaitForExitAsync();
                        if (proc2.ExitCode == 0)
                        {
                            btn.Content = "Reinstalled";
                            _removedAppIds.Remove(pkgName);
                            StatusText.Text = $"{searchName} reinstalled from Store";
                            StatusBarService.SetStatus($"{searchName} reinstalled");
                        }
                        else
                        {
                            btn.Content = "Failed";
                            btn.IsEnabled = true;
                            StatusText.Text = $"Failed to reinstall {searchName}";
                            StatusBarService.SetStatus($"Failed to reinstall {searchName}");
                        }
                    }
                }
            }
        }
        catch
        {
            btn.Content = "Failed";
            btn.IsEnabled = true;
            StatusText.Text = $"Reinstall failed for {searchName}";
            StatusBarService.SetStatus($"Reinstall failed");
        }
    }

    private void ApplyLocalization()
    {
        PageTitle.Text = LocalizationService._("nav.bloatware");
    }
}
