using Avalonia;
using Avalonia.Data.Converters;
using System.Globalization;

namespace AuraCore.UI.Avalonia.Converters;

/// <summary>
/// Converts <see cref="Rect"/> (typically from <c>Window.Bounds</c>) to
/// a boolean indicating narrow-mode state. Converter parameter is the
/// threshold in DIPs (default 1000). Used for one-off XAML responsiveness
/// where adding a VM property is overkill.
/// </summary>
public sealed class BoundsToIsNarrowModeConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not Rect rect) return false;

        double threshold = 1000;
        if (parameter is string s && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            threshold = parsed;
        else if (parameter is double d)
            threshold = d;

        return rect.Width < threshold;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException("BoundsToIsNarrowModeConverter is one-way only.");
}
