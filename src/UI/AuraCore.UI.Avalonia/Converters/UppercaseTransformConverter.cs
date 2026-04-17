using Avalonia.Data.Converters;
using System.Globalization;

namespace AuraCore.UI.Avalonia.Converters;

/// <summary>
/// One-way value converter that maps an input to its culture-invariant
/// uppercase string form. Null passes through as null; non-string inputs
/// are coerced via <c>ToString()</c> before uppercasing. Culture parameter
/// is ignored — we deliberately use <c>ToUpperInvariant</c> for
/// cross-locale consistency.
/// </summary>
public sealed class UppercaseTransformConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null) return null;
        var s = value as string ?? value.ToString() ?? string.Empty;
        return s.ToUpperInvariant();
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException("UppercaseTransformConverter is one-way only.");
}
