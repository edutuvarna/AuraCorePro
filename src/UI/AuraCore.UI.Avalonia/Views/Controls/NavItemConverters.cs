using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace AuraCore.UI.Avalonia.Views.Controls;

public sealed class ActiveBackgroundConverter : IMultiValueConverter
{
    public static readonly ActiveBackgroundConverter Instance = new();
    public object? Convert(IList<object?> values, System.Type targetType, object? parameter, CultureInfo culture)
    {
        var isActive = values.Count > 0 && values[0] is bool b && b;
        var accent = values.Count > 1 && values[1] is ISolidColorBrush s ? s.Color : Color.Parse("#00D4AA");
        if (!isActive) return Brushes.Transparent;
        return new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(Color.FromArgb(0x1F, accent.R, accent.G, accent.B), 0),
                new GradientStop(Colors.Transparent, 1)
            }
        };
    }
}

public sealed class ActiveAccentConverter : IMultiValueConverter
{
    public static readonly ActiveAccentConverter Instance = new();
    public object? Convert(IList<object?> values, System.Type targetType, object? parameter, CultureInfo culture)
    {
        var isActive = values.Count > 0 && values[0] is bool b && b;
        var accent = values.Count > 1 && values[1] is IBrush br ? br : Brushes.Teal;
        return isActive ? accent : Brushes.Transparent;
    }
}

public sealed class ActiveForegroundConverter : IMultiValueConverter
{
    public static readonly ActiveForegroundConverter Instance = new();
    public object? Convert(IList<object?> values, System.Type targetType, object? parameter, CultureInfo culture)
    {
        var isActive = values.Count > 0 && values[0] is bool b && b;
        var accent = values.Count > 1 && values[1] is IBrush br ? br : Brushes.White;
        return isActive ? accent : new SolidColorBrush(Color.Parse("#D0D0DC"));
    }
}
