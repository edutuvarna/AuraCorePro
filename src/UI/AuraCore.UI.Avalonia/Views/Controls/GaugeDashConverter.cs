using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Collections;
using Avalonia.Data.Converters;

namespace AuraCore.UI.Avalonia.Views.Controls;

/// <summary>Converts a 0-100 value into a StrokeDashArray that shows that fraction of a 56px-diameter circle's circumference.</summary>
public sealed class GaugeDashConverter : IValueConverter
{
    public static readonly GaugeDashConverter Instance = new();

    private const double Circumference = Math.PI * 56.0 / 6.0;

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var pct = value is double d ? d : 0;
        pct = Math.Clamp(pct, 0, 100);
        var on = Circumference * (pct / 100.0);
        var off = Circumference - on;
        return new AvaloniaList<double> { on, off };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
