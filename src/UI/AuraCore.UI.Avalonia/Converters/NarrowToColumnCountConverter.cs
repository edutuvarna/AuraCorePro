using Avalonia.Data.Converters;
using System.Collections.Generic;
using System.Globalization;

namespace AuraCore.UI.Avalonia.Converters;

/// <summary>
/// Multi-value converter that reads two bools ([IsNarrow, IsVeryNarrow])
/// and returns a column count suitable for <c>UniformGrid.Columns</c>.
///
/// - both false            -> 4
/// - IsNarrow only         -> 2
/// - IsVeryNarrow          -> 1
///
/// Used by StatRow and any other UniformGrid-based control that reflows
/// 4 -> 2 -> 1 columns at the two thresholds.
/// </summary>
public sealed class NarrowToColumnCountConverter : IMultiValueConverter
{
    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count == 0) return 4;

        bool isVeryNarrow = values.Count > 1 && values[1] is bool v && v;
        if (isVeryNarrow) return 1;

        bool isNarrow = values[0] is bool n && n;
        return isNarrow ? 2 : 4;
    }
}
