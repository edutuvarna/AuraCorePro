using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace AuraCore.UI.Avalonia.Views.Controls;

public partial class StatusChip : UserControl
{
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<StatusChip, string>(nameof(Label), string.Empty);
    public static readonly StyledProperty<IBrush> AccentBrushProperty =
        AvaloniaProperty.Register<StatusChip, IBrush>(nameof(AccentBrush), Brushes.Teal);
    public static readonly StyledProperty<bool> ShowDotProperty =
        AvaloniaProperty.Register<StatusChip, bool>(nameof(ShowDot), true);
    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<StatusChip, Geometry?>(nameof(Icon));

    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public IBrush AccentBrush { get => GetValue(AccentBrushProperty); set => SetValue(AccentBrushProperty, value); }
    public bool ShowDot { get => GetValue(ShowDotProperty); set => SetValue(ShowDotProperty, value); }
    public Geometry? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }

    public StatusChip() { InitializeComponent(); }
}
