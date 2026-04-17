using Avalonia.Controls;
using Avalonia.Data.Converters;
using System.Globalization;

namespace AuraCore.UI.Avalonia.Converters;

/// <summary>
/// Maps a bool to a <see cref="GridLength"/>. ConverterParameter format:
/// <c>"trueValue,falseValue"</c>, e.g. <c>"0,280"</c> or <c>"80,Auto"</c>.
/// Either side can be a fixed DIP count, "Auto", or "*".
/// Used by Phase 5.3 narrow-mode layouts (e.g., AIFeaturesView sidebar
/// width collapses 280 -> 80 -> 0 DIPs at thresholds).
/// </summary>
public sealed class BoolToGridLengthConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not bool b) return GridLength.Auto;
        if (parameter is not string p) return GridLength.Auto;
        var parts = p.Split(',');
        if (parts.Length != 2) return GridLength.Auto;
        var pick = b ? parts[0].Trim() : parts[1].Trim();
        return GridLength.Parse(pick);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException("BoolToGridLengthConverter is one-way only.");
}
