using System.Diagnostics;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using global::Avalonia.Controls;
using global::Avalonia.Interactivity;
using global::Avalonia.Media;
using global::Avalonia.Threading;
using AuraCore.Application;

namespace AuraCore.UI.Avalonia.Views.Pages;

public partial class IsoBuilderView : UserControl
{
    private string? _isoPath;
    private readonly List<BloatItem> _bloatware = new();
    private readonly List<string> _exePaths = new();
    private readonly List<WingetAppItem> _wingetApps = new();
    private readonly List<string> _customWingetIds = new();
    private DateTime _buildStartTime;
    private readonly StringBuilder _terminalLog = new();

    private int _currentStep;
    private const int TotalSteps = 12;
    private static readonly string[] StepTitles = {
        "Select ISO", "User Account", "OOBE Settings", "WiFi",
        "Win11 Bypass", "Bloatware", "EXE Bundler", "Driver Backup",
        "Winget Apps", "Registry Presets", "Post-Install", "Review & Build"
    };
    private readonly List<Button> _stepButtons = new();

    public IsoBuilderView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        try
        {
            BuildStepper();
            ShowStep(0);
            PopulateBloatware();
            BuildBloatwareUI();
            PopulateWingetApps();
            BuildWingetUI();
            CheckAdmin();
            UpdateSummary();

            EnableWifiCheck.IsCheckedChanged += (s, e) =>
                WifiPanel.IsVisible = EnableWifiCheck.IsChecked == true;

            DisclaimerCheck.IsCheckedChanged += (s, e) =>
                BuildBtn.IsEnabled = DisclaimerCheck.IsChecked == true;

            TemplateCombo.SelectionChanged += TemplateCombo_Changed;
        }
        catch (Exception ex) { Debug.WriteLine($"IsoBuilder load error: {ex}"); }
    }

    // ══════════════════════════════════════════
    // WIZARD NAVIGATION
    // ══════════════════════════════════════════

    private void BuildStepper()
    {
        StepperPanel.Children.Clear();
        _stepButtons.Clear();

        for (int i = 0; i < TotalSteps; i++)
        {
            var idx = i;
            var btn = new Button
            {
                HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Stretch,
                HorizontalContentAlignment = global::Avalonia.Layout.HorizontalAlignment.Left,
                Padding = new global::Avalonia.Thickness(12, 10),
                Background = Brushes.Transparent,
                BorderThickness = new global::Avalonia.Thickness(0),
                CornerRadius = new global::Avalonia.CornerRadius(6),
                Cursor = new global::Avalonia.Input.Cursor(global::Avalonia.Input.StandardCursorType.Hand)
            };

            var sp = new StackPanel { Orientation = global::Avalonia.Layout.Orientation.Horizontal, Spacing = 10 };
            var numBorder = new Border
            {
                Width = 24, Height = 24,
                CornerRadius = new global::Avalonia.CornerRadius(12),
                Background = new SolidColorBrush(Color.Parse("#28FFFFFF"))
            };
            numBorder.Child = new TextBlock
            {
                Text = (i + 1).ToString(), FontSize = 11, FontWeight = FontWeight.Bold,
                HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Center,
                VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center,
                Foreground = new SolidColorBrush(Color.Parse("#8888A0"))
            };
            sp.Children.Add(numBorder);
            sp.Children.Add(new TextBlock
            {
                Text = StepTitles[i], FontSize = 13,
                VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center,
                Foreground = new SolidColorBrush(Color.Parse("#8888A0"))
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

        for (int i = 0; i < TotalSteps; i++)
        {
            var panel = this.FindControl<StackPanel>($"Panel_{i}");
            if (panel != null) panel.IsVisible = i == step;
        }

        // Update stepper visuals
        for (int i = 0; i < _stepButtons.Count; i++)
        {
            var btn = _stepButtons[i];
            if (btn.Content is StackPanel sp && sp.Children.Count >= 2)
            {
                var border = sp.Children[0] as Border;
                var label = sp.Children[1] as TextBlock;
                bool active = i == step;
                bool done = i < step;

                if (border != null)
                    border.Background = new SolidColorBrush(Color.Parse(
                        active ? "#00D4AA" : done ? "#2000D4AA" : "#28FFFFFF"));
                if (border?.Child is TextBlock numTb)
                    numTb.Foreground = new SolidColorBrush(Color.Parse(
                        active ? "#0A0A0F" : done ? "#00D4AA" : "#8888A0"));
                if (label != null)
                    label.Foreground = new SolidColorBrush(Color.Parse(
                        active ? "#E8E8F0" : done ? "#00D4AA" : "#8888A0"));
            }
        }

        StepIndicator.Text = $"Step {step + 1} of {TotalSteps}";
        BackBtn.IsVisible = step > 0;
        NextBtn.IsVisible = step < TotalSteps - 1;
        BuildBtn.IsVisible = step == TotalSteps - 1;

        if (step == TotalSteps - 1) UpdateSummary();
    }

    private void Next_Click(object? sender, RoutedEventArgs e) => ShowStep(_currentStep + 1);
    private void Back_Click(object? sender, RoutedEventArgs e) => ShowStep(_currentStep - 1);

    // ══════════════════════════════════════════
    // BLOATWARE
    // ══════════════════════════════════════════

    private void PopulateBloatware()
    {
        _bloatware.Clear();
        var items = new (string Name, string Package, bool Default)[]
        {
            ("3D Viewer", "Microsoft.Microsoft3DViewer", true),
            ("Bing Weather", "Microsoft.BingWeather", true),
            ("Clipchamp", "Clipchamp.Clipchamp", true),
            ("Cortana", "Microsoft.549981C3F5F10", true),
            ("Feedback Hub", "Microsoft.WindowsFeedbackHub", true),
            ("Get Help", "Microsoft.GetHelp", true),
            ("Groove Music", "Microsoft.ZuneMusic", true),
            ("Mail & Calendar", "microsoft.windowscommunicationsapps", false),
            ("Maps", "Microsoft.WindowsMaps", true),
            ("Microsoft News", "Microsoft.BingNews", true),
            ("Microsoft Solitaire", "Microsoft.MicrosoftSolitaireCollection", true),
            ("Microsoft Teams", "MicrosoftTeams", true),
            ("Microsoft Tips", "Microsoft.Getstarted", true),
            ("Mixed Reality Portal", "Microsoft.MixedReality.Portal", true),
            ("Movies & TV", "Microsoft.ZuneVideo", true),
            ("Office Hub", "Microsoft.MicrosoftOfficeHub", true),
            ("OneDrive", "Microsoft.OneDriveSync", false),
            ("OneNote", "Microsoft.Office.OneNote", false),
            ("Paint 3D", "Microsoft.MSPaint", false),
            ("People", "Microsoft.People", true),
            ("Phone Link", "Microsoft.YourPhone", true),
            ("Photos", "Microsoft.Windows.Photos", false),
            ("Power Automate", "Microsoft.PowerAutomateDesktop", true),
            ("Skype", "Microsoft.SkypeApp", true),
            ("Snipping Tool", "Microsoft.ScreenSketch", false),
            ("Sticky Notes", "Microsoft.MicrosoftStickyNotes", false),
            ("To Do", "Microsoft.Todos", true),
            ("Voice Recorder", "Microsoft.WindowsSoundRecorder", true),
            ("Xbox Game Bar", "Microsoft.XboxGamingOverlay", false),
            ("Xbox Identity", "Microsoft.XboxIdentityProvider", false),
            ("Microsoft Edge", "Microsoft.MicrosoftEdge", false),
        };
        foreach (var (name, pkg, def) in items)
            _bloatware.Add(new BloatItem { Name = name, Package = pkg, Selected = def });
    }

    private void BuildBloatwareUI()
    {
        BloatwareList.Children.Clear();
        foreach (var item in _bloatware)
        {
            var cb = new CheckBox { Content = $"{item.Name}  ({item.Package})", IsChecked = item.Selected, FontSize = 12 };
            cb.IsCheckedChanged += (s, e) => item.Selected = cb.IsChecked == true;
            BloatwareList.Children.Add(cb);
        }
    }

    private void SelectAllBloat_Click(object? sender, RoutedEventArgs e)
    {
        foreach (var item in _bloatware) item.Selected = true;
        foreach (var child in BloatwareList.Children)
            if (child is CheckBox cb) cb.IsChecked = true;
    }

    private void DeselectAllBloat_Click(object? sender, RoutedEventArgs e)
    {
        foreach (var item in _bloatware) item.Selected = false;
        foreach (var child in BloatwareList.Children)
            if (child is CheckBox cb) cb.IsChecked = false;
    }

    // ══════════════════════════════════════════
    // WINGET APPS
    // ══════════════════════════════════════════

    private void PopulateWingetApps()
    {
        _wingetApps.Clear();
        var apps = new (string Name, string Id, bool Default)[]
        {
            ("Google Chrome", "Google.Chrome", false),
            ("Mozilla Firefox", "Mozilla.Firefox", false),
            ("7-Zip", "7zip.7zip", true),
            ("VLC Media Player", "VideoLAN.VLC", true),
            ("Notepad++", "Notepad++.Notepad++", false),
            ("VS Code", "Microsoft.VisualStudioCode", false),
            ("Discord", "Discord.Discord", false),
            ("Steam", "Valve.Steam", false),
            ("Spotify", "Spotify.Spotify", false),
            ("qBittorrent", "qBittorrent.qBittorrent", false),
            ("WinRAR", "RARLab.WinRAR", false),
            ("Git", "Git.Git", false),
            ("Node.js LTS", "OpenJS.NodeJS.LTS", false),
            ("Python 3", "Python.Python.3.12", false),
            ("PowerToys", "Microsoft.PowerToys", false),
            ("Adobe Acrobat Reader", "Adobe.Acrobat.Reader.64-bit", false),
        };
        foreach (var (name, id, def) in apps)
            _wingetApps.Add(new WingetAppItem { Name = name, WingetId = id, Selected = def });
    }

    private void BuildWingetUI()
    {
        WingetAppList.Children.Clear();
        foreach (var app in _wingetApps)
        {
            var cb = new CheckBox { Content = $"{app.Name}  ({app.WingetId})", IsChecked = app.Selected, FontSize = 12 };
            cb.IsCheckedChanged += (s, e) => app.Selected = cb.IsChecked == true;
            WingetAppList.Children.Add(cb);
        }
    }

    private void AddCustomWinget_Click(object? sender, RoutedEventArgs e)
    {
        var id = CustomWingetBox.Text?.Trim();
        if (string.IsNullOrEmpty(id)) return;
        if (_customWingetIds.Contains(id)) return;
        _customWingetIds.Add(id);
        CustomWingetList.Children.Add(new TextBlock
        {
            Text = $"\u2713 {id}", FontSize = 12,
            Foreground = new SolidColorBrush(Color.Parse("#00D4AA"))
        });
        CustomWingetBox.Text = "";
    }

    // ══════════════════════════════════════════
    // FILE BROWSING (Platform-specific)
    // ══════════════════════════════════════════

    private async void BrowseIso_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;
            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new global::Avalonia.Platform.Storage.FilePickerOpenOptions
            {
                Title = "Select Windows ISO",
                FileTypeFilter = new[]
                {
                    new global::Avalonia.Platform.Storage.FilePickerFileType("ISO Files") { Patterns = new[] { "*.iso" } },
                    new global::Avalonia.Platform.Storage.FilePickerFileType("All Files") { Patterns = new[] { "*" } }
                },
                AllowMultiple = false
            });

            if (files.Count > 0)
            {
                _isoPath = files[0].Path.LocalPath;
                IsoPathText.Text = System.IO.Path.GetFileName(_isoPath);
                var fi = new FileInfo(_isoPath);
                var sizeMb = fi.Length / (1024.0 * 1024.0);
                var sizeGb = sizeMb / 1024.0;
                IsoOsText.Text = _isoPath.Contains("11", StringComparison.OrdinalIgnoreCase) ? "Windows 11" : "Windows 10 (or 11)";
                IsoSizeText.Text = sizeGb > 1 ? $"Size: {sizeGb:F2} GB" : $"Size: {sizeMb:F0} MB";
                IsoFileText.Text = _isoPath;
                IsoInfoPanel.IsVisible = true;
            }
        }
        catch (Exception ex) { Debug.WriteLine($"Browse ISO error: {ex.Message}"); }
    }

    private async void AddExe_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;
            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new global::Avalonia.Platform.Storage.FilePickerOpenOptions
            {
                Title = "Select Installers",
                FileTypeFilter = new[]
                {
                    new global::Avalonia.Platform.Storage.FilePickerFileType("Installers") { Patterns = new[] { "*.exe", "*.msi" } },
                },
                AllowMultiple = true
            });
            foreach (var f in files)
            {
                var path = f.Path.LocalPath;
                if (!_exePaths.Contains(path))
                {
                    _exePaths.Add(path);
                    ExeFilesList.Children.Add(new TextBlock
                    {
                        Text = $"\u2713 {System.IO.Path.GetFileName(path)}",
                        FontSize = 12, Foreground = new SolidColorBrush(Color.Parse("#00D4AA"))
                    });
                }
            }
            ExeCountText.Text = $"{_exePaths.Count} file(s) added";
        }
        catch (Exception ex) { Debug.WriteLine($"Add EXE error: {ex.Message}"); }
    }

    private void ClearExe_Click(object? sender, RoutedEventArgs e)
    {
        _exePaths.Clear();
        ExeFilesList.Children.Clear();
        ExeCountText.Text = "No files added";
    }

    // ══════════════════════════════════════════
    // DRIVERS (Windows-only stub)
    // ══════════════════════════════════════════

    private void ScanDrivers_Click(object? sender, RoutedEventArgs e)
    {
        DriverScanStatus.Text = OperatingSystem.IsWindows()
            ? "Driver scanning requires administrator mode."
            : "Driver scan only available on Windows.";
    }

    // ══════════════════════════════════════════
    // TEMPLATES
    // ══════════════════════════════════════════

    private void TemplateCombo_Changed(object? sender, SelectionChangedEventArgs e)
    {
        if (TemplateCombo.SelectedItem is not ComboBoxItem item) return;
        var tag = item.Tag?.ToString();
        ApplyTemplate(tag ?? "");
    }

    private void ApplyTemplate(string template)
    {
        // Reset all to defaults first
        foreach (var b in _bloatware) b.Selected = false;
        foreach (var w in _wingetApps) w.Selected = false;
        PresetDefaultCheck.IsChecked = false; PresetGamingCheck.IsChecked = false;
        PresetPrivacyCheck.IsChecked = false; PresetDevCheck.IsChecked = false;
        PresetOfficeCheck.IsChecked = false;

        switch (template)
        {
            case "gaming":
                foreach (var b in _bloatware) b.Selected = true;
                _wingetApps.FirstOrDefault(w => w.WingetId == "Valve.Steam")!.Selected = true;
                _wingetApps.FirstOrDefault(w => w.WingetId == "Discord.Discord")!.Selected = true;
                PresetGamingCheck.IsChecked = true; PresetDefaultCheck.IsChecked = true;
                EnableDarkModeCheck.IsChecked = true;
                break;
            case "office":
                foreach (var b in _bloatware.Where(b => !b.Name.Contains("Office") && !b.Name.Contains("OneNote")))
                    b.Selected = true;
                _wingetApps.FirstOrDefault(w => w.WingetId == "Mozilla.Firefox")!.Selected = true;
                _wingetApps.FirstOrDefault(w => w.WingetId == "Google.Chrome")!.Selected = true;
                _wingetApps.FirstOrDefault(w => w.WingetId == "Adobe.Acrobat.Reader.64-bit")!.Selected = true;
                PresetDefaultCheck.IsChecked = true; PresetOfficeCheck.IsChecked = true;
                break;
            case "privacy":
                foreach (var b in _bloatware) b.Selected = true;
                PresetPrivacyCheck.IsChecked = true; PresetDefaultCheck.IsChecked = true;
                DisableTelemetryCheck.IsChecked = true; DisableCortanaCheck.IsChecked = true;
                DisableOneDriveCheck.IsChecked = true; DisableCopilotCheck.IsChecked = true;
                break;
            case "developer":
                _wingetApps.FirstOrDefault(w => w.WingetId == "Microsoft.VisualStudioCode")!.Selected = true;
                _wingetApps.FirstOrDefault(w => w.WingetId == "Git.Git")!.Selected = true;
                _wingetApps.FirstOrDefault(w => w.WingetId == "OpenJS.NodeJS.LTS")!.Selected = true;
                _wingetApps.FirstOrDefault(w => w.WingetId == "Python.Python.3.12")!.Selected = true;
                _wingetApps.FirstOrDefault(w => w.WingetId == "Microsoft.PowerToys")!.Selected = true;
                PresetDevCheck.IsChecked = true; PresetDefaultCheck.IsChecked = true;
                ShowFileExtCheck.IsChecked = true; ShowHiddenFilesCheck.IsChecked = true;
                break;
            case "minimal":
                foreach (var b in _bloatware) b.Selected = true;
                PresetDefaultCheck.IsChecked = true;
                break;
        }

        // Rebuild UI
        BuildBloatwareUI();
        BuildWingetUI();
    }

    // ══════════════════════════════════════════
    // CONFIG IMPORT / EXPORT
    // ══════════════════════════════════════════

    private async void ImportConfig_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;
            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new global::Avalonia.Platform.Storage.FilePickerOpenOptions
            {
                Title = "Import Config",
                FileTypeFilter = new[] { new global::Avalonia.Platform.Storage.FilePickerFileType("JSON") { Patterns = new[] { "*.json" } } },
                AllowMultiple = false
            });
            if (files.Count > 0)
            {
                var json = await File.ReadAllTextAsync(files[0].Path.LocalPath);
                // TODO: Deserialize and apply config
                Debug.WriteLine($"Config imported: {json.Length} bytes");
            }
        }
        catch (Exception ex) { Debug.WriteLine($"Import error: {ex.Message}"); }
    }

    private async void ExportConfig_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;
            var file = await topLevel.StorageProvider.SaveFilePickerAsync(new global::Avalonia.Platform.Storage.FilePickerSaveOptions
            {
                Title = "Export Config",
                SuggestedFileName = "auracore-iso-config.json",
                FileTypeChoices = new[] { new global::Avalonia.Platform.Storage.FilePickerFileType("JSON") { Patterns = new[] { "*.json" } } }
            });
            if (file != null)
            {
                var config = new { bloatware = _bloatware.Where(b => b.Selected).Select(b => b.Package).ToList() };
                await File.WriteAllTextAsync(file.Path.LocalPath, JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true }));
            }
        }
        catch (Exception ex) { Debug.WriteLine($"Export error: {ex.Message}"); }
    }

    // ══════════════════════════════════════════
    // BUILD
    // ══════════════════════════════════════════

    private void UpdateSummary()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"ISO: {(_isoPath != null ? System.IO.Path.GetFileName(_isoPath) : "None")}");
        sb.AppendLine($"Account: {(LocalAccountRadio.IsChecked == true ? "Local" : "Microsoft")}");
        sb.AppendLine($"Bloatware to remove: {_bloatware.Count(b => b.Selected)}");
        sb.AppendLine($"Custom EXEs: {_exePaths.Count}");
        var selectedApps = _wingetApps.Count(w => w.Selected) + _customWingetIds.Count;
        sb.AppendLine($"Winget apps: {selectedApps}");
        var presets = new List<string>();
        if (PresetDefaultCheck.IsChecked == true) presets.Add("Default");
        if (PresetGamingCheck.IsChecked == true) presets.Add("Gaming");
        if (PresetPrivacyCheck.IsChecked == true) presets.Add("Privacy");
        if (PresetDevCheck.IsChecked == true) presets.Add("Developer");
        if (PresetOfficeCheck.IsChecked == true) presets.Add("Office");
        sb.AppendLine($"Registry presets: {(presets.Count > 0 ? string.Join(", ", presets) : "None")}");
        sb.AppendLine($"Output: {(OutputUnattendOnly.IsChecked == true ? "Files only" : "Standalone ISO")}");

        SummaryText.Text = sb.ToString();
        EstimatedTimeText.Text = "Estimated time: ~5-15 seconds";
    }

    private IsoBuilderConfig BuildConfig()
    {
        var cfg = new IsoBuilderConfig
        {
            IsoPath = _isoPath ?? "",
            UseLocalAccount = LocalAccountRadio.IsChecked == true,
            Username = UsernameBox.Text ?? "Admin",
            Password = PasswordBox.Text ?? "",
            AutoLogin = AutoLoginCheck.IsChecked == true,
            SkipOobe = SkipOobeCheck.IsChecked == true,
            SkipEula = SkipEulaCheck.IsChecked == true,
            DisableTelemetry = DisableTelemetryCheck.IsChecked == true,
            DisableCortana = DisableCortanaCheck.IsChecked == true,
            DisableOneDrive = DisableOneDriveCheck.IsChecked == true,
            ComputerName = ComputerNameBox.Text ?? "",
            Language = (LanguageCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "en-US",
            Timezone = (TimezoneCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "UTC",
            BypassTpm = BypassTpmCheck.IsChecked == true,
            BypassSecureBoot = BypassSecureBootCheck.IsChecked == true,
            BypassRam = BypassRamCheck.IsChecked == true,
            BypassStorage = BypassStorageCheck.IsChecked == true,
            BypassCpu = BypassCpuCheck.IsChecked == true,
            WifiEnabled = EnableWifiCheck.IsChecked == true,
            WifiSsid = WifiSsidBox.Text ?? "",
            WifiPassword = WifiPasswordBox.Text ?? "",
            WifiSecurity = (WifiSecurityCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "WPA2PSK",
            SilentInstall = SilentInstallCheck.IsChecked == true,
            IncludeDrivers = IncludeDriversCheck.IsChecked == true,
            PresetDefault = PresetDefaultCheck.IsChecked == true,
            PresetGaming = PresetGamingCheck.IsChecked == true,
            PresetPrivacy = PresetPrivacyCheck.IsChecked == true,
            PresetDev = PresetDevCheck.IsChecked == true,
            PresetOffice = PresetOfficeCheck.IsChecked == true,
        };

        cfg.BloatwareToRemove = _bloatware.Where(b => b.Selected).Select(b => b.Package).ToList();
        cfg.ExePaths = _exePaths.ToList();
        cfg.WingetApps = _wingetApps.Where(w => w.Selected).Select(w => w.WingetId).Concat(_customWingetIds).ToList();

        var actions = new List<string>();
        if (EnableDarkModeCheck.IsChecked == true) actions.Add("dark_mode");
        if (ShowFileExtCheck.IsChecked == true) actions.Add("file_extensions");
        if (ShowHiddenFilesCheck.IsChecked == true) actions.Add("hidden_files");
        if (DisableWebSearchCheck.IsChecked == true) actions.Add("disable_web_search");
        if (ClassicRightClickCheck.IsChecked == true) actions.Add("classic_context_menu");
        if (DisableWidgetsCheck.IsChecked == true) actions.Add("disable_widgets");
        if (DisableCopilotCheck.IsChecked == true) actions.Add("disable_copilot");
        if (TaskbarLeftCheck.IsChecked == true) actions.Add("taskbar_left");
        if (InstallWingetCheck.IsChecked == true) actions.Add("install_winget");
        cfg.PostInstallActions = actions;

        return cfg;
    }

    private async void Build_Click(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_isoPath) || !File.Exists(_isoPath))
        {
            SummaryText.Text = "ERROR: Please select a valid ISO file first (Step 1).";
            return;
        }

        TerminalOverlay.IsVisible = true;
        _buildStartTime = DateTime.Now;
        _terminalLog.Clear();
        TerminalOutput.Text = "";
        TerminalCloseBtn.IsVisible = false;
        TerminalProgress.Value = 0;

        var cfg = BuildConfig();

        await Task.Run(async () =>
        {
            try
            {
                AppendTerminal("[*] Starting build...");
                UpdateProgress(5, "Collecting configuration...");
                await Task.Delay(200);

                AppendTerminal($"[*] ISO: {System.IO.Path.GetFileName(cfg.IsoPath)}");
                AppendTerminal($"[*] Account: {(cfg.UseLocalAccount ? cfg.Username : "Microsoft")}");
                AppendTerminal($"[*] Bloatware to remove: {cfg.BloatwareToRemove.Count}");
                AppendTerminal($"[*] Winget apps: {cfg.WingetApps.Count}");
                UpdateProgress(15, "Generating autounattend.xml...");

                var unattendXml = IsoBuilderService.GenerateUnattendXml(cfg);
                AppendTerminal($"[OK] autounattend.xml generated ({unattendXml.Length:N0} bytes)");
                UpdateProgress(40, "Generating PostInstall.ps1...");

                var postInstall = IsoBuilderService.GeneratePostInstallScript(cfg);
                AppendTerminal($"[OK] PostInstall.ps1 generated ({postInstall.Length:N0} bytes)");
                UpdateProgress(60, "Selecting output folder...");

                // Save files next to ISO
                var outputDir = System.IO.Path.GetDirectoryName(cfg.IsoPath) ?? Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                var buildDir = System.IO.Path.Combine(outputDir, "AuraCore-ISO-Build");
                Directory.CreateDirectory(buildDir);

                var unattendPath = System.IO.Path.Combine(buildDir, "autounattend.xml");
                var postInstallPath = System.IO.Path.Combine(buildDir, "PostInstall.ps1");

                UpdateProgress(75, "Writing output files...");
                await File.WriteAllTextAsync(unattendPath, unattendXml);
                AppendTerminal($"[OK] Written: {unattendPath}");

                await File.WriteAllTextAsync(postInstallPath, postInstall);
                AppendTerminal($"[OK] Written: {postInstallPath}");

                // Copy EXE installers if any
                if (cfg.ExePaths.Count > 0)
                {
                    var installersDir = System.IO.Path.Combine(buildDir, "AuraCoreInstallers");
                    Directory.CreateDirectory(installersDir);
                    foreach (var exe in cfg.ExePaths)
                    {
                        if (File.Exists(exe))
                        {
                            var dest = System.IO.Path.Combine(installersDir, System.IO.Path.GetFileName(exe));
                            File.Copy(exe, dest, overwrite: true);
                            AppendTerminal($"[OK] Copied: {System.IO.Path.GetFileName(exe)}");
                        }
                    }
                    UpdateProgress(85, "Copying installers...");
                }

                var elapsed = DateTime.Now - _buildStartTime;
                UpdateProgress(100, "Build complete!");
                AppendTerminal("");
                AppendTerminal($"[OK] Build completed in {elapsed.TotalSeconds:F1}s");
                AppendTerminal($"[OK] Output: {buildDir}");
                AppendTerminal("");
                AppendTerminal("[*] Copy autounattend.xml to the root of your USB drive or ISO.");
                AppendTerminal("[*] Copy PostInstall.ps1 to the root as well.");

                Dispatcher.UIThread.Post(() => TerminalCloseBtn.IsVisible = true);
            }
            catch (Exception ex)
            {
                AppendTerminal($"[ERROR] {ex.Message}");
                Dispatcher.UIThread.Post(() => TerminalCloseBtn.IsVisible = true);
            }
        });
    }

    private void AppendTerminal(string line)
    {
        _terminalLog.AppendLine(line);
        Dispatcher.UIThread.Post(() =>
        {
            TerminalOutput.Text = _terminalLog.ToString();
            TerminalScroll.ScrollToEnd();
        });
    }

    private void UpdateProgress(double value, string step)
    {
        Dispatcher.UIThread.Post(() =>
        {
            TerminalProgress.Value = value;
            TerminalStepText.Text = step;
            var elapsed = DateTime.Now - _buildStartTime;
            TerminalTimeText.Text = $"{elapsed.TotalSeconds:F0}s elapsed";
        });
    }

    private void TerminalClose_Click(object? sender, RoutedEventArgs e)
    {
        TerminalOverlay.IsVisible = false;
    }

    // ══════════════════════════════════════════
    // HELPERS
    // ══════════════════════════════════════════

    private void CheckAdmin()
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                using var identity = WindowsIdentity.GetCurrent();
                var principal = new WindowsPrincipal(identity);
                var isAdmin = principal.IsInRole(WindowsBuiltInRole.Administrator);
                AdminStatusText.Text = isAdmin ? "Running as Administrator" : "Not Administrator (limited)";
                AdminStatusText.Foreground = new SolidColorBrush(Color.Parse(isAdmin ? "#00D4AA" : "#F59E0B"));
            }
            else
            {
                AdminStatusText.Text = OperatingSystem.IsLinux() ? "Linux (root check)" : "macOS";
            }
        }
        catch { AdminStatusText.Text = "Admin status unknown"; }
    }

    // ══════════════════════════════════════════
    // DATA CLASSES
    // ══════════════════════════════════════════

    private sealed class BloatItem
    {
        public string Name { get; set; } = "";
        public string Package { get; set; } = "";
        public bool Selected { get; set; }
    }

    private sealed class WingetAppItem
    {
        public string Name { get; set; } = "";
        public string WingetId { get; set; } = "";
        public bool Selected { get; set; }
    }
}
