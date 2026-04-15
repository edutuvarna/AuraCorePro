using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AuraCore.UI.Avalonia.Services.AI;

public sealed class CortexAmbientService : ICortexAmbientService
{
    private readonly AppSettings _settings;

    private bool _anyFeatureEnabled;
    private int _enabledFeatureCount;
    private int _learningDay;
    private CortexActiveness _activeness;
    private string _aggregatedStatusText = "";

    public CortexAmbientService(AppSettings settings)
    {
        _settings = settings;
        Recompute();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public bool AnyFeatureEnabled => _anyFeatureEnabled;
    public int EnabledFeatureCount => _enabledFeatureCount;
    public int TotalFeatureCount => 4;
    public int LearningDay => _learningDay;
    public CortexActiveness Activeness => _activeness;
    public string AggregatedStatusText => _aggregatedStatusText;
    public string FormattedStatusText => $"\u2728 Cortex \u00B7 {_aggregatedStatusText}";

    public void Refresh()
    {
        var prevAny = _anyFeatureEnabled;
        var prevCount = _enabledFeatureCount;
        var prevDay = _learningDay;
        var prevActiveness = _activeness;
        var prevText = _aggregatedStatusText;

        // Stamp AIFirstEnabledAt on first transition to enabled
        var anyNow = _settings.InsightsEnabled || _settings.RecommendationsEnabled
                     || _settings.ScheduleEnabled || _settings.ChatEnabled;
        if (anyNow && _settings.AIFirstEnabledAt is null)
        {
            _settings.AIFirstEnabledAt = DateTime.UtcNow;
            _settings.Save();
        }

        Recompute();

        if (prevAny != _anyFeatureEnabled) Fire(nameof(AnyFeatureEnabled));
        if (prevCount != _enabledFeatureCount) Fire(nameof(EnabledFeatureCount));
        if (prevDay != _learningDay) Fire(nameof(LearningDay));
        if (prevActiveness != _activeness) Fire(nameof(Activeness));
        if (prevText != _aggregatedStatusText) Fire(nameof(AggregatedStatusText));
    }

    private void Recompute()
    {
        _enabledFeatureCount = BoolToInt(_settings.InsightsEnabled)
                             + BoolToInt(_settings.RecommendationsEnabled)
                             + BoolToInt(_settings.ScheduleEnabled)
                             + BoolToInt(_settings.ChatEnabled);
        _anyFeatureEnabled = _enabledFeatureCount > 0;

        _learningDay = ComputeLearningDay(_settings.AIFirstEnabledAt);
        _activeness = ComputeActiveness();
        _aggregatedStatusText = ComputeStatusText();
    }

    private static int BoolToInt(bool b) => b ? 1 : 0;

    private static int ComputeLearningDay(DateTime? firstEnabledAt)
    {
        if (firstEnabledAt is null) return 0;
        var days = (int)(DateTime.UtcNow - firstEnabledAt.Value).TotalDays;
        return Math.Max(0, days);
    }

    private CortexActiveness ComputeActiveness()
    {
        if (_anyFeatureEnabled) return CortexActiveness.Active;
        if (_settings.AIFirstEnabledAt is not null) return CortexActiveness.Paused;
        return CortexActiveness.Ready;
    }

    private string ComputeStatusText() => _activeness switch
    {
        CortexActiveness.Active => $"Active · Learning day {Math.Max(1, _learningDay)}",
        CortexActiveness.Paused => "Paused",
        _ => "Ready to start",
    };

    private void Fire([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
