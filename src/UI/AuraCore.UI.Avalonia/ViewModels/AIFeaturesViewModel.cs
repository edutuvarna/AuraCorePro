using AuraCore.UI.Avalonia.Services.AI;
using global::Avalonia.Controls;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;

namespace AuraCore.UI.Avalonia.ViewModels;

public enum AIFeaturesViewMode { Overview, Detail }

/// <summary>
/// Primary view-model for AIFeaturesView (spec §4.2).
/// Owns the 4 feature cards, manages Overview/Detail mode, wires toggle changes to settings + ambient service.
/// Section UserControl instances are cached to preserve state across navigation.
/// </summary>
public sealed class AIFeaturesViewModel : INotifyPropertyChanged
{
    private readonly AppSettings _settings;
    private readonly ICortexAmbientService _ambient;
    private readonly Dictionary<string, UserControl> _sectionViewCache = new();

    // Set to true while SyncChatCardFromSettings is running so the toggle-handler
    // in WireToggleHandlers doesn't try to re-open the opt-in dialog on a programmatic
    // IsEnabled flip. Reset immediately after the write.
    private bool _suppressChatToggleHandler;

    public AIFeaturesViewModel(AppSettings settings, ICortexAmbientService ambient)
    {
        _settings = settings;
        _ambient = ambient;

        InsightsCard = new AIFeatureCardVM(
            key: "insights",
            title: "Cortex Insights",
            accentColor: "AccentPurple",
            iconKey: "IconSparklesFilled",
            isChatExperimental: false) { IsEnabled = settings.InsightsEnabled };

        RecommendationsCard = new AIFeatureCardVM(
            key: "recommendations",
            title: "Recommendations",
            accentColor: "AccentTeal",
            iconKey: "IconLightbulb",
            isChatExperimental: false) { IsEnabled = settings.RecommendationsEnabled };

        ScheduleCard = new AIFeatureCardVM(
            key: "schedule",
            title: "Smart Schedule",
            accentColor: "AccentAmber",
            iconKey: "IconCalendarClock",
            isChatExperimental: false) { IsEnabled = settings.ScheduleEnabled };

        ChatCard = new AIFeatureCardVM(
            key: "chat",
            title: "Chat",
            accentColor: "AccentPink",
            iconKey: "IconMessageSquare",
            isChatExperimental: true) { IsEnabled = settings.ChatEnabled };

        WireToggleHandlers();

        NavigateToSection = new DelegateCommand<string>(OnNavigateToSection);
        NavigateToOverview = new DelegateCommand<object?>(_ => SetMode(AIFeaturesViewMode.Overview, "overview"));

        // Propagate ambient PropertyChanged → hero status text
        _ambient.PropertyChanged += (_, _) => OnPropertyChanged(nameof(HeroStatusText));
    }

    public AIFeatureCardVM InsightsCard { get; }
    public AIFeatureCardVM RecommendationsCard { get; }
    public AIFeatureCardVM ScheduleCard { get; }
    public AIFeatureCardVM ChatCard { get; }

    private AIFeaturesViewMode _mode = AIFeaturesViewMode.Overview;
    public AIFeaturesViewMode Mode
    {
        get => _mode;
        private set
        {
            if (_mode == value) return;
            _mode = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsOverview));
            OnPropertyChanged(nameof(IsDetail));
        }
    }

    public bool IsOverview => _mode == AIFeaturesViewMode.Overview;
    public bool IsDetail => _mode == AIFeaturesViewMode.Detail;

    private string _activeSection = "overview";
    public string ActiveSection
    {
        get => _activeSection;
        private set
        {
            if (_activeSection == value) return;
            _activeSection = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ActiveSectionView));
        }
    }

    public string HeroStatusText => _ambient.AggregatedStatusText;

    /// <summary>
    /// Lazily-created, cached UserControl for the active section.
    /// Cache key = section name. Null when in Overview mode.
    /// </summary>
    public UserControl? ActiveSectionView
    {
        get
        {
            if (_mode == AIFeaturesViewMode.Overview) return null;
            return GetOrCreateSectionView(_activeSection);
        }
    }

    public ICommand NavigateToSection { get; }
    public ICommand NavigateToOverview { get; }

    /// <summary>
    /// Factory that creates section UserControls on demand.
    /// Swapped by the View's code-behind (via DI) before it's first accessed.
    /// </summary>
    public Func<string, UserControl>? SectionViewFactory { get; set; }

    /// <summary>
    /// Wired by the View's code-behind to open the ChatOptInDialog when user enables Chat
    /// without having completed opt-in. Returns true if opt-in completed.
    /// </summary>
    public Func<Task<bool>>? ChatOptInOpener { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void WireToggleHandlers()
    {
        InsightsCard.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AIFeatureCardVM.IsEnabled))
            {
                _settings.InsightsEnabled = InsightsCard.IsEnabled;
                _settings.Save();
                _ambient.Refresh();
            }
        };
        RecommendationsCard.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AIFeatureCardVM.IsEnabled))
            {
                _settings.RecommendationsEnabled = RecommendationsCard.IsEnabled;
                _settings.Save();
                _ambient.Refresh();
            }
        };
        ScheduleCard.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AIFeatureCardVM.IsEnabled))
            {
                _settings.ScheduleEnabled = ScheduleCard.IsEnabled;
                _settings.Save();
                _ambient.Refresh();
            }
        };
        ChatCard.PropertyChanged += async (_, e) =>
        {
            if (e.PropertyName != nameof(AIFeatureCardVM.IsEnabled)) return;
            // Programmatic syncs (SyncChatCardFromSettings) don't go through opt-in.
            if (_suppressChatToggleHandler) return;

            if (ChatCard.IsEnabled)
            {
                // User is trying to enable chat. Check if opt-in flow is required.
                if (_settings.ActiveChatModelId is null || !_settings.ChatOptInAcknowledged)
                {
                    // Revert toggle until opt-in completes
                    SyncChatCardFromSettings(desired: false);
                    var opened = await OpenChatOptInDialogAsync();
                    if (opened)
                    {
                        // Opt-in flow already set ChatEnabled = true via CompleteFromStep2
                        SyncChatCardFromSettings(desired: true);
                    }
                    return;
                }
            }

            _settings.ChatEnabled = ChatCard.IsEnabled;
            _settings.Save();
            _ambient.Refresh();
        };
    }

    private async void OnNavigateToSection(string? section)
    {
        if (string.IsNullOrEmpty(section)) return;
        if (section == "overview")
        {
            SetMode(AIFeaturesViewMode.Overview, "overview");
            return;
        }

        // Chat requires opt-in acknowledgement + an active model. If either is
        // missing (or the user toggled Chat off after opting in), route through
        // the same ChatOptInOpener the card toggle uses. This closes a bypass
        // where users could reach ChatSection from the detail-mode sidebar
        // without ever seeing the experimental warning or picking a model.
        //
        // Other sections (Insights / Recommendations / Schedule) don't have
        // prerequisites, so they navigate directly even when their toggle is
        // OFF — the section content itself can prompt the user to enable it.
        if (section == "chat" && !_settings.ChatEnabled)
        {
            if (ChatOptInOpener is not null)
            {
                var opened = await OpenChatOptInDialogAsync();
                if (!opened) return; // user cancelled — stay in current mode
                // Opt-in flow set ChatEnabled = true in settings; sync the card.
                SyncChatCardFromSettings(desired: true);
            }
            // If opener isn't wired (design-time / tests that don't mock it),
            // fall through so the test can still verify nav mechanics.
        }

        SetMode(AIFeaturesViewMode.Detail, section);
    }

    /// <summary>
    /// Programmatic flip of ChatCard.IsEnabled that bypasses the toggle handler.
    /// Used when the opt-in flow has authoritatively set settings.ChatEnabled and
    /// the UI card needs to catch up without re-triggering a fresh opt-in prompt.
    /// </summary>
    private void SyncChatCardFromSettings(bool desired)
    {
        _suppressChatToggleHandler = true;
        try { ChatCard.IsEnabled = desired; }
        finally { _suppressChatToggleHandler = false; }
    }

    private void SetMode(AIFeaturesViewMode mode, string section)
    {
        ActiveSection = section;
        Mode = mode;
    }

    private UserControl GetOrCreateSectionView(string section)
    {
        if (_sectionViewCache.TryGetValue(section, out var existing))
            return existing;

        if (SectionViewFactory is null)
        {
            // Fallback — empty placeholder. Real factory wired by View's code-behind.
            var placeholder = new UserControl();
            _sectionViewCache[section] = placeholder;
            return placeholder;
        }

        var view = SectionViewFactory(section);
        _sectionViewCache[section] = view;
        return view;
    }

    private async Task<bool> OpenChatOptInDialogAsync()
    {
        if (ChatOptInOpener is null) return false;
        return await ChatOptInOpener();
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

/// <summary>
/// Minimal ICommand implementation with a typed parameter.
/// </summary>
public sealed class DelegateCommand<T> : ICommand
{
    private readonly Action<T?> _execute;
    private readonly Func<T?, bool>? _canExecute;

    public DelegateCommand(Action<T?> execute, Func<T?, bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke((T?)parameter) ?? true;
    public void Execute(object? parameter) => _execute((T?)parameter);
    public event EventHandler? CanExecuteChanged;
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
