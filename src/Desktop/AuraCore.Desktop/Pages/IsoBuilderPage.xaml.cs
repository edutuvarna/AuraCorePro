using AuraCore.Application;
using AuraCore.Desktop.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using Windows.Storage.Pickers;
using Windows.UI;

using System.Runtime.InteropServices;

namespace AuraCore.Desktop.Pages;

public sealed partial class IsoBuilderPage : Page
{
    private string? _isoPath;
    private readonly List<BloatwareItem> _bloatware = new();
    private readonly List<string> _exePaths = new();
    private readonly List<DriverItem> _drivers = new();
    private readonly List<WingetApp> _wingetApps = new();
    private readonly List<string> _customWingetIds = new();
    private DateTime _buildStartTime;
    private readonly StringBuilder _terminalLog = new();

    private int _currentStep = 0;
    private const int TotalSteps = 12;
    private string[] _stepTitles = {
        "Select ISO", "User Account", "OOBE Settings", "WiFi",
        "Win11 Bypass", "Bloatware", "EXE Bundler", "Driver Backup",
        "Winget Apps", "Registry Presets", "Post-Install", "Review & Build"
    };
    private readonly List<Button> _stepButtons = new();

    public IsoBuilderPage()
    {
        InitializeComponent();
    }

    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            var tier = SessionState.UserTier ?? "free";
            if (tier == "free")
            {
                TierLockBanner.Visibility = Visibility.Visible;
                BuildBtn.IsEnabled = false;
                NextBtn.IsEnabled = false;
            }

            BuildStepper();
            ShowStep(0);
            PopulateBloatware();
            BuildBloatwareUI();
            PopulateWingetApps();
            BuildWingetUI();

            // Admin status
            try
            {
                if (IsAdmin())
                {
                    AdminStatusText.Text = "Running as Administrator";
                    AdminStatusText.Foreground = new SolidColorBrush(Color.FromArgb(255, 6, 214, 160));
                }
                else
                {
                    AdminStatusText.Text = "Not Administrator (limited)";
                    AdminStatusText.Foreground = new SolidColorBrush(Color.FromArgb(255, 245, 158, 11));
                }
            }
            catch { }

            UpdateSummary();
            ApplyIsoLocalization();
            Services.S.LanguageChanged += () => DispatcherQueue?.TryEnqueue(ApplyIsoLocalization);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Page_Loaded error: {ex}");
        }
    }

    // ══════════════════════════════════════════════
    // WIZARD NAVIGATION
    // ══════════════════════════════════════════════

    private void BuildStepper()
    {
        StepperPanel.Children.Clear();
        _stepButtons.Clear();

        for (int i = 0; i < TotalSteps; i++)
        {
            var idx = i;
            var btn = new Button
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Padding = new Thickness(12, 10, 12, 10),
                Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                BorderThickness = new Thickness(0),
                CornerRadius = new CornerRadius(6)
            };

            var sp = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };

            var numBorder = new Border
            {
                Width = 24,
                Height = 24,
                CornerRadius = new CornerRadius(12),
                Background = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255))
            };

            var numText = new TextBlock
            {
                Text = (i + 1).ToString(),
                FontSize = 11,
                FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            numBorder.Child = numText;
            sp.Children.Add(numBorder);
            sp.Children.Add(new TextBlock
            {
                Text = _stepTitles[i],
                FontSize = 13,
                VerticalAlignment = VerticalAlignment.Center
            });

            btn.Content = sp;
            btn.Click += (s, e) => ShowStep(idx);

            StepperPanel.Children.Add(btn);
            _stepButtons.Add(btn);
        }
    }

    private void ShowStep(int step)
    {
        if (step < 0 || step >= TotalSteps) return;
        _currentStep = step;

        // Hide all panels
        for (int i = 0; i < TotalSteps; i++)
        {
            if (FindName($"Panel_{i}") is StackPanel panel)
            {
                panel.Visibility = Visibility.Collapsed;
            }
        }

        // Show active panel
        if (FindName($"Panel_{step}") is StackPanel active)
        {
            active.Visibility = Visibility.Visible;
        }

        // Update stepper highlighting
        for (int i = 0; i < _stepButtons.Count; i++)
        {
            var btn = _stepButtons[i];
            if (btn.Content is not StackPanel sp || sp.Children.Count < 2) continue;

            var numBorder = sp.Children[0] as Border;
            var label = sp.Children[1] as TextBlock;
            var numTextBlock = numBorder?.Child as TextBlock;

            if (i == step)
            {
                btn.Background = new SolidColorBrush(Color.FromArgb(20, 6, 214, 160));
                if (numBorder is not null)
                    numBorder.Background = new SolidColorBrush(Color.FromArgb(255, 6, 214, 160));
                if (numTextBlock is not null)
                    numTextBlock.Foreground = new SolidColorBrush(Color.FromArgb(255, 4, 6, 14));
                if (label is not null)
                    label.Foreground = new SolidColorBrush(Color.FromArgb(255, 226, 232, 240));
            }
            else
            {
                btn.Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
                if (numBorder is not null)
                    numBorder.Background = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255));
                if (numTextBlock is not null)
                    numTextBlock.Foreground = new SolidColorBrush(Color.FromArgb(255, 148, 163, 184));
                if (label is not null)
                    label.Foreground = new SolidColorBrush(Color.FromArgb(255, 148, 163, 184));
            }
        }

        // Nav buttons
        BackBtn.Visibility = step > 0 ? Visibility.Visible : Visibility.Collapsed;
        NextBtn.Visibility = step < TotalSteps - 1 ? Visibility.Visible : Visibility.Collapsed;
        BuildBtn.Visibility = step == TotalSteps - 1 ? Visibility.Visible : Visibility.Collapsed;

        StepIndicator.Text = $"Step {step + 1} of {TotalSteps}";

        if (step == TotalSteps - 1) UpdateSummary();
    }

    private void Next_Click(object s, RoutedEventArgs e) { ShowStep(_currentStep + 1); }
    private void Back_Click(object s, RoutedEventArgs e) { ShowStep(_currentStep - 1); }
    private void Upgrade_Click(object s, RoutedEventArgs e) { Frame.Navigate(typeof(UpgradePage)); }

    private void DisclaimerCheck_Changed(object s, RoutedEventArgs e)
    {
        if (BuildBtn is not null)
            BuildBtn.IsEnabled = DisclaimerCheck?.IsChecked == true;
    }

    private void AccountType_Changed(object s, RoutedEventArgs e)
    {
        if (LocalAccountPanel is not null)
            LocalAccountPanel.Visibility = LocalAccountRadio?.IsChecked == true
                ? Visibility.Visible : Visibility.Collapsed;
    }

    private void WifiToggle_Changed(object s, RoutedEventArgs e)
    {
        if (WifiPanel is not null)
            WifiPanel.Visibility = EnableWifiCheck?.IsChecked == true
                ? Visibility.Visible : Visibility.Collapsed;
    }

    // ══════════════════════════════════════════════
    // POWERSHELL
    // ══════════════════════════════════════════════

    private static async Task<string> RunPowershell(string script)
    {
        var tmp = Path.Combine(Path.GetTempPath(), $"ac_{Guid.NewGuid():N}.ps1");
        try
        {
            await File.WriteAllTextAsync(tmp, script, Encoding.UTF8);
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{tmp}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var p = Process.Start(psi);
            if (p is null) throw new Exception("PowerShell failed to start");
            var output = await p.StandardOutput.ReadToEndAsync();
            await p.WaitForExitAsync();
            return output;
        }
        finally
        {
            try { File.Delete(tmp); } catch { }
        }
    }

    private static bool IsAdmin()
    {
        using var id = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator);
    }

    // ══════════════════════════════════════════════
    // STEP 0: ISO
    // ══════════════════════════════════════════════

    private async void BrowseIso_Click(object s, RoutedEventArgs e)
    {
        try
        {
            var picker = new FileOpenPicker { SuggestedStartLocation = PickerLocationId.Downloads };
            picker.FileTypeFilter.Add(".iso");
            WinRT.Interop.InitializeWithWindow.Initialize(picker,
                WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow));

            var file = await picker.PickSingleFileAsync();
            if (file is null) return;

            _isoPath = file.Path;
            IsoPathText.Text = file.Path;

            var fi = new FileInfo(file.Path);
            var gb = fi.Length / (1024.0 * 1024.0 * 1024.0);
            IsoSizeText.Text = $"Size: {gb:F2} GB";
            IsoFileText.Text = $"File: {Path.GetFileName(_isoPath)}";

            var n = Path.GetFileName(_isoPath).ToLower();
            if (n.Contains("win11") || n.Contains("windows11") || n.Contains("windows_11"))
            {
                IsoOsText.Text = "Windows 11";
            }
            else if (n.Contains("win10") || n.Contains("windows10") || n.Contains("windows_10"))
            {
                IsoOsText.Text = "Windows 10";
            }
            else
            {
                IsoOsText.Text = "Windows (auto-detect)";
            }

            if (n.Contains("x64") || n.Contains("amd64"))
                IsoArchText.Text = "Architecture: x64";
            else if (n.Contains("arm64"))
                IsoArchText.Text = "Architecture: ARM64";
            else
                IsoArchText.Text = "Architecture: x64 (assumed)";

            IsoInfoPanel.Visibility = Visibility.Visible;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"BrowseIso: {ex}");
            IsoPathText.Text = $"Error: {ex.Message}";
            // Fallback: try Win32 dialog
            try
            {
                var hwnd2 = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
                var fallback = ShowWin32OpenDialog("Select Windows ISO", "ISO Files (*.iso)\0*.iso\0", hwnd2);
                if (!string.IsNullOrEmpty(fallback))
                {
                    _isoPath = fallback;
                    IsoPathText.Text = fallback;
                    var fi2 = new FileInfo(fallback);
                    IsoSizeText.Text = $"Size: {fi2.Length / (1024.0 * 1024 * 1024):F2} GB";
                    IsoFileText.Text = $"File: {fi2.Name}";
                    var fn = fi2.Name.ToLower();
                    if (fn.Contains("win11") || fn.Contains("windows11") || fn.Contains("windows_11"))
                        IsoOsText.Text = "Windows 11";
                    else if (fn.Contains("win10") || fn.Contains("windows10") || fn.Contains("windows_10"))
                        IsoOsText.Text = "Windows 10";
                    else
                        IsoOsText.Text = "Windows (auto-detect)";
                    if (fn.Contains("x64") || fn.Contains("amd64"))
                        IsoArchText.Text = "Architecture: x64";
                    else if (fn.Contains("arm64"))
                        IsoArchText.Text = "Architecture: ARM64";
                    else
                        IsoArchText.Text = "Architecture: x64 (assumed)";
                    IsoInfoPanel.Visibility = Visibility.Visible;
                }
            }
            catch { }
        }
    }

    // ══════════════════════════════════════════════
    // STEP 5: BLOATWARE
    // ══════════════════════════════════════════════

    private void PopulateBloatware()
    {
        _bloatware.Clear();
        _bloatware.AddRange(new[]
        {
            new BloatwareItem("Microsoft.YourPhone", "Phone Link", "Communication", true),
            new BloatwareItem("Microsoft.People", "People", "Communication", true),
            new BloatwareItem("microsoft.windowscommunicationsapps", "Mail & Calendar", "Communication", false),
            new BloatwareItem("Microsoft.SkypeApp", "Skype", "Communication", true),
            new BloatwareItem("Microsoft.Xbox.TCUI", "Xbox TCUI", "Gaming", true),
            new BloatwareItem("Microsoft.XboxApp", "Xbox Companion", "Gaming", true),
            new BloatwareItem("Microsoft.XboxGamingOverlay", "Xbox Game Bar", "Gaming", false),
            new BloatwareItem("Microsoft.GamingApp", "Xbox App", "Gaming", true),
            new BloatwareItem("Microsoft.ZuneMusic", "Groove Music", "Entertainment", true),
            new BloatwareItem("Microsoft.ZuneVideo", "Movies & TV", "Entertainment", true),
            new BloatwareItem("Microsoft.BingNews", "News", "Entertainment", true),
            new BloatwareItem("Microsoft.BingWeather", "Weather", "Entertainment", true),
            new BloatwareItem("Microsoft.MicrosoftSolitaireCollection", "Solitaire", "Entertainment", true),
            new BloatwareItem("Microsoft.MicrosoftOfficeHub", "Office Hub", "Productivity", true),
            new BloatwareItem("Clipchamp.Clipchamp", "Clipchamp", "Productivity", true),
            new BloatwareItem("Microsoft.PowerAutomateDesktop", "Power Automate", "Productivity", true),
            new BloatwareItem("Microsoft.GetHelp", "Get Help", "System", true),
            new BloatwareItem("Microsoft.Getstarted", "Tips", "System", true),
            new BloatwareItem("Microsoft.WindowsFeedbackHub", "Feedback Hub", "System", true),
            new BloatwareItem("Microsoft.WindowsMaps", "Maps", "System", true),
            new BloatwareItem("Microsoft.MicrosoftEdge.Stable", "Microsoft Edge", "Browser", false),
            new BloatwareItem("Microsoft.OneDrive", "OneDrive", "Cloud", true),
            new BloatwareItem("SpotifyAB.SpotifyMusic", "Spotify", "Third-Party", true),
            new BloatwareItem("Disney.37853FC22B2CE", "Disney+", "Third-Party", true),
            new BloatwareItem("5A894077.McAfeeSecurity", "McAfee", "Third-Party", true),
            new BloatwareItem("Facebook.Facebook", "Facebook", "Third-Party", true),
            new BloatwareItem("Facebook.Instagram", "Instagram", "Third-Party", true),
            new BloatwareItem("BytedancePte.Ltd.TikTok", "TikTok", "Third-Party", true),
            new BloatwareItem("king.com.CandyCrushSaga", "Candy Crush", "Third-Party", true),
            new BloatwareItem("king.com.CandyCrushSodaSaga", "Candy Crush Soda", "Third-Party", true),
        });
    }

    private void BuildBloatwareUI()
    {
        try
        {
            BloatwareList.Children.Clear();
            foreach (var cat in _bloatware.Select(b => b.Category).Distinct().OrderBy(c => c))
            {
                var items = _bloatware.Where(b => b.Category == cat).ToList();
                var hdr = new TextBlock
                {
                    Text = $"{cat} ({items.Count(b => b.Selected)}/{items.Count})",
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    FontSize = 13,
                    Margin = new Thickness(0, 8, 0, 4)
                };
                BloatwareList.Children.Add(hdr);

                foreach (var item in items)
                {
                    var cb = new CheckBox
                    {
                        Content = $"{item.DisplayName} ({item.PackageName})",
                        IsChecked = item.Selected,
                        FontSize = 12,
                        Margin = new Thickness(16, 0, 0, 0)
                    };
                    var ci = item;
                    var ch = hdr;
                    var cc = cat;
                    cb.Checked += (ss, ee) =>
                    {
                        ci.Selected = true;
                        ch.Text = $"{cc} ({_bloatware.Count(b => b.Category == cc && b.Selected)}/{_bloatware.Count(b => b.Category == cc)})";
                    };
                    cb.Unchecked += (ss, ee) =>
                    {
                        ci.Selected = false;
                        ch.Text = $"{cc} ({_bloatware.Count(b => b.Category == cc && b.Selected)}/{_bloatware.Count(b => b.Category == cc)})";
                    };
                    BloatwareList.Children.Add(cb);
                }
            }
        }
        catch (Exception ex) { Debug.WriteLine($"BloatwareUI: {ex}"); }
    }

    private void SelectAllBloat_Click(object s, RoutedEventArgs e)
    {
        foreach (var b in _bloatware) b.Selected = true;
        BuildBloatwareUI();
    }

    private void DeselectAllBloat_Click(object s, RoutedEventArgs e)
    {
        foreach (var b in _bloatware) b.Selected = false;
        BuildBloatwareUI();
    }

    // ══════════════════════════════════════════════
    // STEP 6: EXE BUNDLER
    // ══════════════════════════════════════════════

    private async void AddExe_Click(object s, RoutedEventArgs e)
    {
        try
        {
            var exeHwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
            if (IsAdmin())
            {
                var selected = ShowWin32OpenDialog("Select Installer",
                    "Installers (*.exe;*.msi)\0*.exe;*.msi\0All Files\0*.*\0", exeHwnd);
                if (!string.IsNullOrEmpty(selected) && !_exePaths.Contains(selected))
                    _exePaths.Add(selected);
            }
            else
            {
                var picker = new FileOpenPicker { SuggestedStartLocation = PickerLocationId.Downloads };
                picker.FileTypeFilter.Add(".exe");
                picker.FileTypeFilter.Add(".msi");
                WinRT.Interop.InitializeWithWindow.Initialize(picker, exeHwnd);
                var files = await picker.PickMultipleFilesAsync();
                if (files is null) return;
                foreach (var f in files)
                {
                    if (!_exePaths.Contains(f.Path))
                        _exePaths.Add(f.Path);
                }
            }
            BuildExeUI();
        }
        catch (Exception ex) { Debug.WriteLine($"AddExe: {ex}"); }
    }
    private void ClearExe_Click(object s, RoutedEventArgs e)
    {
        _exePaths.Clear();
        BuildExeUI();
    }

    private void BuildExeUI()
    {
        ExeFilesList.Children.Clear();
        foreach (var path in _exePaths)
        {
            var fi = new FileInfo(path);
            var mb = fi.Exists ? fi.Length / (1024.0 * 1024.0) : 0;
            var sp = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            sp.Children.Add(new FontIcon
            {
                Glyph = fi.Exists ? "\uE73E" : "\uE783",
                FontSize = 12,
                Foreground = new SolidColorBrush(fi.Exists
                    ? Color.FromArgb(255, 6, 214, 160)
                    : Color.FromArgb(255, 239, 68, 68))
            });
            sp.Children.Add(new TextBlock
            {
                Text = $"{fi.Name} ({mb:F1} MB)",
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center
            });
            var removeBtn = new HyperlinkButton { Content = "Remove", FontSize = 11 };
            var capturedPath = path;
            removeBtn.Click += (ss, ee) => { _exePaths.Remove(capturedPath); BuildExeUI(); };
            sp.Children.Add(removeBtn);
            ExeFilesList.Children.Add(sp);
        }

        var totalMb = _exePaths.Where(File.Exists).Sum(p => new FileInfo(p).Length) / (1024.0 * 1024.0);
        ExeCountText.Text = _exePaths.Count == 0
            ? "No files added"
            : $"{_exePaths.Count} file(s) - {totalMb:F1} MB";
    }

    // ══════════════════════════════════════════════
    // STEP 7: DRIVER SCAN
    // ══════════════════════════════════════════════

    private async void ScanDrivers_Click(object s, RoutedEventArgs e)
    {
        try
        {
            if (!IsAdmin())
            {
                await ShowDialog("Administrator Required",
                    "Right-click AuraCore Pro and select 'Run as administrator' to scan drivers.");
                return;
            }

            ScanDriversBtn.IsEnabled = false;
            DriverScanProgress.IsActive = true;
            DriverScanProgress.Visibility = Visibility.Visible;
            DriverScanStatus.Text = "Scanning...";
            _drivers.Clear();
            DriverList.Children.Clear();

            var script = "$r=@();" +
                "try{$p=pnputil /enum-drivers;$c=@{};" +
                "foreach($l in $p -split \"`n\"){$l=$l.Trim();" +
                "if($l-match'^Published Name\\s*:\\s*(.+)'){$c=@{};$c['D']=$Matches[1].Trim()}" +
                "elseif($l-match'^Provider Name\\s*:\\s*(.+)'){$c['P']=$Matches[1].Trim()}" +
                "elseif($l-match'^Class Name\\s*:\\s*(.+)'){$c['C']=$Matches[1].Trim()}" +
                "elseif($l-match'^Driver Version\\s*:\\s*(.+)'){$c['V']=$Matches[1].Trim();" +
                "if($c['P']-and$c['P']-ne'Microsoft'-and$c['C']){" +
                "$r+=[PSCustomObject]@{ClassName=$c['C'];ProviderName=$c['P'];Driver=$c['D'];Version=$c['V']};$c=@{}}}" +
                "}}catch{};" +
                "try{Get-CimInstance Win32_VideoController -EA SilentlyContinue|" +
                "?{$_.DriverVersion-and$_.Name-notmatch'Basic'}|" +
                "%{$r+=[PSCustomObject]@{ClassName='Display (GPU)';ProviderName=$_.AdapterCompatibility;" +
                "Driver='';Version=$_.DriverVersion;DeviceName=$_.Name}}}catch{};" +
                "$r|Sort-Object ClassName,ProviderName -Unique|ConvertTo-Json -Depth 2 -Compress";

            var output = (await RunPowershell(script)).Trim();

            if (string.IsNullOrEmpty(output) || output == "null")
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    DriverScanStatus.Text = "No drivers found.";
                    ScanDriversBtn.IsEnabled = true;
                    DriverScanProgress.IsActive = false;
                    DriverScanProgress.Visibility = Visibility.Collapsed;
                });
                return;
            }

            if (output.StartsWith("{")) output = "[" + output + "]";

            if (output.StartsWith("["))
            {
                var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var items = JsonSerializer.Deserialize<List<DriverJsonItem>>(output, opts);
                if (items != null)
                {
                    foreach (var item in items)
                    {
                        var prov = item.ProviderName ?? "Unknown";
                        var ver = item.Version ?? "";
                        if (_drivers.Any(d => d.ProviderName == prov && d.Version == ver))
                            continue;

                        _drivers.Add(new DriverItem
                        {
                            ClassName = item.ClassName ?? "Unknown",
                            ProviderName = prov,
                            DriverFile = item.Driver ?? "",
                            Version = ver,
                            DeviceName = item.DeviceName ?? "",
                            Selected = true
                        });
                    }
                }
            }

            DispatcherQueue.TryEnqueue(() =>
            {
                foreach (var cat in _drivers.Select(d => d.ClassName).Distinct().OrderBy(c => c))
                {
                    DriverList.Children.Add(new TextBlock
                    {
                        Text = $"{cat} ({_drivers.Count(d => d.ClassName == cat)})",
                        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                        FontSize = 13,
                        Margin = new Thickness(0, 6, 0, 2)
                    });

                    foreach (var drv in _drivers.Where(d => d.ClassName == cat))
                    {
                        var label = !string.IsNullOrEmpty(drv.DeviceName)
                            ? $"{drv.DeviceName} ({drv.ProviderName})"
                            : $"{drv.ProviderName} v{drv.Version}";

                        var cb = new CheckBox
                        {
                            Content = label,
                            IsChecked = true,
                            FontSize = 12,
                            Margin = new Thickness(16, 0, 0, 0)
                        };
                        var cd = drv;
                        cb.Checked += (ss, ev) => { cd.Selected = true; };
                        cb.Unchecked += (ss, ev) => { cd.Selected = false; };
                        DriverList.Children.Add(cb);
                    }
                }

                DriverScanStatus.Text = $"Found {_drivers.Count} drivers";
                ScanDriversBtn.IsEnabled = true;
                DriverScanProgress.IsActive = false;
                DriverScanProgress.Visibility = Visibility.Collapsed;
            });
        }
        catch (Exception ex)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                DriverScanStatus.Text = $"Error: {ex.Message}";
                ScanDriversBtn.IsEnabled = true;
                DriverScanProgress.IsActive = false;
                DriverScanProgress.Visibility = Visibility.Collapsed;
            });
        }
    }

    // ══════════════════════════════════════════════
    // STEP 8: WINGET
    // ══════════════════════════════════════════════

    private void PopulateWingetApps()
    {
        _wingetApps.Clear();
        _wingetApps.AddRange(new[]
        {
            new WingetApp("Google.Chrome", "Chrome", "Browsers", false),
            new WingetApp("Mozilla.Firefox", "Firefox", "Browsers", false),
            new WingetApp("Brave.Brave", "Brave", "Browsers", false),
            new WingetApp("VideoLAN.VLC", "VLC", "Media", false),
            new WingetApp("Discord.Discord", "Discord", "Communication", true),
            new WingetApp("Telegram.TelegramDesktop", "Telegram", "Communication", false),
            new WingetApp("Valve.Steam", "Steam", "Gaming", true),
            new WingetApp("EpicGames.EpicGamesLauncher", "Epic Games", "Gaming", false),
            new WingetApp("Microsoft.VisualStudioCode", "VS Code", "Development", false),
            new WingetApp("Git.Git", "Git", "Development", false),
            new WingetApp("Python.Python.3.12", "Python", "Development", false),
            new WingetApp("Notepad++.Notepad++", "Notepad++", "Utilities", false),
            new WingetApp("7zip.7zip", "7-Zip", "Utilities", true),
            new WingetApp("qBittorrent.qBittorrent", "qBittorrent", "Utilities", false),
            new WingetApp("CPUID.CPU-Z", "CPU-Z", "System", false),
            new WingetApp("REALiX.HWiNFO", "HWiNFO", "System", false),
        });
    }

    private void BuildWingetUI()
    {
        try
        {
            WingetAppList.Children.Clear();
            foreach (var cat in _wingetApps.Select(a => a.Category).Distinct().OrderBy(c => c))
            {
                WingetAppList.Children.Add(new TextBlock
                {
                    Text = cat,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    FontSize = 13,
                    Margin = new Thickness(0, 8, 0, 2)
                });

                foreach (var app in _wingetApps.Where(a => a.Category == cat))
                {
                    var cb = new CheckBox
                    {
                        Content = $"{app.DisplayName} ({app.WingetId})",
                        IsChecked = app.Selected,
                        FontSize = 12,
                        Margin = new Thickness(16, 0, 0, 0)
                    };
                    var ca = app;
                    cb.Checked += (ss, ee) => { ca.Selected = true; };
                    cb.Unchecked += (ss, ee) => { ca.Selected = false; };
                    WingetAppList.Children.Add(cb);
                }
            }
        }
        catch (Exception ex) { Debug.WriteLine($"WingetUI: {ex}"); }
    }

    private void AddCustomWinget_Click(object s, RoutedEventArgs e)
    {
        var id = CustomWingetBox.Text.Trim();
        if (string.IsNullOrEmpty(id) || _customWingetIds.Contains(id)) return;

        _customWingetIds.Add(id);
        CustomWingetBox.Text = "";

        var sp = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        sp.Children.Add(new TextBlock { Text = id, FontSize = 12, VerticalAlignment = VerticalAlignment.Center });
        var btn = new HyperlinkButton { Content = "Remove", FontSize = 11 };
        var capturedId = id;
        btn.Click += (ss, ev) => { _customWingetIds.Remove(capturedId); CustomWingetList.Children.Remove(sp); };
        sp.Children.Add(btn);
        CustomWingetList.Children.Add(sp);
    }

    // ══════════════════════════════════════════════
    // STEP 11: OUTPUT
    // ══════════════════════════════════════════════

    private void OutputMode_Changed(object s, RoutedEventArgs e)
    {
        if (UsbPanel is not null)
        {
            UsbPanel.Visibility = OutputUsbWrite?.IsChecked == true
                ? Visibility.Visible : Visibility.Collapsed;
            if (OutputUsbWrite?.IsChecked == true) RefreshUsbDrives();
        }
        UpdateSummary();
    }

    private void RefreshUsb_Click(object s, RoutedEventArgs e) { RefreshUsbDrives(); }

    private void RefreshUsbDrives()
    {
        try
        {
            UsbDriveCombo.Items.Clear();
            foreach (var d in DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Removable && d.IsReady))
            {
                var label = string.IsNullOrEmpty(d.VolumeLabel) ? "USB" : d.VolumeLabel;
                var sizeGb = d.TotalSize / (1024.0 * 1024 * 1024);
                UsbDriveCombo.Items.Add(new ComboBoxItem
                {
                    Content = $"{d.Name} - {label} ({sizeGb:F1} GB)",
                    Tag = d.Name
                });
            }
            if (UsbDriveCombo.Items.Count > 0)
                UsbDriveCombo.SelectedIndex = 0;
        }
        catch { }
    }

    // ══════════════════════════════════════════════
    // SUMMARY
    // ══════════════════════════════════════════════

    private void UpdateSummary()
    {
        try
        {
            if (SummaryText is null) return;
            var sb = new StringBuilder();
            sb.AppendLine($"ISO: {(_isoPath != null ? Path.GetFileName(_isoPath) : "Not selected")}");
            sb.AppendLine($"Account: {(LocalAccountRadio?.IsChecked == true ? $"Local ({UsernameBox?.Text ?? "Admin"})" : "Microsoft")}");
            sb.AppendLine($"Bloatware: {_bloatware.Count(b => b.Selected)} apps to remove");
            sb.AppendLine($"EXE bundled: {_exePaths.Count}");
            sb.AppendLine($"Drivers: {_drivers.Count(d => d.Selected)}");
            sb.AppendLine($"Winget apps: {_wingetApps.Count(a => a.Selected) + _customWingetIds.Count}");
            sb.AppendLine($"WiFi: {(EnableWifiCheck?.IsChecked == true ? WifiSsidBox?.Text ?? "Not set" : "Disabled")}");
            sb.AppendLine($"Output: {(OutputUsbWrite?.IsChecked == true ? "USB Write" : "Files Only")}");
            SummaryText.Text = sb.ToString();

            var min = 1;
            if (OutputUsbWrite?.IsChecked == true) min += 8;
            if (_drivers.Count(d => d.Selected) > 0) min += 3;
            EstimatedTimeText.Text = $"Estimated: ~{min} min";
        }
        catch { }
    }

    // ══════════════════════════════════════════════
    // IMPORT / EXPORT
    // ══════════════════════════════════════════════

    private async void ExportConfig_Click(object s, RoutedEventArgs e)
    {
        try
        {
            var json = JsonSerializer.Serialize(GatherConfig(), new JsonSerializerOptions { WriteIndented = true });
            var picker = new FileSavePicker
            {
                SuggestedStartLocation = PickerLocationId.Desktop,
                SuggestedFileName = "AuraCore-ISO-Config"
            };
            picker.FileTypeChoices.Add("JSON", new List<string> { ".json" });
            WinRT.Interop.InitializeWithWindow.Initialize(picker,
                WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow));

            var file = await picker.PickSaveFileAsync();
            if (file is null) return;
            await File.WriteAllTextAsync(file.Path, json);
            await ShowDialog(S._("iso.exported"), $"Saved to:\n{file.Path}");
        }
        catch (Exception ex) { await ShowDialog("Error", ex.Message); }
    }

    private async void ImportConfig_Click(object s, RoutedEventArgs e)
    {
        try
        {
            var picker = new FileOpenPicker { SuggestedStartLocation = PickerLocationId.Desktop };
            picker.FileTypeFilter.Add(".json");
            WinRT.Interop.InitializeWithWindow.Initialize(picker,
                WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow));

            var file = await picker.PickSingleFileAsync();
            if (file is null) return;

            var cfg = JsonSerializer.Deserialize<IsoBuilderConfig>(await File.ReadAllTextAsync(file.Path));
            if (cfg is null) return;
            ApplyConfig(cfg);
            await ShowDialog(S._("iso.imported"), S._("iso.configLoaded"));
        }
        catch (Exception ex) { await ShowDialog("Error", ex.Message); }
    }

    private void ApplyConfig(IsoBuilderConfig cfg)
    {
        try
        {
            _isoPath = cfg.IsoPath;
            IsoPathText.Text = string.IsNullOrEmpty(cfg.IsoPath) ? "No file" : cfg.IsoPath;
            LocalAccountRadio.IsChecked = cfg.UseLocalAccount;
            MsAccountRadio.IsChecked = !cfg.UseLocalAccount;
            UsernameBox.Text = cfg.Username;
            PasswordBox.Password = cfg.Password;
            AutoLoginCheck.IsChecked = cfg.AutoLogin;
            SkipOobeCheck.IsChecked = cfg.SkipOobe;
            SkipEulaCheck.IsChecked = cfg.SkipEula;
            DisableTelemetryCheck.IsChecked = cfg.DisableTelemetry;
            DisableCortanaCheck.IsChecked = cfg.DisableCortana;
            DisableOneDriveCheck.IsChecked = cfg.DisableOneDrive;
            ComputerNameBox.Text = cfg.ComputerName;
            BypassTpmCheck.IsChecked = cfg.BypassTpm;
            BypassSecureBootCheck.IsChecked = cfg.BypassSecureBoot;
            BypassRamCheck.IsChecked = cfg.BypassRam;
            BypassStorageCheck.IsChecked = cfg.BypassStorage;
            BypassCpuCheck.IsChecked = cfg.BypassCpu;
            EnableWifiCheck.IsChecked = cfg.WifiEnabled;
            WifiSsidBox.Text = cfg.WifiSsid;
            WifiPasswordBox.Password = cfg.WifiPassword;

            foreach (var b in _bloatware)
                b.Selected = cfg.BloatwareToRemove.Contains(b.PackageName);
            BuildBloatwareUI();

            _exePaths.Clear();
            _exePaths.AddRange(cfg.ExePaths);
            BuildExeUI();

            SilentInstallCheck.IsChecked = cfg.SilentInstall;
            IncludeDriversCheck.IsChecked = cfg.IncludeDrivers;
            PresetDefaultCheck.IsChecked = cfg.PresetDefault;
            PresetGamingCheck.IsChecked = cfg.PresetGaming;
            PresetPrivacyCheck.IsChecked = cfg.PresetPrivacy;
            PresetDevCheck.IsChecked = cfg.PresetDev;
            PresetOfficeCheck.IsChecked = cfg.PresetOffice;

            foreach (var a in _wingetApps)
                a.Selected = cfg.WingetApps.Contains(a.WingetId);
            BuildWingetUI();

            ShowStep(0);
            UpdateSummary();
        }
        catch (Exception ex) { Debug.WriteLine($"ApplyConfig: {ex}"); }
    }

    // ══════════════════════════════════════════════
    // BUILD
    // ══════════════════════════════════════════════

    private async void Build_Click(object s, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_isoPath) || !File.Exists(_isoPath))
        {
            await ShowDialog("Error", "Select a valid ISO file first (Step 1).");
            return;
        }
        if (DisclaimerCheck?.IsChecked != true)
        {
            await ShowDialog("Disclaimer", "Accept the disclaimer first.");
            return;
        }
        if (OutputUsbWrite?.IsChecked == true && !IsAdmin())
        {
            await ShowDialog("Admin Required", "Restart AuraCore Pro as Administrator for USB write.");
            return;
        }

        var config = GatherConfig();

        WizardGrid.Visibility = Visibility.Collapsed;
        TerminalOverlay.Visibility = Visibility.Visible;
        TerminalCloseBtn.Visibility = Visibility.Collapsed;
        _terminalLog.Clear();
        TerminalOutput.Text = "";
        _buildStartTime = DateTime.Now;
        TerminalProgress.Value = 0;

        try
        {
            if (OutputUnattendOnly?.IsChecked == true)
            {
                string? folderPath = null;
                var buildHwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);

                if (IsAdmin())
                {
                    folderPath = ShowWin32FolderDialog("Select output folder", buildHwnd);
                }
                else
                {
                    var picker = new FolderPicker { SuggestedStartLocation = PickerLocationId.Desktop };
                    picker.FileTypeFilter.Add("*");
                    WinRT.Interop.InitializeWithWindow.Initialize(picker, buildHwnd);
                    var folder = await picker.PickSingleFolderAsync();
                    folderPath = folder?.Path;
                }

                if (string.IsNullOrEmpty(folderPath))
                {
                    TerminalOverlay.Visibility = Visibility.Collapsed;
                    WizardGrid.Visibility = Visibility.Visible;
                    return;
                }
                await RunBuildSteps(config, folderPath, null);
            }
            else if (OutputUsbWrite?.IsChecked == true)
            {
                if (UsbDriveCombo.SelectedItem is not ComboBoxItem sel)
                {
                    await ShowDialog("Error", "Select USB.");
                    TerminalOverlay.Visibility = Visibility.Collapsed;
                    WizardGrid.Visibility = Visibility.Visible;
                    return;
                }
                var drv = sel.Tag?.ToString() ?? "";
                var dlg = new ContentDialog
                {
                    Title = "FINAL WARNING",
                    Content = $"ALL DATA on {drv} PERMANENTLY ERASED.\nCannot undo. Sure?",
                    PrimaryButtonText = "Erase & Write",
                    CloseButtonText = "Cancel",
                    DefaultButton = ContentDialogButton.Close,
                    XamlRoot = Content.XamlRoot
                };
                if (await dlg.ShowAsync() != ContentDialogResult.Primary)
                {
                    TerminalOverlay.Visibility = Visibility.Collapsed;
                    WizardGrid.Visibility = Visibility.Visible;
                    return;
                }
                await RunBuildSteps(config, null, drv);
            }
        }
        catch (Exception ex)
        {
            LogTerminal($"\n[FATAL] {ex.Message}");
            TerminalStepText.Text = "BUILD FAILED";
        }

        TerminalCloseBtn.Visibility = Visibility.Visible;
    }

    private async Task RunBuildSteps(IsoBuilderConfig config, string? folder, string? usb)
    {
        var total = 3;
        if (config.ExePaths.Count > 0) total++;
        if (config.IncludeDrivers && config.Drivers.Count > 0) total++;
        if (usb != null) total += 3;
        var step = 0;

        void Prog(string msg)
        {
            step++;
            var pct = (int)((step / (double)total) * 100);
            var elapsed = (DateTime.Now - _buildStartTime).TotalSeconds;
            var remain = step > 0 ? (elapsed / step) * (total - step) : 0;
            DispatcherQueue.TryEnqueue(() =>
            {
                TerminalProgress.Value = pct;
                TerminalStepText.Text = $"Step {step}/{total}";
                TerminalTimeText.Text = $"Elapsed: {elapsed:F0}s | Remaining: ~{remain:F0}s";
                LogTerminal(msg);
            });
        }

        await Task.Run(async () =>
        {
            string target;

            if (usb != null)
            {
                Prog($"[{step + 1}] Formatting USB {usb}...");
                var letter = usb.TrimEnd('\\', ':');
                await RunPowershell(
                    $"$d=Get-Partition -DriveLetter '{letter}'|Get-Disk;" +
                    $"Clear-Disk -Number $d.Number -RemoveData -RemoveOEM -Confirm:$false;" +
                    $"$p=New-Partition -DiskNumber $d.Number -UseMaximumSize -AssignDriveLetter;" +
                    $"Format-Volume -Partition $p -FileSystem NTFS -NewFileSystemLabel 'WIN_USB' -Confirm:$false");
                DispatcherQueue.TryEnqueue(() => LogTerminal("  USB formatted"));

                Prog($"[{step + 1}] Mounting ISO...");
                var mountScript =
                    $"try{{Dismount-DiskImage -ImagePath '{_isoPath}' -EA SilentlyContinue}}catch{{}};" +
                    $"Start-Sleep 1;" +
                    $"(Mount-DiskImage -ImagePath '{_isoPath}' -PassThru|Get-Volume).DriveLetter";
                var ml = (await RunPowershell(mountScript)).Trim();
                DispatcherQueue.TryEnqueue(() => LogTerminal($"  Mounted at {ml}:\\"));

                Prog($"[{step + 1}] Copying files (5-10 min)...");
                await RunPowershell($"Copy-Item -Path '{ml}:\\*' -Destination '{usb}' -Recurse -Force");
                DispatcherQueue.TryEnqueue(() => LogTerminal("  Files copied"));

                target = usb;
            }
            else
            {
                target = folder!;
            }

            Prog($"[{step + 1}] Generating autounattend.xml...");
            var xml = IsoBuilderService.GenerateUnattendXml(config);
            await File.WriteAllTextAsync(Path.Combine(target, "autounattend.xml"), xml, Encoding.UTF8);
            DispatcherQueue.TryEnqueue(() => LogTerminal($"  autounattend.xml ({xml.Length:N0} bytes)"));

            Prog($"[{step + 1}] Generating PostInstall scripts...");
            var script = IsoBuilderService.GeneratePostInstallScript(config);
            await File.WriteAllTextAsync(Path.Combine(target, "PostInstall.ps1"), script, Encoding.UTF8);
            await File.WriteAllTextAsync(Path.Combine(target, "RunPostInstall.bat"),
                "@echo off\r\necho AuraCore Pro Post-Install\r\n" +
                "powershell.exe -ExecutionPolicy Bypass -File \"%~dp0PostInstall.ps1\"\r\npause",
                Encoding.UTF8);
            DispatcherQueue.TryEnqueue(() => LogTerminal($"  PostInstall.ps1 + bat"));

            if (config.ExePaths.Count > 0)
            {
                Prog($"[{step + 1}] Bundling {config.ExePaths.Count} installer(s)...");
                var exeDir = Path.Combine(target, "AuraCoreInstallers");
                Directory.CreateDirectory(exeDir);
                foreach (var p in config.ExePaths)
                {
                    if (File.Exists(p))
                    {
                        File.Copy(p, Path.Combine(exeDir, Path.GetFileName(p)), true);
                        var fn = Path.GetFileName(p);
                        DispatcherQueue.TryEnqueue(() => LogTerminal($"  + {fn}"));
                    }
                }
            }

            if (config.IncludeDrivers && config.Drivers.Count > 0)
            {
                Prog($"[{step + 1}] Exporting drivers...");
                var drvDir = Path.Combine(target, "AuraCoreDrivers");
                Directory.CreateDirectory(drvDir);
                await RunPowershell($"Export-WindowsDriver -Online -Destination '{drvDir}'|Out-Null");
                DispatcherQueue.TryEnqueue(() => LogTerminal("  Drivers exported"));
            }

            if (usb != null)
            {
                DispatcherQueue.TryEnqueue(() => LogTerminal("  Unmounting..."));
                try { await RunPowershell($"Dismount-DiskImage -ImagePath '{_isoPath}'"); } catch { }
            }

            var totalElapsed = (DateTime.Now - _buildStartTime).TotalSeconds;
            DispatcherQueue.TryEnqueue(() =>
            {
                LogTerminal($"\n=== BUILD COMPLETE ===");
                LogTerminal($"  Time: {totalElapsed:F1}s");
                LogTerminal($"  Output: {target}");
                LogTerminal($"  Bloatware: {config.BloatwareToRemove.Count}");
                LogTerminal($"  EXE: {config.ExePaths.Count}");
                LogTerminal($"  Drivers: {config.Drivers.Count}");
                LogTerminal($"  Winget: {config.WingetApps.Count}");
                LogTerminal($"\n  Powered by AuraCore Pro");
                TerminalProgress.Value = 100;
                TerminalStepText.Text = "BUILD SUCCESSFUL";
                TerminalTimeText.Text = $"Completed in {totalElapsed:F1}s";
            });
        });
    }

    private void LogTerminal(string text)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            _terminalLog.AppendLine(text);
            TerminalOutput.Text = _terminalLog.ToString();
            TerminalScroll?.ChangeView(null, TerminalScroll.ScrollableHeight, null);
        });
    }

    private void TerminalClose_Click(object s, RoutedEventArgs e)
    {
        TerminalOverlay.Visibility = Visibility.Collapsed;
        WizardGrid.Visibility = Visibility.Visible;
    }

    private async void Preview_Click(object s, RoutedEventArgs e)
    {
        try
        {
            var xml = IsoBuilderService.GenerateUnattendXml(GatherConfig());
            var dialog = new ContentDialog
            {
                Title = "Unattend.xml Preview",
                Content = new ScrollViewer
                {
                    Content = new TextBlock
                    {
                        Text = xml,
                        FontFamily = new FontFamily("Consolas"),
                        FontSize = 11,
                        IsTextSelectionEnabled = true,
                        TextWrapping = TextWrapping.Wrap
                    },
                    MaxHeight = 500
                },
                CloseButtonText = "Close",
                XamlRoot = Content.XamlRoot
            };
            await dialog.ShowAsync();
        }
        catch { }
    }

    // ══════════════════════════════════════════════
    // CONFIG
    // ══════════════════════════════════════════════

    private IsoBuilderConfig GatherConfig()
    {
        var cfg = new IsoBuilderConfig
        {
            IsoPath = _isoPath ?? "",
            UseLocalAccount = LocalAccountRadio.IsChecked == true,
            Username = UsernameBox.Text.Trim(),
            Password = PasswordBox.Password,
            AutoLogin = AutoLoginCheck.IsChecked == true,
            SkipOobe = SkipOobeCheck.IsChecked == true,
            SkipEula = SkipEulaCheck.IsChecked == true,
            DisableTelemetry = DisableTelemetryCheck.IsChecked == true,
            DisableCortana = DisableCortanaCheck.IsChecked == true,
            DisableOneDrive = DisableOneDriveCheck.IsChecked == true,
            ComputerName = ComputerNameBox.Text.Trim(),
            Language = (LanguageCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "en-US",
            Timezone = (TimezoneCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "UTC",
            BypassTpm = BypassTpmCheck.IsChecked == true,
            BypassSecureBoot = BypassSecureBootCheck.IsChecked == true,
            BypassRam = BypassRamCheck.IsChecked == true,
            BypassStorage = BypassStorageCheck.IsChecked == true,
            BypassCpu = BypassCpuCheck.IsChecked == true,
            WifiEnabled = EnableWifiCheck.IsChecked == true,
            WifiSsid = WifiSsidBox.Text.Trim(),
            WifiPassword = WifiPasswordBox.Password,
            WifiSecurity = (WifiSecurityCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "WPA2PSK",
            BloatwareToRemove = _bloatware.Where(b => b.Selected).Select(b => b.PackageName).ToList(),
            ExePaths = new List<string>(_exePaths.Where(File.Exists)),
            SilentInstall = SilentInstallCheck.IsChecked == true,
            IncludeDrivers = IncludeDriversCheck.IsChecked == true,
            Drivers = _drivers.Where(d => d.Selected).ToList(),
            WingetApps = _wingetApps.Where(a => a.Selected).Select(a => a.WingetId).Concat(_customWingetIds).ToList(),
            PresetDefault = PresetDefaultCheck.IsChecked == true,
            PresetGaming = PresetGamingCheck.IsChecked == true,
            PresetPrivacy = PresetPrivacyCheck.IsChecked == true,
            PresetDev = PresetDevCheck.IsChecked == true,
            PresetOffice = PresetOfficeCheck.IsChecked == true,
        };

        if (InstallWingetCheck.IsChecked == true) cfg.PostInstallActions.Add("install_winget");
        if (EnableDarkModeCheck.IsChecked == true) cfg.PostInstallActions.Add("dark_mode");
        if (ShowFileExtCheck.IsChecked == true) cfg.PostInstallActions.Add("file_extensions");
        if (ShowHiddenFilesCheck.IsChecked == true) cfg.PostInstallActions.Add("hidden_files");
        if (DisableWebSearchCheck.IsChecked == true) cfg.PostInstallActions.Add("disable_web_search");
        if (ClassicRightClickCheck.IsChecked == true) cfg.PostInstallActions.Add("classic_context_menu");
        if (DisableWidgetsCheck.IsChecked == true) cfg.PostInstallActions.Add("disable_widgets");
        if (DisableCopilotCheck.IsChecked == true) cfg.PostInstallActions.Add("disable_copilot");
        if (TaskbarLeftCheck.IsChecked == true) cfg.PostInstallActions.Add("taskbar_left");

        return cfg;
    }

    // ══════════════════════════════════════════════

    private void ApplyIsoLocalization()
    {
        try
        {
            // Step titles array
            _stepTitles = new[] {
                S._("iso.step1"), S._("iso.step2"), S._("iso.step3"), S._("iso.step4"),
                S._("iso.step5"), S._("iso.step6"), S._("iso.step7"), S._("iso.step8"),
                S._("iso.step9"), S._("iso.step10"), S._("iso.step11"), S._("iso.step12")
            };

            // Rebuild stepper with localized names
            BuildStepper();
            ShowStep(_currentStep);

            // Step indicator
            StepIndicator.Text = string.Format(S._("iso.stepOf"), _currentStep + 1, TotalSteps);

            // Sidebar
            if (FindName("TemplateCombo") is ComboBox tc) tc.PlaceholderText = S._("iso.loadTemplate");

            // Navigation buttons
            BackBtn.Content = S._("iso.back");
            NextBtn.Content = S._("iso.next");
            BuildBtn.Content = S._("iso.buildBtn");
            if (FindName("TerminalCloseBtn") is Button tcb) tcb.Content = S._("iso.close");

            // Pro feature banner
            if (FindName("ProFeatureTitle") is TextBlock pft) pft.Text = S._("iso.proFeature");
            if (FindName("ProFeatureDesc") is TextBlock pfd) pfd.Text = S._("iso.proRequired");

            // Step 1
            if (FindName("S1Title") is TextBlock s1t) s1t.Text = S._("iso.s1.title");
            if (FindName("S1Desc") is TextBlock s1d) s1d.Text = S._("iso.s1.desc");

            // Step 2
            if (FindName("S2Title") is TextBlock s2t) s2t.Text = S._("iso.s2.title");
            if (FindName("S2Desc") is TextBlock s2d) s2d.Text = S._("iso.s2.desc");
            UsernameBox.Header = S._("iso.s2.username");
            UsernameBox.PlaceholderText = S._("iso.s2.usernamePlaceholder");
            PasswordBox.Header = S._("iso.s2.password");
            PasswordBox.PlaceholderText = S._("iso.s2.passwordPlaceholder");
            LocalAccountRadio.Content = S._("iso.s2.localAccount");
            MsAccountRadio.Content = S._("iso.s2.msAccount");
            AutoLoginCheck.Content = S._("iso.s2.autoLogin");

            // Step 3
            if (FindName("S3Title") is TextBlock s3t) s3t.Text = S._("iso.s3.title");
            if (FindName("S3Desc") is TextBlock s3d) s3d.Text = S._("iso.s3.desc");
            SkipOobeCheck.Content = S._("iso.s3.skipOobe");
            SkipEulaCheck.Content = S._("iso.s3.skipEula");
            DisableTelemetryCheck.Content = S._("iso.s3.disableTelemetry");
            DisableCortanaCheck.Content = S._("iso.s3.disableCortana");
            DisableOneDriveCheck.Content = S._("iso.s3.disableOneDrive");
            ComputerNameBox.Header = S._("iso.s3.computerName");
            ComputerNameBox.PlaceholderText = S._("iso.s3.computerNamePlaceholder");

            // Step 4
            if (FindName("S4Title") is TextBlock s4t) s4t.Text = S._("iso.s4.title");
            if (FindName("S4Desc") is TextBlock s4d) s4d.Text = S._("iso.s4.desc");
            EnableWifiCheck.Content = S._("iso.s4.enable");
            WifiSsidBox.Header = S._("iso.s4.ssid");
            WifiSsidBox.PlaceholderText = S._("iso.s4.ssidPlaceholder");
            WifiPasswordBox.Header = S._("iso.s4.password");
            if (FindName("S4Warning") is TextBlock s4w) s4w.Text = S._("iso.s4.warning");

            // Step 5
            if (FindName("S5Title") is TextBlock s5t) s5t.Text = S._("iso.s5.title");
            if (FindName("S5Warning") is TextBlock s5w) s5w.Text = S._("iso.s5.warning");
            BypassTpmCheck.Content = S._("iso.s5.tpm");
            BypassSecureBootCheck.Content = S._("iso.s5.secureBoot");
            BypassRamCheck.Content = S._("iso.s5.ram");
            BypassStorageCheck.Content = S._("iso.s5.storage");
            BypassCpuCheck.Content = S._("iso.s5.cpu");

            // Step 6
            if (FindName("S6Title") is TextBlock s6t) s6t.Text = S._("iso.s6.title");
            if (FindName("S6Desc") is TextBlock s6d) s6d.Text = S._("iso.s6.desc");
            if (FindName("S6EdgeWarning") is TextBlock s6e) s6e.Text = S._("iso.s6.edgeWarning");

            // Step 7
            if (FindName("S7Title") is TextBlock s7t) s7t.Text = S._("iso.s7.title");
            if (FindName("S7Desc") is TextBlock s7d) s7d.Text = S._("iso.s7.desc");
            SilentInstallCheck.Content = S._("iso.s7.silentInstall");

            // Step 8
            if (FindName("S8Title") is TextBlock s8t) s8t.Text = S._("iso.s8.title");
            if (FindName("S8Desc") is TextBlock s8d) s8d.Text = S._("iso.s8.desc");
            if (FindName("S8AdminWarning") is TextBlock s8w) s8w.Text = S._("iso.s8.adminWarning");
            ScanDriversBtn.Content = S._("iso.s8.scanBtn");
            IncludeDriversCheck.Content = S._("iso.s8.includeDrivers");

            // Step 9
            if (FindName("S9Title") is TextBlock s9t) s9t.Text = S._("iso.s9.title");
            if (FindName("S9Desc") is TextBlock s9d) s9d.Text = S._("iso.s9.desc");
            CustomWingetBox.PlaceholderText = S._("iso.s9.customPlaceholder");

            // Step 10
            if (FindName("S10Title") is TextBlock s10t) s10t.Text = S._("iso.s10.title");
            if (FindName("S10Desc") is TextBlock s10d) s10d.Text = S._("iso.s10.desc");
            PresetDefaultCheck.Content = S._("iso.s10.default");
            PresetGamingCheck.Content = S._("iso.s10.gaming");
            PresetPrivacyCheck.Content = S._("iso.s10.privacy");
            PresetDevCheck.Content = S._("iso.s10.dev");
            PresetOfficeCheck.Content = S._("iso.s10.office");

            // Step 11
            if (FindName("S11Title") is TextBlock s11t) s11t.Text = S._("iso.s11.title");
            if (FindName("S11Desc") is TextBlock s11d) s11d.Text = S._("iso.s11.desc");
            InstallWingetCheck.Content = S._("iso.s11.winget");
            EnableDarkModeCheck.Content = S._("iso.s11.darkMode");
            ShowFileExtCheck.Content = S._("iso.s11.fileExt");
            ShowHiddenFilesCheck.Content = S._("iso.s11.hiddenFiles");
            DisableWebSearchCheck.Content = S._("iso.s11.webSearch");
            ClassicRightClickCheck.Content = S._("iso.s11.classicMenu");
            DisableWidgetsCheck.Content = S._("iso.s11.widgets");
            DisableCopilotCheck.Content = S._("iso.s11.copilot");
            TaskbarLeftCheck.Content = S._("iso.s11.taskbarLeft");

            // Step 12
            if (FindName("S12Title") is TextBlock s12t) s12t.Text = S._("iso.s12.title");
            if (FindName("S12Desc") is TextBlock s12d) s12d.Text = S._("iso.s12.desc");
            if (FindName("OutputFormatLabel") is TextBlock ofl) ofl.Text = S._("iso.s12.outputFormat");
            OutputUnattendOnly.Content = S._("iso.s12.filesOnly");
            OutputUsbWrite.Content = S._("iso.s12.usbWrite");
            if (FindName("BuildSummaryLabel") is TextBlock bsl) bsl.Text = S._("iso.s12.buildSummary");
            DisclaimerCheck.Content = S._("iso.s12.acceptRisks");
            if (FindName("UsbDestructiveWarning") is TextBlock udw) udw.Text = S._("iso.s12.usbWarning");
            if (FindName("UsbAdminWarning") is TextBlock uaw) uaw.Text = S._("iso.s12.usbAdminWarning");
            if (FindName("BuildingTitle") is TextBlock bt) bt.Text = S._("iso.building");

            // Admin status
            if (IsAdmin())
            {
                AdminStatusText.Text = S._("iso.adminRunning");
            }
            else
            {
                AdminStatusText.Text = S._("iso.adminNotRunning");
            }
        }
        catch { }
    }

    // TEMPLATES
    // -------

    private void TemplateCombo_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (TemplateCombo.SelectedItem is not ComboBoxItem item) return;
        var tag = item.Tag?.ToString();
        if (string.IsNullOrEmpty(tag)) return;

        var template = GetTemplate(tag);
        if (template is null) return;

        ApplyConfig(template);
        TemplateCombo.SelectedIndex = -1; // Reset dropdown
        UpdateSummary();

        _ = ShowDialog(S._("iso.templateApplied"),
            string.Format(S._("iso.templateMsg"), item.Content));
    }

    private static IsoBuilderConfig? GetTemplate(string key) => key switch
    {
        "gaming" => new IsoBuilderConfig
        {
            UseLocalAccount = true,
            Username = "Gamer",
            AutoLogin = true,
            SkipOobe = true,
            SkipEula = true,
            DisableTelemetry = true,
            DisableCortana = true,
            DisableOneDrive = true,
            BypassTpm = true,
            BypassSecureBoot = true,
            BypassRam = true,
            BypassStorage = true,
            BypassCpu = true,
            BloatwareToRemove = new List<string>
            {
                "Microsoft.BingNews", "Microsoft.BingWeather", "Microsoft.GetHelp",
                "Microsoft.Getstarted", "Microsoft.MicrosoftSolitaireCollection",
                "Microsoft.People", "Microsoft.PowerAutomateDesktop",
                "Microsoft.Todos", "Microsoft.WindowsAlarms",
                "Microsoft.WindowsFeedbackHub", "Microsoft.WindowsMaps",
                "Microsoft.WindowsSoundRecorder", "Microsoft.YourPhone",
                "Microsoft.ZuneMusic", "Microsoft.ZuneVideo",
                "Clipchamp.Clipchamp", "Microsoft.MicrosoftOfficeHub",
                "Microsoft.549981C3F5F10", "MicrosoftTeams",
                "Microsoft.OutlookForWindows",
            },
            WingetApps = new List<string>
            {
                "Valve.Steam", "Discord.Discord", "7zip.7zip",
                "Mozilla.Firefox", "VideoLAN.VLC",
            },
            PresetDefault = false,
            PresetGaming = true,
            PresetPrivacy = false,
            PresetDev = false,
            PresetOffice = false,
            PostInstallActions = new List<string>
            {
                "dark_mode", "file_extensions", "disable_web_search",
                "classic_context_menu", "disable_widgets", "disable_copilot",
            },
        },

        "office" => new IsoBuilderConfig
        {
            UseLocalAccount = true,
            Username = "User",
            AutoLogin = true,
            SkipOobe = true,
            SkipEula = true,
            DisableTelemetry = true,
            DisableCortana = true,
            DisableOneDrive = false,
            BloatwareToRemove = new List<string>
            {
                "Microsoft.BingNews", "Microsoft.BingWeather",
                "Microsoft.MicrosoftSolitaireCollection",
                "Microsoft.PowerAutomateDesktop",
                "Microsoft.WindowsFeedbackHub",
                "Microsoft.ZuneVideo", "Clipchamp.Clipchamp",
                "Microsoft.549981C3F5F10", "Microsoft.Xbox.TCUI",
                "Microsoft.XboxGameOverlay", "Microsoft.XboxGamingOverlay",
                "Microsoft.XboxIdentityProvider", "Microsoft.XboxSpeechToTextOverlay",
            },
            WingetApps = new List<string>
            {
                "Mozilla.Firefox", "7zip.7zip",
                "Adobe.Acrobat.Reader.64-bit", "Notepad++.Notepad++",
                "VideoLAN.VLC", "Google.Chrome",
            },
            PresetDefault = true,
            PresetGaming = false,
            PresetPrivacy = false,
            PresetDev = false,
            PresetOffice = true,
            PostInstallActions = new List<string>
            {
                "file_extensions", "disable_web_search",
                "install_winget",
            },
        },

        "privacy" => new IsoBuilderConfig
        {
            UseLocalAccount = true,
            Username = "User",
            AutoLogin = true,
            SkipOobe = true,
            SkipEula = true,
            DisableTelemetry = true,
            DisableCortana = true,
            DisableOneDrive = true,
            BypassTpm = true,
            BypassSecureBoot = true,
            BypassRam = true,
            BypassStorage = true,
            BypassCpu = true,
            BloatwareToRemove = new List<string>
            {
                "Microsoft.BingNews", "Microsoft.BingWeather", "Microsoft.GetHelp",
                "Microsoft.Getstarted", "Microsoft.MicrosoftSolitaireCollection",
                "Microsoft.People", "Microsoft.PowerAutomateDesktop",
                "Microsoft.Todos", "Microsoft.WindowsAlarms",
                "Microsoft.WindowsFeedbackHub", "Microsoft.WindowsMaps",
                "Microsoft.WindowsSoundRecorder", "Microsoft.YourPhone",
                "Microsoft.ZuneMusic", "Microsoft.ZuneVideo",
                "Clipchamp.Clipchamp", "Microsoft.MicrosoftOfficeHub",
                "Microsoft.549981C3F5F10", "MicrosoftTeams",
                "Microsoft.OutlookForWindows",
                "Microsoft.Xbox.TCUI", "Microsoft.XboxGameOverlay",
                "Microsoft.XboxGamingOverlay", "Microsoft.XboxIdentityProvider",
                "Microsoft.XboxSpeechToTextOverlay",
                "Microsoft.Edge",
            },
            WingetApps = new List<string>
            {
                "Mozilla.Firefox", "7zip.7zip", "Notepad++.Notepad++",
            },
            PresetDefault = false,
            PresetGaming = false,
            PresetPrivacy = true,
            PresetDev = false,
            PresetOffice = false,
            PostInstallActions = new List<string>
            {
                "dark_mode", "file_extensions", "hidden_files",
                "disable_web_search", "classic_context_menu",
                "disable_widgets", "disable_copilot", "taskbar_left",
            },
        },

        "developer" => new IsoBuilderConfig
        {
            UseLocalAccount = true,
            Username = "Dev",
            AutoLogin = true,
            SkipOobe = true,
            SkipEula = true,
            DisableTelemetry = true,
            DisableCortana = true,
            DisableOneDrive = false,
            BypassTpm = true,
            BypassSecureBoot = true,
            BypassRam = true,
            BypassStorage = true,
            BypassCpu = true,
            BloatwareToRemove = new List<string>
            {
                "Microsoft.BingNews", "Microsoft.BingWeather",
                "Microsoft.MicrosoftSolitaireCollection",
                "Microsoft.People", "Microsoft.PowerAutomateDesktop",
                "Microsoft.WindowsFeedbackHub", "Microsoft.WindowsMaps",
                "Microsoft.ZuneMusic", "Microsoft.ZuneVideo",
                "Clipchamp.Clipchamp", "Microsoft.549981C3F5F10",
            },
            WingetApps = new List<string>
            {
                "Microsoft.VisualStudioCode", "Git.Git",
                "OpenJS.NodeJS.LTS", "Python.Python.3.12",
                "7zip.7zip", "Mozilla.Firefox",
                "Notepad++.Notepad++", "Microsoft.WindowsTerminal",
                "Docker.DockerDesktop",
            },
            PresetDefault = false,
            PresetGaming = false,
            PresetPrivacy = false,
            PresetDev = true,
            PresetOffice = false,
            PostInstallActions = new List<string>
            {
                "dark_mode", "file_extensions", "hidden_files",
                "disable_web_search", "classic_context_menu",
                "disable_widgets", "install_winget",
            },
        },

        "minimal" => new IsoBuilderConfig
        {
            UseLocalAccount = true,
            Username = "User",
            AutoLogin = true,
            SkipOobe = true,
            SkipEula = true,
            DisableTelemetry = true,
            DisableCortana = true,
            DisableOneDrive = true,
            BypassTpm = true,
            BypassSecureBoot = true,
            BypassRam = true,
            BypassStorage = true,
            BypassCpu = true,
            BloatwareToRemove = new List<string>
            {
                "Microsoft.BingNews", "Microsoft.BingWeather", "Microsoft.GetHelp",
                "Microsoft.Getstarted", "Microsoft.MicrosoftSolitaireCollection",
                "Microsoft.People", "Microsoft.PowerAutomateDesktop",
                "Microsoft.Todos", "Microsoft.WindowsAlarms",
                "Microsoft.WindowsFeedbackHub", "Microsoft.WindowsMaps",
                "Microsoft.WindowsSoundRecorder", "Microsoft.YourPhone",
                "Microsoft.ZuneMusic", "Microsoft.ZuneVideo",
                "Clipchamp.Clipchamp", "Microsoft.MicrosoftOfficeHub",
                "Microsoft.549981C3F5F10", "MicrosoftTeams",
                "Microsoft.OutlookForWindows",
                "Microsoft.Xbox.TCUI", "Microsoft.XboxGameOverlay",
                "Microsoft.XboxGamingOverlay", "Microsoft.XboxIdentityProvider",
                "Microsoft.XboxSpeechToTextOverlay",
            },
            WingetApps = new List<string>(),
            PresetDefault = true,
            PresetGaming = false,
            PresetPrivacy = false,
            PresetDev = false,
            PresetOffice = false,
            PostInstallActions = new List<string>
            {
                "dark_mode", "file_extensions",
                "disable_web_search", "disable_widgets", "disable_copilot",
            },
        },

        _ => null
    };

    // HELPERS
    // ══════════════════════════════════════════════

    private async Task ShowDialog(string title, string content)
    {
        try
        {
            var dialog = new ContentDialog
            {
                Title = title,
                Content = content,
                CloseButtonText = "OK",
                XamlRoot = Content.XamlRoot
            };
            await dialog.ShowAsync();
        }
        catch { }
    }

    // ── Win32 File Dialog (works in elevated/admin mode) ──
    private static string? ShowWin32OpenDialog(string title, string filter, nint ownerHwnd)
    {
        try
        {
            var ofn = new OPENFILENAME();
            ofn.lStructSize = Marshal.SizeOf(ofn);
            ofn.hwndOwner = ownerHwnd;
            ofn.lpstrFilter = filter;
            ofn.lpstrFile = new string('\0', 260);
            ofn.nMaxFile = 260;
            ofn.lpstrTitle = title;
            ofn.Flags = 0x00080000 | 0x00001000 | 0x00000800; // OFN_EXPLORER | OFN_FILEMUSTEXIST | OFN_PATHMUSTEXIST
            
            if (GetOpenFileName(ref ofn))
                return ofn.lpstrFile.TrimEnd('\0');
        }
        catch { }
        return null;
    }

    private static string? ShowWin32OpenDialogMulti(string title, string filter, nint ownerHwnd)
    {
        return ShowWin32OpenDialog(title, filter, ownerHwnd);
    }

    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    private static extern nint SHBrowseForFolder(ref BROWSEINFO lpbi);

    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    private static extern bool SHGetPathFromIDList(nint pidl, char[] pszPath);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct BROWSEINFO
    {
        public nint hwndOwner;
        public nint pidlRoot;
        public string pszDisplayName;
        public string lpszTitle;
        public uint ulFlags;
        public nint lpfn;
        public nint lParam;
        public int iImage;
    }

    private static string? ShowWin32FolderDialog(string title, nint ownerHwnd)
    {
        try
        {
            var bi = new BROWSEINFO();
            bi.hwndOwner = ownerHwnd;
            bi.lpszTitle = title;
            bi.ulFlags = 0x00000040 | 0x00000010; // BIF_NEWDIALOGSTYLE | BIF_EDITBOX
            bi.pszDisplayName = new string('\0', 260);

            var pidl = SHBrowseForFolder(ref bi);
            if (pidl == IntPtr.Zero) return null;

            var path = new char[260];
            if (SHGetPathFromIDList(pidl, path))
            {
                Marshal.FreeCoTaskMem(pidl);
                return new string(path).TrimEnd('\0');
            }
            Marshal.FreeCoTaskMem(pidl);
        }
        catch { }
        return null;
    }


    [DllImport("comdlg32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern bool GetOpenFileName(ref OPENFILENAME lpofn);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct OPENFILENAME
    {
        public int lStructSize;
        public nint hwndOwner;
        public nint hInstance;
        public string lpstrFilter;
        public string lpstrCustomFilter;
        public int nMaxCustFilter;
        public int nFilterIndex;
        public string lpstrFile;
        public int nMaxFile;
        public string lpstrFileTitle;
        public int nMaxFileTitle;
        public string lpstrInitialDir;
        public string lpstrTitle;
        public int Flags;
        public short nFileOffset;
        public short nFileExtension;
        public string lpstrDefExt;
        public nint lCustData;
        public nint lpfnHook;
        public string lpTemplateName;
        public nint pvReserved;
        public int dwReserved;
        public int FlagsEx;
    }
}

// ══════════════════════════════════════════════

// DATA MODELS
// ══════════════════════════════════════════════

public sealed class BloatwareItem
{
    public string PackageName { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Category { get; set; } = "";
    public bool Selected { get; set; }

    public BloatwareItem(string p, string d, string c, bool s)
    {
        PackageName = p; DisplayName = d; Category = c; Selected = s;
    }

    public BloatwareItem() { }
}

public sealed class DriverItem
{
    public string ClassName { get; set; } = "";
    public string ProviderName { get; set; } = "";
    public string DriverFile { get; set; } = "";
    public string Version { get; set; } = "";
    public string DeviceName { get; set; } = "";
    public bool Selected { get; set; } = true;
}

public sealed class DriverJsonItem
{
    public string? ClassName { get; set; }
    public string? ProviderName { get; set; }
    public string? Driver { get; set; }
    public string? Version { get; set; }
    public string? DeviceName { get; set; }
    public string? Source { get; set; }
}

public sealed class WingetApp
{
    public string WingetId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Category { get; set; } = "";
    public bool Selected { get; set; }

    public WingetApp(string id, string n, string c, bool s)
    {
        WingetId = id; DisplayName = n; Category = c; Selected = s;
    }

    public WingetApp() { }
}

public sealed class IsoBuilderConfig
{
    public string IsoPath { get; set; } = "";
    public bool UseLocalAccount { get; set; } = true;
    public string Username { get; set; } = "Admin";
    public string Password { get; set; } = "";
    public bool AutoLogin { get; set; } = true;
    public bool SkipOobe { get; set; } = true;
    public bool SkipEula { get; set; } = true;
    public bool DisableTelemetry { get; set; } = true;
    public bool DisableCortana { get; set; } = true;
    public bool DisableOneDrive { get; set; } = true;
    public string ComputerName { get; set; } = "";
    public string Language { get; set; } = "en-US";
    public string Timezone { get; set; } = "UTC";
    public bool BypassTpm { get; set; }
    public bool BypassSecureBoot { get; set; }
    public bool BypassRam { get; set; }
    public bool BypassStorage { get; set; }
    public bool BypassCpu { get; set; }
    public bool WifiEnabled { get; set; }
    public string WifiSsid { get; set; } = "";
    public string WifiPassword { get; set; } = "";
    public string WifiSecurity { get; set; } = "WPA2PSK";
    public List<string> BloatwareToRemove { get; set; } = new();
    public List<string> PostInstallActions { get; set; } = new();
    public List<string> ExePaths { get; set; } = new();
    public bool SilentInstall { get; set; } = true;
    public bool IncludeDrivers { get; set; } = true;
    public List<DriverItem> Drivers { get; set; } = new();
    public List<string> WingetApps { get; set; } = new();
    public bool PresetDefault { get; set; } = true;
    public bool PresetGaming { get; set; }
    public bool PresetPrivacy { get; set; }
    public bool PresetDev { get; set; }
    public bool PresetOffice { get; set; }
}
