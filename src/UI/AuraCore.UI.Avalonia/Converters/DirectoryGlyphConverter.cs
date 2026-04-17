using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace AuraCore.UI.Avalonia.Converters;

/// <summary>
/// Maps a <c>bool</c> IsDirectory value to a folder or file icon glyph.
/// <c>true</c> → folder emoji; <c>false</c> → file emoji.
/// </summary>
public sealed class DirectoryGlyphConverter : IValueConverter
{
    public static readonly DirectoryGlyphConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool isDir && isDir ? "\uD83D\uDCC1" : "\uD83D\uDCC4";

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException("DirectoryGlyphConverter is one-way only.");
}
