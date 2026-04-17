using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace AuraCore.UI.Avalonia.ViewModels;

/// <summary>
/// View-model for a single feature card shown in the AIFeaturesView overview grid.
/// Four instances: Insights, Recommendations, Schedule, Chat.
/// </summary>
public sealed class AIFeatureCardVM : INotifyPropertyChanged
{
    public AIFeatureCardVM(
        string key,
        string title,
        string accentColor,
        string iconKey,
        bool isChatExperimental)
    {
        Key = key;
        Title = title;
        AccentColor = accentColor;
        IconKey = iconKey;
        IsChatExperimental = isChatExperimental;
    }

    /// <summary>Stable identifier: "insights" | "recommendations" | "schedule" | "chat".</summary>
    public string Key { get; }

    /// <summary>Localized title displayed in the card.</summary>
    public string Title { get; }

    /// <summary>Resource key for the accent color brush (e.g. "AccentPurple").</summary>
    public string AccentColor { get; }

    /// <summary>Resource key for the icon geometry.</summary>
    public string IconKey { get; }

    /// <summary>True only for the Chat card — shows EXPERIMENTAL badge.</summary>
    public bool IsChatExperimental { get; }

    // Observable properties:
    private bool _isEnabled;
    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetProp(ref _isEnabled, value);
    }

    private string _previewSummary = "";
    public string PreviewSummary
    {
        get => _previewSummary;
        set => SetProp(ref _previewSummary, value);
    }

    private string? _highlightText;
    public string? HighlightText
    {
        get => _highlightText;
        set => SetProp(ref _highlightText, value);
    }

    private string? _highlightIcon;
    public string? HighlightIcon
    {
        get => _highlightIcon;
        set => SetProp(ref _highlightIcon, value);
    }

    /// <summary>Command fired when the card body (not the toggle) is clicked.
    /// Wired by AIFeaturesViewModel to navigate to the detail section.</summary>
    public ICommand? NavigateToDetail { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool SetProp<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
