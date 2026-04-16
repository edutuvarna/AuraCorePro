using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Media;

namespace AuraCore.UI.Avalonia.Views.Controls;

/// <summary>
/// Phase 4.0 Module Page stat card — uppercase label + large bold value colored
/// by a bindable <see cref="ValueBrush"/>. Matches the <see cref="StatusChip.AccentBrush"/>
/// IBrush DP pattern. Default ValueBrush is <see cref="Brushes.White"/> (fallback);
/// callers should pass <c>{DynamicResource TextPrimaryBrush}</c> or similar.
/// Spec §4.2. Theme V2 tokens only.
/// </summary>
public partial class StatCard : UserControl
{
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<StatCard, string>(nameof(Label), string.Empty);

    public static readonly StyledProperty<string> ValueProperty =
        AvaloniaProperty.Register<StatCard, string>(nameof(Value), "--");

    public static readonly StyledProperty<IBrush> ValueBrushProperty =
        AvaloniaProperty.Register<StatCard, IBrush>(nameof(ValueBrush), Brushes.White);

    public string Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public string Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public IBrush ValueBrush
    {
        get => GetValue(ValueBrushProperty);
        set => SetValue(ValueBrushProperty, value);
    }

    public StatCard()
    {
        InitializeComponent();
    }
}
