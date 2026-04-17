using AuraCore.UI.Avalonia.Converters;
using FluentAssertions;
using System.Globalization;
using Xunit;

namespace AuraCore.Tests.Platform.Responsive;

public class NarrowToColumnCountConverterTests
{
    private static readonly NarrowToColumnCountConverter Converter = new();

    // Inputs: [IsNarrow, IsVeryNarrow] -> 1 | 2 | 4
    [Theory]
    [InlineData(false, false, 4)]  // wide
    [InlineData(true,  false, 2)]  // narrow (but not very)
    [InlineData(true,  true,  1)]  // very narrow
    [InlineData(false, true,  1)]  // impossible in practice but very-narrow wins if inconsistent
    public void Convert_returns_expected_column_count(bool isNarrow, bool isVeryNarrow, int expected)
    {
        var result = Converter.Convert(
            new List<object?> { isNarrow, isVeryNarrow },
            typeof(int), parameter: null, CultureInfo.InvariantCulture);
        result.Should().Be(expected);
    }

    [Fact]
    public void Convert_with_non_bool_inputs_returns_wide_fallback()
    {
        var result = Converter.Convert(
            new List<object?> { "not a bool", null },
            typeof(int), null, CultureInfo.InvariantCulture);
        result.Should().Be(4);
    }

    [Fact]
    public void Convert_with_empty_values_returns_wide_fallback()
    {
        var result = Converter.Convert(
            new List<object?>(),
            typeof(int), null, CultureInfo.InvariantCulture);
        result.Should().Be(4);
    }
}
