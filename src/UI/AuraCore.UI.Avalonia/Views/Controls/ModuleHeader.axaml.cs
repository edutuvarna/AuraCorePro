using global::Avalonia;
using global::Avalonia.Controls;

namespace AuraCore.UI.Avalonia.Views.Controls;

/// <summary>
/// Phase 4.0 Module Page shell header — title + optional subtitle on the left,
/// optional right-aligned <see cref="Actions"/> slot for buttons / toggle clusters.
/// Spec §4.1. Theme V2 tokens only.
/// </summary>
public partial class ModuleHeader : UserControl
{
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<ModuleHeader, string>(nameof(Title), string.Empty);

    public static readonly StyledProperty<string?> SubtitleProperty =
        AvaloniaProperty.Register<ModuleHeader, string?>(nameof(Subtitle));

    public static readonly StyledProperty<object?> ActionsProperty =
        AvaloniaProperty.Register<ModuleHeader, object?>(nameof(Actions));

    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string? Subtitle
    {
        get => GetValue(SubtitleProperty);
        set => SetValue(SubtitleProperty, value);
    }

    public object? Actions
    {
        get => GetValue(ActionsProperty);
        set => SetValue(ActionsProperty, value);
    }

    public ModuleHeader()
    {
        InitializeComponent();
    }
}
