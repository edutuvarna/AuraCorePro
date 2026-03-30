using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using AuraCore.Application;
using AuraCore.Application.Interfaces.Modules;
using AuraCore.Desktop.Services;
using AuraCore.Module.GamingMode;
using AuraCore.Module.GamingMode.Models;

namespace AuraCore.Desktop.Pages;

public sealed partial class GamingModePage : Page
{
    private GamingModeModule? _module;
    private readonly Dictionary<string, bool> _toggleSelections = new();
    private readonly Dictionary<int, bool> _processSelections = new();
    private DispatcherTimer? _gamingTimer;
    private DateTimeOffset _gamingStartTime;

    public GamingModePage()
    {
        InitializeComponent();
        ApplyLocalization();
        Services.S.LanguageChanged += () => DispatcherQueue?.TryEnqueue(ApplyLocalization);
        var modules = App.Current.Services.GetServices<IOptimizationModule>();
        _module = modules.FirstOrDefault(m => m.Id == "gaming-mode") as GamingModeModule;
        InitAutoDetect();
    }

    private void InitAutoDetect()
    {
        var watcher = App.GameWatcher;
        if (watcher is null) return;

        AutoDetectToggle.IsOn = watcher.IsEnabled;
        UpdateAutoDetectStatus();
        RenderCustomGames();
        RenderProfiles();

        watcher.GameDetected += (game) =>
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                AutoDetectStatusText.Text = $"Game detected: {game} — Gaming Mode activated automatically";
                ModeStatusText.Text = "Gaming Mode is ON (Auto)";
                ModeStatusText.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 46, 125, 50));
            });
        };

        watcher.GameExited += () =>
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                AutoDetectStatusText.Text = "Game exited — Gaming Mode deactivated";
                ModeStatusText.Text = S._("game.off");
                ModeStatusText.Foreground = null;
            });
        };
    }

    private void UpdateAutoDetectStatus()
    {
        var watcher = App.GameWatcher;
        if (watcher is null) { AutoDetectStatusText.Text = S._("game.notInitialized"); return; }

        if (watcher.IsEnabled)
        {
            AutoDetectStatusText.Text = watcher.IsGameDetected
                ? $"Monitoring active — Game detected: {watcher.DetectedGameName}"
                : "Monitoring active — waiting for game launch...";
        }
        else
        {
            AutoDetectStatusText.Text = "Disabled";
        }
    }

    private void AutoDetectToggle_Toggled(object sender, RoutedEventArgs e)
    {
        var watcher = App.GameWatcher;
        if (watcher is null) return;

        if (AutoDetectToggle.IsOn) watcher.Start();
        else watcher.Stop();

        UpdateAutoDetectStatus();
    }

    private void AddCustomGame_Click(object sender, RoutedEventArgs e)
    {
        var name = CustomGameBox.Text.Trim();
        if (string.IsNullOrEmpty(name)) return;

        App.GameWatcher?.AddCustomGame(name);
        CustomGameBox.Text = "";
        RenderCustomGames();
    }

    private void RenderCustomGames()
    {
        CustomGamesList.Children.Clear();
        var watcher = App.GameWatcher;
        if (watcher is null) return;

        foreach (var game in watcher.GetCustomGames())
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            row.Children.Add(new TextBlock
            {
                Text = game, FontSize = 12, VerticalAlignment = VerticalAlignment.Center,
                FontFamily = new FontFamily("Consolas")
            });
            var removeBtn = new Button { Content = "Remove", Padding = new Thickness(8, 2, 8, 2), FontSize = 11 };
            var capturedGame = game;
            removeBtn.Click += (s, ev) =>
            {
                watcher.RemoveCustomGame(capturedGame);
                RenderCustomGames();
            };
            row.Children.Add(removeBtn);
            CustomGamesList.Children.Add(row);
        }
    }

    private async void AnalyzeBtn_Click(object sender, RoutedEventArgs e)
    {
        AnalyzeBtn.IsEnabled = false;
        Progress.IsActive = true;
        Progress.Visibility = Visibility.Visible;
        ToggleList.Children.Clear();
        BgProcessList.Children.Clear();
        ResultCard.Visibility = Visibility.Collapsed;
        _toggleSelections.Clear();
        _processSelections.Clear();

        try
        {
            if (_module is null) return;
            await _module.ScanAsync(new ScanOptions());
            var state = _module.LastState;
            if (state is null) return;

            // Update status
            PowerPlanText.Text = string.Format(S._("game.currentPlan"), state.CurrentPowerPlan);
            ModeDetailText.Text = state.IsActive
                ? "Gaming Mode is currently active — your system is optimized"
                : $"Found {state.BackgroundProcesses.Count} background processes using resources";

            if (state.IsActive)
            {
                ModeStatusText.Text = S._("game.on");
                ModeStatusText.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 46, 125, 50));
                DeactivateBtn.Visibility = Visibility.Visible;
                ActivateBtn.Visibility = Visibility.Collapsed;
            }
            else
            {
                ModeStatusText.Text = "Gaming Mode is OFF";
                ActivateBtn.Visibility = Visibility.Visible;
                ActivateBtn.IsEnabled = true;
                DeactivateBtn.Visibility = Visibility.Collapsed;
            }

            // Render toggles
            TogglesHeader.Visibility = Visibility.Visible;
            foreach (var toggle in state.Toggles)
            {
                _toggleSelections[toggle.Id] = !toggle.CurrentState; // Default ON for things not already active

                var card = new Border
                {
                    Background = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(16, 12, 16, 12),
                    BorderBrush = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["CardStrokeColorDefaultBrush"],
                    BorderThickness = new Thickness(0.5)
                };

                var grid = new Grid { ColumnSpacing = 12 };
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var toggleSwitch = new ToggleSwitch
                {
                    IsOn = !toggle.CurrentState,
                    OnContent = "",
                    OffContent = "",
                    VerticalAlignment = VerticalAlignment.Center
                };
                var capturedId = toggle.Id;
                toggleSwitch.Toggled += (s, ev) => _toggleSelections[capturedId] = toggleSwitch.IsOn;
                Grid.SetColumn(toggleSwitch, 0);
                grid.Children.Add(toggleSwitch);

                var info = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
                info.Children.Add(new TextBlock
                {
                    Text = toggle.Name,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    FontSize = 14
                });
                info.Children.Add(new TextBlock { Text = toggle.Description, FontSize = 12, Opacity = 0.6 });
                Grid.SetColumn(info, 1);
                grid.Children.Add(info);

                // Risk badge
                var riskColor = toggle.Risk switch
                {
                    "Safe" => Windows.UI.Color.FromArgb(255, 46, 125, 50),
                    "Caution" => Windows.UI.Color.FromArgb(255, 230, 81, 0),
                    _ => Windows.UI.Color.FromArgb(255, 158, 158, 158)
                };
                var badge = new Border
                {
                    Background = new SolidColorBrush(Windows.UI.Color.FromArgb(30, riskColor.R, riskColor.G, riskColor.B)),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(8, 3, 8, 3),
                    VerticalAlignment = VerticalAlignment.Center
                };
                badge.Child = new TextBlock
                {
                    Text = toggle.Risk.ToUpper(),
                    FontSize = 10,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(riskColor)
                };
                Grid.SetColumn(badge, 2);
                grid.Children.Add(badge);

                // Current state indicator
                if (toggle.CurrentState)
                {
                    var activeBadge = new Border
                    {
                        Background = new SolidColorBrush(Windows.UI.Color.FromArgb(25, 33, 150, 243)),
                        CornerRadius = new CornerRadius(4),
                        Padding = new Thickness(6, 3, 6, 3),
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    activeBadge.Child = new TextBlock
                    {
                        Text = "ALREADY ON",
                        FontSize = 9,
                        Opacity = 0.7
                    };
                    Grid.SetColumn(activeBadge, 3);
                    grid.Children.Add(activeBadge);
                }

                card.Child = grid;
                ToggleList.Children.Add(card);
            }

            // Render background processes
            var suspendable = state.BackgroundProcesses.Where(p => p.SuggestSuspend).ToList();
            if (suspendable.Count > 0)
            {
                BgHeader.Text = string.Format(S._("game.bgAppsHeader"), suspendable.Count);
                BgHeader.Visibility = Visibility.Visible;

                foreach (var proc in suspendable)
                {
                    _processSelections[proc.Pid] = true;

                    var row = new Grid
                    {
                        ColumnSpacing = 8,
                        Padding = new Thickness(12, 6, 12, 6),
                        BorderBrush = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["CardStrokeColorDefaultBrush"],
                        BorderThickness = new Thickness(0, 0, 0, 0.5)
                    };
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                    var check = new CheckBox { IsChecked = true, MinWidth = 0, VerticalAlignment = VerticalAlignment.Center };
                    var capturedPid = proc.Pid;
                    check.Checked += (s, ev) => _processSelections[capturedPid] = true;
                    check.Unchecked += (s, ev) => _processSelections[capturedPid] = false;
                    Grid.SetColumn(check, 0);
                    row.Children.Add(check);

                    var nameText = new TextBlock
                    {
                        Text = $"{proc.Name}  (PID {proc.Pid})",
                        FontSize = 13,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    Grid.SetColumn(nameText, 1);
                    row.Children.Add(nameText);

                    var memText = new TextBlock
                    {
                        Text = $"{proc.MemoryMb} MB",
                        FontSize = 12,
                        Opacity = 0.5,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    Grid.SetColumn(memText, 2);
                    row.Children.Add(memText);

                    BgProcessList.Children.Add(row);
                }
            }
        }
        catch (Exception ex)
        {
            ModeDetailText.Text = $"Error: {ex.Message}";
        }
        finally
        {
            AnalyzeBtn.IsEnabled = true;
            Progress.IsActive = false;
            Progress.Visibility = Visibility.Collapsed;
        }
    }

    private async void ActivateBtn_Click(object sender, RoutedEventArgs e)
    {
        var enabledToggles = _toggleSelections.Where(kv => kv.Value).Select(kv => kv.Key).ToList();
        if (enabledToggles.Count == 0)
        {
            ResultText.Text = S._("game.noOptSelected");
            ResultCard.Visibility = Visibility.Visible;
            return;
        }

        var summary = string.Join("\n", enabledToggles.Select(t => $"  • {t.Replace("-", " ")}"));
        var dialog = new ContentDialog
        {
            Title = "Activate Gaming Mode?",
            Content = $"The following optimizations will be applied:\n\n{summary}\n\nAll changes are reversible when you deactivate.",
            PrimaryButtonText = "Activate",
            CloseButtonText = "Cancel",
            XamlRoot = this.XamlRoot,
            DefaultButton = ContentDialogButton.Primary
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        ActivateBtn.IsEnabled = false;
        Progress.IsActive = true;
        Progress.Visibility = Visibility.Visible;

        try
        {
            if (_module is null) return;

            var ids = new List<string> { "activate" };
            ids.AddRange(enabledToggles);

            // Add selected PIDs to suspend
            foreach (var kv in _processSelections.Where(kv => kv.Value))
                ids.Add($"pid:{kv.Key}");

            var plan = new OptimizationPlan("gaming-mode", ids);
            var progress = new Progress<TaskProgress>(p =>
            {
                DispatcherQueue.TryEnqueue(() => ModeDetailText.Text = p.StatusText);
            });

            var result = await _module.OptimizeAsync(plan, progress);

            ModeStatusText.Text = "Gaming Mode is ON";
            ModeStatusText.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 46, 125, 50));
            ModeDetailText.Text = $"Applied {result.ItemsProcessed} optimizations — your system is ready for gaming";
            ActivateBtn.Visibility = Visibility.Collapsed;
            DeactivateBtn.Visibility = Visibility.Visible;

            // Start gaming timer
            _gamingStartTime = DateTimeOffset.Now;
            _gamingTimer?.Stop();
            _gamingTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _gamingTimer.Tick += (_, _) =>
            {
                var elapsed = DateTimeOffset.Now - _gamingStartTime;
                PowerPlanText.Text = $"⏱ Active for {(int)elapsed.TotalHours}h {elapsed.Minutes:D2}m {elapsed.Seconds:D2}s";
            };
            _gamingTimer.Start();
            ActivityLog.Add("🎮", $"Gaming Mode activated — {result.ItemsProcessed} optimizations applied");

            ResultText.Text = $"Gaming Mode activated! ({result.ItemsProcessed} changes applied in {result.Duration.TotalSeconds:F1}s)";
            ResultText.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 46, 125, 50));
            ResultCard.Visibility = Visibility.Visible;
        }
        catch (Exception ex)
        {
            ResultText.Text = $"Error: {ex.Message}";
            ResultText.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 198, 40, 40));
            ResultCard.Visibility = Visibility.Visible;
        }
        finally
        {
            Progress.IsActive = false;
            Progress.Visibility = Visibility.Collapsed;
        }
    }

    private async void DeactivateBtn_Click(object sender, RoutedEventArgs e)
    {
        DeactivateBtn.IsEnabled = false;
        Progress.IsActive = true;
        Progress.Visibility = Visibility.Visible;
        ModeDetailText.Text = "Restoring system to normal...";

        try
        {
            if (_module is null) return;

            var plan = new OptimizationPlan("gaming-mode", new List<string> { "deactivate" });
            var progress = new Progress<TaskProgress>(p =>
            {
                DispatcherQueue.TryEnqueue(() => ModeDetailText.Text = p.StatusText);
            });

            var result = await _module.OptimizeAsync(plan, progress);

            ModeStatusText.Text = "Gaming Mode is OFF";
            ModeStatusText.Foreground = null; // Reset to default
            ModeDetailText.Text = "All settings restored to normal";
            DeactivateBtn.Visibility = Visibility.Collapsed;
            ActivateBtn.Visibility = Visibility.Visible;
            ActivateBtn.IsEnabled = true;

            // Stop gaming timer
            var elapsed = DateTimeOffset.Now - _gamingStartTime;
            _gamingTimer?.Stop(); _gamingTimer = null;
            PowerPlanText.Text = "";
            ActivityLog.Add("🎮", $"Gaming Mode deactivated after {(int)elapsed.TotalHours}h {elapsed.Minutes}m");

            ResultText.Text = $"Gaming Mode deactivated — {result.ItemsProcessed} settings restored. Session: {(int)elapsed.TotalHours}h {elapsed.Minutes}m";
            ResultText.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 33, 150, 243));
            ResultCard.Visibility = Visibility.Visible;

            // Clear toggle/process lists
            ToggleList.Children.Clear();
            BgProcessList.Children.Clear();
            TogglesHeader.Visibility = Visibility.Collapsed;
            BgHeader.Visibility = Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            ResultText.Text = $"Error: {ex.Message}";
            ResultCard.Visibility = Visibility.Visible;
        }
        finally
        {
            DeactivateBtn.IsEnabled = true;
            Progress.IsActive = false;
            Progress.Visibility = Visibility.Collapsed;
        }
    }

    // ── GAME PROFILES ─────────────────────────────────────────

    private void CreateProfile_Click(object sender, RoutedEventArgs e)
    {
        var name = ProfileNameBox.Text.Trim();
        var games = ProfileGamesBox.Text.Trim();
        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(games))
        {
            ResultText.Text = "Please enter a profile name and at least one game process.";
            ResultCard.Visibility = Visibility.Visible;
            return;
        }

        var gameList = games.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

        var profile = new GameProfileStore.GameProfileEntry
        {
            ProfileName = name,
            GameProcesses = gameList,
            SwitchPowerPlan = ProfPower.IsChecked == true,
            DisableNotifications = ProfNotif.IsChecked == true,
            SuspendBackground = ProfSuspend.IsChecked == true,
            CleanRam = ProfRam.IsChecked == true,
            BoostPriority = ProfPriority.IsChecked == true,
        };

        GameProfileStore.Add(profile);
        ProfileNameBox.Text = "";
        ProfileGamesBox.Text = "";
        RenderProfiles();

        ResultText.Text = $"Profile \"{name}\" created for {gameList.Count} game(s).";
        ResultText.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 46, 125, 50));
        ResultCard.Visibility = Visibility.Visible;
    }

    private void RenderProfiles()
    {
        ProfilesList.Children.Clear();
        var profiles = GameProfileStore.GetAll();

        if (profiles.Count == 0)
        {
            ProfilesList.Children.Add(new TextBlock
            {
                Text = "No profiles yet. Create one above to customize per-game settings.",
                FontSize = 12, Opacity = 0.5
            });
            return;
        }

        foreach (var profile in profiles)
        {
            var card = new Border
            {
                Background = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(16, 12, 16, 12),
                BorderBrush = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["CardStrokeColorDefaultBrush"],
                BorderThickness = new Thickness(0.5)
            };

            var grid = new Grid { ColumnSpacing = 12 };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var info = new StackPanel { Spacing = 3 };
            info.Children.Add(new TextBlock
            {
                Text = profile.ProfileName,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, FontSize = 14
            });

            var gamesText = string.Join(", ", profile.GameProcesses);
            info.Children.Add(new TextBlock
            {
                Text = $"Games: {gamesText}",
                FontSize = 11, Opacity = 0.6, FontFamily = new FontFamily("Consolas")
            });

            var toggles = new List<string>();
            if (profile.SwitchPowerPlan) toggles.Add("Power Plan");
            if (profile.DisableNotifications) toggles.Add("Notifications");
            if (profile.SuspendBackground) toggles.Add("Suspend BG");
            if (profile.CleanRam) toggles.Add("Clean RAM");
            if (profile.BoostPriority) toggles.Add("Boost Priority");
            info.Children.Add(new TextBlock
            {
                Text = toggles.Count > 0 ? string.Join(" · ", toggles) : "No optimizations",
                FontSize = 11, Opacity = 0.4
            });

            Grid.SetColumn(info, 0);
            grid.Children.Add(info);

            var deleteBtn = new Button
            {
                Content = "Delete", Padding = new Thickness(10, 4, 10, 4),
                FontSize = 11, VerticalAlignment = VerticalAlignment.Center
            };
            var capturedId = profile.Id;
            deleteBtn.Click += (s, ev) =>
            {
                GameProfileStore.Delete(capturedId);
                RenderProfiles();
            };
            Grid.SetColumn(deleteBtn, 1);
            grid.Children.Add(deleteBtn);

            card.Child = grid;
            ProfilesList.Children.Add(card);
        }
    }

    private void ApplyLocalization()
    {
        if (FindName("PageTitle") is Microsoft.UI.Xaml.Controls.TextBlock title)
            title.Text = S._("game.title");
        if (FindName("PageSubtitle") is Microsoft.UI.Xaml.Controls.TextBlock subtitle)
            subtitle.Text = S._("game.subtitle");
    }
}
