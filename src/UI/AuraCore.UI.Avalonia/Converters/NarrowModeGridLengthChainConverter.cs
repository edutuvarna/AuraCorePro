using Avalonia.Controls;
using Avalonia.Data.Converters;
using System.Collections.Generic;
using System.Globalization;

namespace AuraCore.UI.Avalonia.Converters;

/// <summary>
/// Multi-value converter that maps [IsNarrow, IsVeryNarrow] to a
/// <see cref="GridLength"/>, enabling 3-state column width switching.
///
/// ConverterParameter: <c>"veryNarrowValue,narrowValue,wideValue"</c>
/// Default parameter: <c>"0,80,280"</c>.
///
/// Each value can be a fixed DIP count, "Auto", or "*".
///
/// Logic:
/// - IsVeryNarrow=true  ->  veryNarrowValue (e.g. 0, sidebar hidden)
/// - IsNarrow=true only ->  narrowValue     (e.g. 80, icon-only)
/// - both false         ->  wideValue       (e.g. 280, full sidebar)
///
/// Used by Phase 5.3 AIFeaturesView to drive the detail sidebar width.
/// </summary>
public sealed class NarrowModeGridLengthChainConverter : IMultiValueConverter
{
    // values: [IsNarrow, IsVeryNarrow]
    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        var p = (parameter as string ?? "0,80,280").Split(',');
        if (p.Length != 3) return GridLength.Auto;

        bool veryNarrow = values.Count > 1 && values[1] is bool v && v;
        bool narrow     = values.Count > 0 && values[0] is bool n && n;

        var pick = veryNarrow ? p[0].Trim()
                 : narrow     ? p[1].Trim()
                              : p[2].Trim();
        return GridLength.Parse(pick);
    }
}
