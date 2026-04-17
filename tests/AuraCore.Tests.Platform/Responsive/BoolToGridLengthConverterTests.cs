using Avalonia.Controls;
using AuraCore.UI.Avalonia.Converters;
using FluentAssertions;
using System.Globalization;
using Xunit;

namespace AuraCore.Tests.Platform.Responsive;

public class BoolToGridLengthConverterTests
{
    private static readonly BoolToGridLengthConverter Converter = new();

    // true -> pick first value of "trueVal,falseVal"
    [Theory]
    [InlineData(true,  "80,280",  80.0,  GridUnitType.Pixel)]
    [InlineData(false, "80,280",  280.0, GridUnitType.Pixel)]
    [InlineData(true,  "0,Auto",  0.0,   GridUnitType.Pixel)]
    public void Convert_picks_correct_GridLength(bool input, string param, double expectedValue, GridUnitType expectedUnit)
    {
        var result = Converter.Convert(input, typeof(GridLength), param, CultureInfo.InvariantCulture);
        result.Should().BeOfType<GridLength>();
        var gl = (GridLength)result!;
        gl.Value.Should().BeApproximately(expectedValue, 0.001);
        gl.GridUnitType.Should().Be(expectedUnit);
    }

    [Fact]
    public void Convert_false_with_Auto_param_returns_AutoGridLength()
    {
        var result = Converter.Convert(false, typeof(GridLength), "0,Auto", CultureInfo.InvariantCulture);
        result.Should().BeOfType<GridLength>();
        var gl = (GridLength)result!;
        gl.GridUnitType.Should().Be(GridUnitType.Auto);
    }

    [Fact]
    public void Convert_null_value_returns_AutoGridLength()
    {
        var result = Converter.Convert(null, typeof(GridLength), "80,280", CultureInfo.InvariantCulture);
        result.Should().BeOfType<GridLength>();
        ((GridLength)result!).GridUnitType.Should().Be(GridUnitType.Auto);
    }

    [Fact]
    public void Convert_null_parameter_returns_AutoGridLength()
    {
        var result = Converter.Convert(true, typeof(GridLength), null, CultureInfo.InvariantCulture);
        result.Should().BeOfType<GridLength>();
        ((GridLength)result!).GridUnitType.Should().Be(GridUnitType.Auto);
    }

    [Fact]
    public void Convert_bad_parameter_format_returns_AutoGridLength()
    {
        // Parameter must be "trueVal,falseVal" — single value is malformed
        var result = Converter.Convert(true, typeof(GridLength), "120", CultureInfo.InvariantCulture);
        result.Should().BeOfType<GridLength>();
        ((GridLength)result!).GridUnitType.Should().Be(GridUnitType.Auto);
    }

    [Fact]
    public void ConvertBack_throws_NotSupportedException()
    {
        Action act = () => Converter.ConvertBack(
            new GridLength(80), typeof(bool), "80,280", CultureInfo.InvariantCulture);
        act.Should().Throw<NotSupportedException>();
    }
}
