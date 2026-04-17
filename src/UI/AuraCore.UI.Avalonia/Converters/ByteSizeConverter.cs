using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace AuraCore.UI.Avalonia.Converters;

/// <summary>
/// Formats a <c>long</c> byte-count into a human-readable size string
/// (e.g. 1536 → "1.5 KB"). Returns "—" for null or negative values.
/// </summary>
public sealed class ByteSizeConverter : IValueConverter
{
    public static readonly ByteSizeConverter Instance = new();

    private static readonly string[] Suffixes = { "B", "KB", "MB", "GB", "TB", "PB" };

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not long bytes || bytes < 0)
            return "—";

        double size = bytes;
        int idx = 0;
        while (size >= 1024 && idx < Suffixes.Length - 1)
        {
            size /= 1024;
            idx++;
        }
        return $"{size:0.##} {Suffixes[idx]}";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException("ByteSizeConverter is one-way only.");
}
