using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace AuraCore.UI.Avalonia.Views.Controls;

public partial class Gauge : UserControl
{
    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<Gauge, double>(nameof(Value), 0.0);

    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<Gauge, string>(nameof(Label), "CPU");

    public static readonly StyledProperty<string> SubLabelProperty =
        AvaloniaProperty.Register<Gauge, string>(nameof(SubLabel), string.Empty);

    public static readonly StyledProperty<string> InsightProperty =
        AvaloniaProperty.Register<Gauge, string>(nameof(Insight), string.Empty);

    public static readonly StyledProperty<IBrush> RingBrushProperty =
        AvaloniaProperty.Register<Gauge, IBrush>(nameof(RingBrush), Brushes.Teal);

    public static readonly StyledProperty<IBrush> InsightBrushProperty =
        AvaloniaProperty.Register<Gauge, IBrush>(nameof(InsightBrush), Brushes.Gray);

    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<Gauge, Geometry?>(nameof(Icon));

    public double Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public string SubLabel { get => GetValue(SubLabelProperty); set => SetValue(SubLabelProperty, value); }
    public string Insight { get => GetValue(InsightProperty); set => SetValue(InsightProperty, value); }
    public IBrush RingBrush { get => GetValue(RingBrushProperty); set => SetValue(RingBrushProperty, value); }
    public IBrush InsightBrush { get => GetValue(InsightBrushProperty); set => SetValue(InsightBrushProperty, value); }
    public Geometry? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }

    public Gauge()
    {
        InitializeComponent();
    }
}
