using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace AuraCore.UI.Avalonia.Views.Controls;

public partial class AccentBadge : UserControl
{
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<AccentBadge, string>(nameof(Label), string.Empty);
    public static readonly StyledProperty<IBrush> AccentBrushProperty =
        AvaloniaProperty.Register<AccentBadge, IBrush>(nameof(AccentBrush), Brushes.Violet);

    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public IBrush AccentBrush { get => GetValue(AccentBrushProperty); set => SetValue(AccentBrushProperty, value); }

    public AccentBadge() { InitializeComponent(); }
}
