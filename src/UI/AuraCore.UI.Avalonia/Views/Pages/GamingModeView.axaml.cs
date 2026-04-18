using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Interactivity;
using global::Avalonia.Layout;
using global::Avalonia.Media;
using global::Avalonia.Threading;
using AuraCore.Application;
using AuraCore.Application.Interfaces.Modules;
using AuraCore.Module.GamingMode;
using AuraCore.Module.GamingMode.Models;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;

namespace AuraCore.UI.Avalonia.Views.Pages;

public record BgProcessItem(string Name, string MemText, string Category, bool Suggest, int Pid);

public partial class GamingModeView : UserControl
{
    private readonly GamingModeModule? _module;
    private readonly List<CheckBox> _toggleCbs = new();

    // ── Session duration timer ──
    private DispatcherTimer? _sessionTimer;

    // ── Auto-Detect fields ──
    private DispatcherTimer? _gameWatchTimer;
    private bool _gameDetected;
    private string? _detectedGameName;

    private static readonly HashSet<string> KnownGames = new(StringComparer.OrdinalIgnoreCase)
    {
        "steam", "steamservice",
        "csgo", "cs2", "dota2", "hl2", "left4dead2",
        "fortniteClient-win64-shipping", "fortniteclient-win64-shipping", "rocketleague",
        "league of legends", "leagueclient", "valorant", "valorant-win64-shipping", "riotclientservices",
        "wow", "overwatch", "diablo iv", "hearthstone", "battle.net",
        "fifa", "fc24", "fc25", "apexlegends", "battlefield", "needforspeed", "thesims4",
        "assassinscreed", "farcry", "r6-siege",
        "minecraft", "javaw",
        "gta5", "gtav", "rdr2", "cyberpunk2077",
        "eldenring", "darksouls", "sekiro",
        "baldursgate3", "bg3", "hogwartslegacy",
        "terraria", "stardewvalley", "witcher3",
        "halo", "haloinfinite",
        "cod", "moderwarfare", "blackops",
        "pubg", "pubgtslgame", "rust", "ark",
        "satisfactory", "factorio",
        "totalwar", "civilization", "civ6",
        "flightsimulator",
        "godofwar", "horizonzerodawn",
        "spiderman", "spidermanremastered", "ghostoftsushima",
        "unrealengine", "unitycrashandler",
    };

    public GamingModeView()
    {
        InitializeComponent();
        Loaded += (s, e) => ApplyLocalization();
        LocalizationService.LanguageChanged += () =>
            Dispatcher.UIThread.Post(ApplyLocalization);
        _module = App.Services.GetServices<IOptimizationModule>()
            .OfType<GamingModeModule>().FirstOrDefault();
        Loaded += async (s, e) => await RunScan();
        Unloaded += (s, e) => { StopGameWatcher(); StopSessionTimer(); };
}

    private async Task RunScan()
    {
        if (_module is null) return;
        await _module.ScanAsync(new ScanOptions());
        var state = _module.LastState;
        if (state is null) return;

        UpdateStatus(state.IsActive);
        PowerPlanText.Text = string.Format(LocalizationService._("gaming.powerPlan"), state.CurrentPowerPlan);

        // Toggles
        TogglePanel.Children.Clear();
        _toggleCbs.Clear();
        foreach (var t in state.Toggles)
        {
            var card = new Border
            {
                CornerRadius = new CornerRadius(10), Padding = new Thickness(16, 12),
                Background = new SolidColorBrush(Color.Parse("#1A1A28")),
                BorderBrush = new SolidColorBrush(Color.Parse("#33334A")), BorderThickness = new Thickness(1)
            };
            var grid = new Grid { ColumnDefinitions = ColumnDefinitions.Parse("Auto,*,60") };
            var cb = new CheckBox { IsChecked = true, Tag = t.Id, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) };
            _toggleCbs.Add(cb);
            Grid.SetColumn(cb, 0);
            grid.Children.Add(cb);
            var info = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Spacing = 2 };
            info.Children.Add(new TextBlock { Text = t.Name, FontSize = 12, FontWeight = FontWeight.SemiBold, Foreground = new SolidColorBrush(Color.Parse("#E8E8F0")) });
            info.Children.Add(new TextBlock { Text = t.Description, FontSize = 10, Foreground = new SolidColorBrush(Color.Parse("#555570")), TextWrapping = TextWrapping.Wrap });
            Grid.SetColumn(info, 1);
            grid.Children.Add(info);
            var risk = new TextBlock { Text = t.Risk, FontSize = 10, FontWeight = FontWeight.SemiBold,
                Foreground = new SolidColorBrush(Color.Parse(t.Risk == "Caution" ? "#F59E0B" : "#22C55E")),
                VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right };
            Grid.SetColumn(risk, 2);
            grid.Children.Add(risk);
            card.Child = grid;
            TogglePanel.Children.Add(card);
        }

        // Processes
        var items = state.BackgroundProcesses.Select(p => new BgProcessItem(
            p.Name, $"{p.MemoryMb} MB", p.Category, p.SuggestSuspend, p.Pid
        )).ToList();
        ProcessList.ItemsSource = items;
    }

    private void UpdateStatus(bool active)
    {
        var state = _module?.LastState;
        var L = LocalizationService._;

        if (active)
        {
            StatusLabel.Text = L("gaming.status.active");
            StatusLabel.Foreground = new SolidColorBrush(Color.Parse("#22C55E"));
            StatusDot.Background = new SolidColorBrush(Color.Parse("#22C55E"));
            StatusBorder.Background = new SolidColorBrush(Color.Parse("#0D22C55E"));
            ActivateLabel.Text = L("gaming.action.deactivate");
            ActivateBtn.Background = new SolidColorBrush(Color.Parse("#EF4444"));

            // Show active session badge
            ActiveBadge.IsVisible = true;
            StatusRowGrid.IsVisible = true;
            StartSessionTimer();

            // Update GPU/Network/WU status labels
            if (state is not null)
            {
                GpuStatusLabel.Text = state.GpuOptimized ? state.GpuStatus : state.GpuVendor;
                GpuStatusLabel.Foreground = new SolidColorBrush(Color.Parse(state.GpuOptimized ? "#22C55E" : "#8B5CF6"));

                NetworkQosLabel.Text = state.NetworkQosActive ? L("gaming.qos.active") : L("gaming.status.inactive");
                NetworkQosLabel.Foreground = new SolidColorBrush(Color.Parse(state.NetworkQosActive ? "#22C55E" : "#3B82F6"));

                WinUpdateLabel.Text = state.WindowsUpdatePaused ? L("gaming.wu.paused") : L("gaming.wu.running");
                WinUpdateLabel.Foreground = new SolidColorBrush(Color.Parse(state.WindowsUpdatePaused ? "#22C55E" : "#F59E0B"));
            }
        }
        else
        {
            StatusLabel.Text = L("gaming.status.inactive");
            StatusLabel.Foreground = new SolidColorBrush(Color.Parse("#8888A0"));
            StatusDot.Background = new SolidColorBrush(Color.Parse("#555570"));
            StatusBorder.Background = new SolidColorBrush(Color.Parse("#1A1A28"));
            ActivateLabel.Text = L("gaming.action.activate");
            ActivateBtn.Background = new SolidColorBrush(Color.Parse("#00D4AA"));

            // Hide active session badge
            ActiveBadge.IsVisible = false;
            StatusRowGrid.IsVisible = false;
            StopSessionTimer();
        }
    }

    // ── Session Duration Timer ──

    private void StartSessionTimer()
    {
        if (_sessionTimer is not null) return;
        SessionDurationText.Text = string.Format(LocalizationService._("gaming.session.duration"), "0:00");
        _sessionTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _sessionTimer.Tick += SessionTimerTick;
        _sessionTimer.Start();
    }

    private void StopSessionTimer()
    {
        if (_sessionTimer is null) return;
        _sessionTimer.Tick -= SessionTimerTick;
        _sessionTimer.Stop();
        _sessionTimer = null;
        SessionDurationText.Text = string.Format(LocalizationService._("gaming.session.duration"), "0:00");
    }

    private void SessionTimerTick(object? sender, EventArgs e)
    {
        var activatedAt = _module?.LastState?.ActivatedAt;
        if (activatedAt is null) return;
        var elapsed = DateTime.UtcNow - activatedAt.Value;
        var elapsedStr = elapsed.TotalHours >= 1 ? elapsed.ToString(@"h\:mm\:ss") : elapsed.ToString(@"m\:ss");
        SessionDurationText.Text = string.Format(LocalizationService._("gaming.session.duration"), elapsedStr);
    }

    private async void Activate_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (_module is null) return;
            ActivateBtn.IsEnabled = false;
            ActivateLabel.Text = LocalizationService._("common.working");

            var isActive = _module.IsActive;
            var ids = new List<string> { isActive ? "deactivate" : "activate" };
            if (!isActive)
                ids.AddRange(_toggleCbs.Where(cb => cb.IsChecked == true).Select(cb => cb.Tag!.ToString()!));

            var plan = new OptimizationPlan(_module.Id, ids);
            var progress = new Progress<TaskProgress>(p =>
                global::Avalonia.Threading.Dispatcher.UIThread.Post(() => SubText.Text = p.StatusText));

            await _module.OptimizeAsync(plan, progress);
            await RunScan();
            ActivateBtn.IsEnabled = true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error in Activate_Click: {ex.Message}");
        }
}

    // ── Auto-Detect wiring ──

    private void AutoDetectToggle_Changed(object? sender, RoutedEventArgs e)
    {
        if (AutoDetectToggle.IsChecked == true)
            StartGameWatcher();
        else
            StopGameWatcher();
    }

    private void StartGameWatcher()
    {
        if (_gameWatchTimer is not null) return;
        _gameWatchTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _gameWatchTimer.Tick += GameWatchTick;
        _gameWatchTimer.Start();
        UpdateAutoDetectUI(watching: true, gameName: null);
        AutoDetectLabel.Text = LocalizationService._("gaming.autoDetect.on");
    }

    private void StopGameWatcher()
    {
        if (_gameWatchTimer is null) return;
        _gameWatchTimer.Tick -= GameWatchTick;
        _gameWatchTimer.Stop();
        _gameWatchTimer = null;

        if (_gameDetected)
        {
            _gameDetected = false;
            _detectedGameName = null;
        }
        UpdateAutoDetectUI(watching: false, gameName: null);
    }

    private void GameWatchTick(object? sender, EventArgs e)
    {
        string? foundGame = null;
        try
        {
            foreach (var proc in Process.GetProcesses())
            {
                try
                {
                    if (KnownGames.Contains(proc.ProcessName))
                    {
                        foundGame = proc.ProcessName;
                        break;
                    }
                }
                catch { }
                finally { try { proc.Dispose(); } catch { } }
            }
        }
        catch { }

        if (foundGame is not null && !_gameDetected)
        {
            _gameDetected = true;
            _detectedGameName = foundGame;
            UpdateAutoDetectUI(watching: true, gameName: foundGame);

            // Auto-activate gaming mode if not already active
            if (_module is not null && !_module.IsActive)
                _ = AutoActivateAsync();
        }
        else if (foundGame is null && _gameDetected)
        {
            var lastGame = _detectedGameName;
            _gameDetected = false;
            _detectedGameName = null;
            UpdateAutoDetectUI(watching: true, gameName: null);

            // Auto-deactivate gaming mode
            if (_module is not null && _module.IsActive)
                _ = AutoDeactivateAsync();
        }
    }

    private async Task AutoActivateAsync()
    {
        if (_module is null) return;
        try
        {
            var ids = new List<string> { "activate", "power-plan", "notifications", "suspend-bg", "clean-ram", "gpu-optimize", "network-qos", "pause-wu" };
            var plan = new OptimizationPlan(_module.Id, ids);
            await _module.OptimizeAsync(plan);
            await RunScan();
        }
        catch { }
    }

    private async Task AutoDeactivateAsync()
    {
        if (_module is null) return;
        try
        {
            var plan = new OptimizationPlan(_module.Id, new List<string> { "deactivate" });
            await _module.OptimizeAsync(plan);
            await RunScan();
        }
        catch { }
    }

    private void UpdateAutoDetectUI(bool watching, string? gameName)
    {
        var L = LocalizationService._;
        if (watching)
        {
            AutoDetectDot.Background = gameName is not null
                ? new SolidColorBrush(Color.Parse("#22C55E"))
                : new SolidColorBrush(Color.Parse("#A855F7"));
            AutoDetectLabel.Text = gameName is not null
                ? string.Format(L("gaming.autoDetect.detected"), gameName)
                : L("gaming.autoDetect.watching");
            AutoDetectLabel.Foreground = gameName is not null
                ? new SolidColorBrush(Color.Parse("#22C55E"))
                : new SolidColorBrush(Color.Parse("#A855F7"));
            DetectedGameLabel.Text = gameName is not null
                ? L("gaming.autoDetect.autoActivated")
                : "";
            DetectedGameLabel.IsVisible = gameName is not null;
        }
        else
        {
            AutoDetectDot.Background = new SolidColorBrush(Color.Parse("#555570"));
            AutoDetectLabel.Text = L("gaming.autoDetect.off");
            AutoDetectLabel.Foreground = new SolidColorBrush(Color.Parse("#8888A0"));
            DetectedGameLabel.IsVisible = false;
        }
    }

    private void ApplyLocalization()
    {
        var L = LocalizationService._;
        PageTitle.Text          = L("nav.gamingMode");
        ModuleHdr.Title         = L("gaming.title");
        ModuleHdr.Subtitle      = L("gaming.subtitle");
        GamingActiveLabel.Text  = L("gaming.activeLabel");
        OptimizationsLabel.Text = L("gaming.section.optimizations");
        BgProcessesLabel.Text   = L("gaming.section.bgProcesses");
        NetworkStatLabel.Text   = L("gaming.stat.network");
        WinUpdateStatLabel.Text = L("gaming.stat.winUpdate");
        // Refresh state-dependent labels
        if (_module is not null)
            UpdateStatus(_module.IsActive);
        else
        {
            ActivateLabel.Text = L("gaming.action.activate");
            StatusLabel.Text   = L("gaming.status.inactive");
            AutoDetectLabel.Text = L("gaming.autoDetect.off");
        }
    }
}