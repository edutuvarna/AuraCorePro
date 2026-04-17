using Avalonia.Controls;
using AuraCore.UI.Avalonia.Converters;
using FluentAssertions;
using System.Globalization;
using Xunit;

namespace AuraCore.Tests.Platform.Responsive;

/// <summary>
/// Tests for <see cref="NarrowModeGridLengthChainConverter"/>.
/// Inputs: [IsNarrow, IsVeryNarrow]; parameter: "veryNarrow,narrow,wide"
/// (default "0,80,280")
/// </summary>
public class NarrowModeGridLengthChainConverterTests
{
    private static readonly NarrowModeGridLengthChainConverter Converter = new();

    // Default parameter "0,80,280"
    // IsNarrow=false, IsVeryNarrow=false -> 280 (wide)
    // IsNarrow=true,  IsVeryNarrow=false -> 80  (narrow)
    // IsNarrow=true,  IsVeryNarrow=true  -> 0   (very-narrow)
    // IsNarrow=false, IsVeryNarrow=true  -> 0   (very-narrow wins even if inconsistent)
    [Theory]
    [InlineData(false, false, null,        280.0)]  // wide, default param
    [InlineData(true,  false, null,        80.0)]   // narrow
    [InlineData(true,  true,  null,        0.0)]    // very-narrow
    [InlineData(false, true,  null,        0.0)]    // inconsistent — very-narrow wins
    [InlineData(false, false, "0,80,280",  280.0)]  // explicit param, wide
    [InlineData(true,  false, "0,80,280",  80.0)]   // explicit param, narrow
    [InlineData(true,  true,  "0,80,280",  0.0)]    // explicit param, very-narrow
    [InlineData(false, false, "0,120,200", 200.0)]  // custom wide value
    [InlineData(true,  false, "0,120,200", 120.0)]  // custom narrow value
    [InlineData(true,  true,  "0,120,200", 0.0)]    // custom very-narrow
    public void Convert_returns_correct_GridLength(
        bool isNarrow, bool isVeryNarrow, string? param, double expectedDip)
    {
        var result = Converter.Convert(
            new List<object?> { isNarrow, isVeryNarrow },
            typeof(GridLength),
            param,
            CultureInfo.InvariantCulture);

        result.Should().BeOfType<GridLength>();
        var gl = (GridLength)result!;
        gl.Value.Should().BeApproximately(expectedDip, 0.001);
        gl.GridUnitType.Should().Be(GridUnitType.Pixel);
    }

    [Fact]
    public void Convert_with_empty_values_uses_wide_slot_of_default_param()
    {
        // Empty list: both bools treated as false -> wide (280 default)
        var result = Converter.Convert(
            new List<object?>(),
            typeof(GridLength),
            null,
            CultureInfo.InvariantCulture);

        result.Should().BeOfType<GridLength>();
        ((GridLength)result!).Value.Should().BeApproximately(280.0, 0.001);
    }

    [Fact]
    public void Convert_with_non_bool_inputs_uses_wide_fallback()
    {
        // Non-bool values treated as false -> wide
        var result = Converter.Convert(
            new List<object?> { "not a bool", null },
            typeof(GridLength),
            null,
            CultureInfo.InvariantCulture);

        result.Should().BeOfType<GridLength>();
        ((GridLength)result!).Value.Should().BeApproximately(280.0, 0.001);
    }

    [Fact]
    public void Convert_with_bad_parameter_format_returns_AutoGridLength()
    {
        // Parameter must have exactly 3 comma-separated parts
        var result = Converter.Convert(
            new List<object?> { true, false },
            typeof(GridLength),
            "only-two-parts,here",
            CultureInfo.InvariantCulture);

        result.Should().BeOfType<GridLength>();
        ((GridLength)result!).GridUnitType.Should().Be(GridUnitType.Auto);
    }
}
