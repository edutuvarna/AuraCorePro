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
    private static string[] GetStepTitles() => new[]
    {
        LocalizationService._("isoBuild.step.selectIso"), LocalizationService._("isoBuild.step.userAccount"),
        LocalizationService._("isoBuild.step.oobe"),      LocalizationService._("isoBuild.step.wifi"),
        LocalizationService._("isoBuild.step.win11Bypass"), LocalizationService._("isoBuild.step.bloatware"),
        LocalizationService._("isoBuild.step.exeBundler"), LocalizationService._("isoBuild.step.driverBackup"),
        LocalizationService._("isoBuild.step.winget"),    LocalizationService._("isoBuild.step.registry"),
        LocalizationService._("isoBuild.step.postInstall"), LocalizationService._("isoBuild.step.review"),
    };
    private readonly List<Button> _stepButtons = new();

    public IsoBuilderView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        LocalizationService.LanguageChanged += () =>
            global::Avalonia.Threading.Dispatcher.UIThread.Post(ApplyLocalization);
        Unloaded += (s, e) =>
            LocalizationService.LanguageChanged -= () =>
                global::Avalonia.Threading.Dispatcher.UIThread.Post(ApplyLocalization);
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        try
        {
            ApplyLocalization();
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

    private void ApplyLocalization()
    {
        var L = LocalizationService._;
        PageTitle.Text              = L("nav.isoBuilder");
        ModuleHdr.Title             = L("isoBuild.title");
        ModuleHdr.Subtitle          = L("isoBuild.subtitle");
        SidebarTitle.Text           = L("isoBuild.title");
        TemplatesLabel.Text         = L("isoBuild.templates");
        TemplateCombo.PlaceholderText = L("isoBuild.loadTemplate");
        TmplGaming.Content          = L("isoBuild.tmpl.gaming");
        TmplOffice.Content          = L("isoBuild.tmpl.office");
        TmplPrivacy.Content         = L("isoBuild.tmpl.privacy");
        TmplDeveloper.Content       = L("isoBuild.tmpl.developer");
        TmplMinimal.Content         = L("isoBuild.tmpl.minimal");
        ImportLabel.Text            = L("common.import");
        ExportLabel.Text            = L("common.export");
        // Panel 0
        P0Title.Text                = L("isoBuild.s0.title");
        P0Desc.Text                 = L("isoBuild.s0.desc");
        BrowseIsoBtn.Content        = L("isoBuild.s0.browse");
        if (string.IsNullOrEmpty(IsoPathText.Text))
            IsoPathText.Text        = L("isoBuild.s0.noFile");
        // Panel 1
        P1Title.Text                = L("isoBuild.s1.title");
        P1Desc.Text                 = L("isoBuild.s1.desc");
        LocalAccountRadio.Content   = L("isoBuild.s1.localAccount");
        MsAccountRadio.Content      = L("isoBuild.s1.msAccount");
        UsernameBox.Watermark       = L("isoBuild.s1.usernameWatermark");
        PasswordBox.Watermark       = L("isoBuild.s1.passwordWatermark");
        AutoLoginCheck.Content      = L("isoBuild.s1.autoLogin");
        // Panel 2
        P2Title.Text                = L("isoBuild.s2.title");
        P2Desc.Text                 = L("isoBuild.s2.desc");
        SkipOobeCheck.Content       = L("isoBuild.s2.skipOobe");
        SkipEulaCheck.Content       = L("isoBuild.s2.skipEula");
        DisableTelemetryCheck.Content = L("isoBuild.s2.disableTelemetry");
        DisableCortanaCheck.Content = L("isoBuild.s2.disableCortana");
        DisableOneDriveCheck.Content = L("isoBuild.s2.disableOneDrive");
        ComputerNameBox.Watermark   = L("isoBuild.s2.computerNameWatermark");
        LangEnUs.Content            = L("isoBuild.s2.lang.enUs");
        LangTrTr.Content            = L("isoBuild.s2.lang.trTr");
        LangDeDe.Content            = L("isoBuild.s2.lang.deDe");
        LangFrFr.Content            = L("isoBuild.s2.lang.frFr");
        LangEsEs.Content            = L("isoBuild.s2.lang.esEs");
        LangRuRu.Content            = L("isoBuild.s2.lang.ruRu");
        TzUtc.Content               = L("isoBuild.s2.tz.utc");
        TzEastern.Content           = L("isoBuild.s2.tz.eastern");
        TzPacific.Content           = L("isoBuild.s2.tz.pacific");
        TzCet.Content               = L("isoBuild.s2.tz.cet");
        TzTurkey.Content            = L("isoBuild.s2.tz.turkey");
        TzMoscow.Content            = L("isoBuild.s2.tz.moscow");
        // Panel 3
        P3Title.Text                = L("isoBuild.s3.title");
        P3Desc.Text                 = L("isoBuild.s3.desc");
        EnableWifiCheck.Content     = L("isoBuild.s3.enableWifi");
        WifiSsidBox.Watermark       = L("isoBuild.s3.ssidWatermark");
        WifiPasswordBox.Watermark   = L("isoBuild.s3.passwordWatermark");
        WifiSec1.Content            = L("isoBuild.s3.wpa2");
        WifiSec2.Content            = L("isoBuild.s3.wpa3");
        WifiSec3.Content            = L("isoBuild.s3.wpaLegacy");
        WifiSec4.Content            = L("isoBuild.s3.open");
        WifiPlainTextNote.Text      = L("isoBuild.s3.plainTextNote");
        // Panel 4
        P4Title.Text                = L("isoBuild.s4.title");
        BypassWarning.Text          = L("isoBuild.s4.warning");
        BypassTpmCheck.Content      = L("isoBuild.s4.tpm");
        BypassSecureBootCheck.Content = L("isoBuild.s4.secureBoot");
        BypassRamCheck.Content      = L("isoBuild.s4.ram");
        BypassStorageCheck.Content  = L("isoBuild.s4.storage");
        BypassCpuCheck.Content      = L("isoBuild.s4.cpu");
        // Panel 5
        P5Title.Text                = L("isoBuild.s5.title");
        P5Desc.Text                 = L("isoBuild.s5.desc");
        SelectAllBtn.Content        = L("isoBuild.s5.selectAll");
        DeselectAllBtn.Content      = L("isoBuild.s5.deselectAll");
        // Panel 6
        P6Title.Text                = L("isoBuild.s6.title");
        P6Desc.Text                 = L("isoBuild.s6.desc");
        AddFilesBtn.Content         = L("isoBuild.s6.addFiles");
        ClearAllBtn.Content         = L("isoBuild.s6.clearAll");
        SilentInstallCheck.Content  = L("isoBuild.s6.silentInstall");
        if (string.IsNullOrEmpty(ExeCountText.Text))
            ExeCountText.Text       = L("isoBuild.s6.noFiles");
        // Panel 7
        P7Title.Text                = L("isoBuild.s7.title");
        P7Desc.Text                 = L("isoBuild.s7.desc");
        ScanDriversBtn.Content      = L("isoBuild.s7.scanDrivers");
        DriverAdminNote.Text        = L("isoBuild.s7.adminNote");
        IncludeDriversCheck.Content = L("isoBuild.s7.includeDrivers");
        // Panel 8
        P8Title.Text                = L("isoBuild.s8.title");
        P8Desc.Text                 = L("isoBuild.s8.desc");
        CustomWingetBox.Watermark   = L("isoBuild.s8.customIdWatermark");
        AddWingetBtn.Content        = L("common.add");
        // Panel 9
        P9Title.Text                = L("isoBuild.s9.title");
        P9Desc.Text                 = L("isoBuild.s9.desc");
        PresetDefaultCheck.Content  = L("iso.s10.default");
        PresetGamingCheck.Content   = L("iso.s10.gaming");
        PresetPrivacyCheck.Content  = L("iso.s10.privacy");
        PresetDevCheck.Content      = L("iso.s10.dev");
        PresetOfficeCheck.Content   = L("iso.s10.office");
        // Panel 10
        P10Title.Text               = L("isoBuild.s10.title");
        P10Desc.Text                = L("isoBuild.s10.desc");
        InstallWingetCheck.Content  = L("isoBuild.s10.installWinget");
        EnableDarkModeCheck.Content = L("isoBuild.s10.darkMode");
        ShowFileExtCheck.Content    = L("isoBuild.s10.fileExt");
        ShowHiddenFilesCheck.Content = L("isoBuild.s10.hiddenFiles");
        DisableWebSearchCheck.Content = L("isoBuild.s10.disableWebSearch");
        ClassicRightClickCheck.Content = L("isoBuild.s10.classicRightClick");
        DisableWidgetsCheck.Content = L("isoBuild.s10.disableWidgets");
        DisableCopilotCheck.Content = L("isoBuild.s10.disableCopilot");
        TaskbarLeftCheck.Content    = L("isoBuild.s10.taskbarLeft");
        // Panel 11
        P11Title.Text               = L("isoBuild.s11.title");
        OutputFormatLabel.Text      = L("isoBuild.s11.outputFormat");
        OutputUnattendOnly.Content  = L("isoBuild.s11.filesOnly");
        OutputStandaloneIso.Content = L("isoBuild.s11.standaloneIso");
        BuildSummaryLabel.Text      = L("isoBuild.s11.buildSummary");
        DisclaimerText.Text         = L("isoBuild.s11.disclaimer");
        DisclaimerCheck.Content     = L("isoBuild.s11.disclaimerAccept");
        // Bottom nav
        BackBtn.Content             = L("common.back");
        NextBtn.Content             = L("common.next");
        BuildBtn.Content            = L("isoBuild.action.build");
        // Terminal overlay
        BuildingTitle.Text          = L("isoBuild.terminal.title");
        if (string.IsNullOrEmpty(TerminalStepText.Text))
            TerminalStepText.Text   = L("isoBuild.terminal.initializing");
        TerminalCloseBtn.Content    = L("common.close");
        // Rebuild stepper with localized titles if already built
        if (_stepButtons.Count > 0) RebuildStepperLabels();
    }

    private void RebuildStepperLabels()
    {
        var titles = GetStepTitles();
        for (int i = 0; i < _stepButtons.Count && i < titles.Length; i++)
        {
            if (_stepButtons[i].Content is global::Avalonia.Controls.StackPanel sp && sp.Children.Count >= 2)
                if (sp.Children[1] is TextBlock lbl) lbl.Text = titles[i];
        }
        StepIndicator.Text = string.Format(LocalizationService._("isoBuild.stepOf"), _currentStep + 1, TotalSteps);
    }

    private void BuildStepper()
    {
        StepperPanel.Children.Clear();
        _stepButtons.Clear();

        var titles = GetStepTitles();
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
                Text = titles[i], FontSize = 13,
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

        StepIndicator.Text = string.Format(LocalizationService._("isoBuild.stepOf"), step + 1, TotalSteps);
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
                IsoPathText.Text = System.IO.Path.GetFileName(_isoPath) ?? "";
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
            ExeCountText.Text = string.Format(LocalizationService._("isoBuild.s6.filesAdded"), _exePaths.Count);
        }
        catch (Exception ex) { Debug.WriteLine($"Add EXE error: {ex.Message}"); }
    }

    private void ClearExe_Click(object? sender, RoutedEventArgs e)
    {
        _exePaths.Clear();
        ExeFilesList.Children.Clear();
        ExeCountText.Text = LocalizationService._("isoBuild.s6.noFiles");
    }

    // ══════════════════════════════════════════
    // DRIVERS (Windows-only stub)
    // ══════════════════════════════════════════

    private void ScanDrivers_Click(object? sender, RoutedEventArgs e)
    {
        DriverScanStatus.Text = OperatingSystem.IsWindows()
            ? LocalizationService._("isoBuild.s7.requiresAdmin")
            : LocalizationService._("isoBuild.s7.windowsOnly");
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
                var steam = _wingetApps.FirstOrDefault(w => w.WingetId == "Valve.Steam");
                if (steam != null) steam.Selected = true;
                var discord = _wingetApps.FirstOrDefault(w => w.WingetId == "Discord.Discord");
                if (discord != null) discord.Selected = true;
                PresetGamingCheck.IsChecked = true; PresetDefaultCheck.IsChecked = true;
                EnableDarkModeCheck.IsChecked = true;
                break;
            case "office":
                foreach (var b in _bloatware.Where(b => !b.Name.Contains("Office") && !b.Name.Contains("OneNote")))
                    b.Selected = true;
                var firefox = _wingetApps.FirstOrDefault(w => w.WingetId == "Mozilla.Firefox");
                if (firefox != null) firefox.Selected = true;
                var chrome = _wingetApps.FirstOrDefault(w => w.WingetId == "Google.Chrome");
                if (chrome != null) chrome.Selected = true;
                var acrobat = _wingetApps.FirstOrDefault(w => w.WingetId == "Adobe.Acrobat.Reader.64-bit");
                if (acrobat != null) acrobat.Selected = true;
                PresetDefaultCheck.IsChecked = true; PresetOfficeCheck.IsChecked = true;
                break;
            case "privacy":
                foreach (var b in _bloatware) b.Selected = true;
                PresetPrivacyCheck.IsChecked = true; PresetDefaultCheck.IsChecked = true;
                DisableTelemetryCheck.IsChecked = true; DisableCortanaCheck.IsChecked = true;
                DisableOneDriveCheck.IsChecked = true; DisableCopilotCheck.IsChecked = true;
                break;
            case "developer":
                var vscode = _wingetApps.FirstOrDefault(w => w.WingetId == "Microsoft.VisualStudioCode");
                if (vscode != null) vscode.Selected = true;
                var git = _wingetApps.FirstOrDefault(w => w.WingetId == "Git.Git");
                if (git != null) git.Selected = true;
                var node = _wingetApps.FirstOrDefault(w => w.WingetId == "OpenJS.NodeJS.LTS");
                if (node != null) node.Selected = true;
                var python = _wingetApps.FirstOrDefault(w => w.WingetId == "Python.Python.3.12");
                if (python != null) python.Selected = true;
                var powertoys = _wingetApps.FirstOrDefault(w => w.WingetId == "Microsoft.PowerToys");
                if (powertoys != null) powertoys.Selected = true;
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
        var L = LocalizationService._;
        var sb = new StringBuilder();
        sb.AppendLine($"ISO: {(_isoPath != null ? System.IO.Path.GetFileName(_isoPath) : L("isoBuild.summary.none"))}");
        sb.AppendLine($"{L("isoBuild.summary.account")}: {(LocalAccountRadio.IsChecked == true ? L("isoBuild.summary.local") : L("isoBuild.summary.microsoft"))}");
        sb.AppendLine($"{L("isoBuild.summary.bloatware")}: {_bloatware.Count(b => b.Selected)}");
        sb.AppendLine($"{L("isoBuild.summary.exes")}: {_exePaths.Count}");
        var selectedApps = _wingetApps.Count(w => w.Selected) + _customWingetIds.Count;
        sb.AppendLine($"{L("isoBuild.summary.winget")}: {selectedApps}");
        var presets = new List<string>();
        if (PresetDefaultCheck.IsChecked == true) presets.Add(L("isoBuild.summary.presetDefault"));
        if (PresetGamingCheck.IsChecked == true) presets.Add(L("isoBuild.summary.presetGaming"));
        if (PresetPrivacyCheck.IsChecked == true) presets.Add(L("isoBuild.summary.presetPrivacy"));
        if (PresetDevCheck.IsChecked == true) presets.Add(L("isoBuild.summary.presetDev"));
        if (PresetOfficeCheck.IsChecked == true) presets.Add(L("isoBuild.summary.presetOffice"));
        sb.AppendLine($"{L("isoBuild.summary.presets")}: {(presets.Count > 0 ? string.Join(", ", presets) : L("isoBuild.summary.none"))}");
        sb.AppendLine($"{L("isoBuild.summary.output")}: {(OutputUnattendOnly.IsChecked == true ? L("isoBuild.s11.filesOnly") : L("isoBuild.s11.standaloneIso"))}");

        SummaryText.Text = sb.ToString();
        EstimatedTimeText.Text = LocalizationService._("isoBuild.s11.estimatedTime");
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
            SummaryText.Text = LocalizationService._("isoBuild.error.noIso");
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
                AdminStatusText.Text = isAdmin
                    ? LocalizationService._("isoBuild.admin.isAdmin")
                    : LocalizationService._("isoBuild.admin.notAdmin");
                AdminStatusText.Foreground = new SolidColorBrush(Color.Parse(isAdmin ? "#00D4AA" : "#F59E0B"));
            }
            else
            {
                AdminStatusText.Text = OperatingSystem.IsLinux()
                    ? LocalizationService._("isoBuild.admin.linux")
                    : LocalizationService._("isoBuild.admin.macos");
            }
        }
        catch { AdminStatusText.Text = LocalizationService._("isoBuild.admin.unknown"); }
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
