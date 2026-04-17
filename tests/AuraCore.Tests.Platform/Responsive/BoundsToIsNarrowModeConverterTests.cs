using Avalonia;
using AuraCore.UI.Avalonia.Converters;
using FluentAssertions;
using System.Globalization;
using Xunit;

namespace AuraCore.Tests.Platform.Responsive;

public class BoundsToIsNarrowModeConverterTests
{
    private static readonly BoundsToIsNarrowModeConverter Converter = new();

    [Theory]
    [InlineData(1200.0, "1000", false)]   // 1200 >= 1000 -> wide
    [InlineData(999.0,  "1000", true)]    // 999 < 1000 -> narrow
    [InlineData(900.0,  "1000", true)]
    [InlineData(899.0,  "900",  true)]    // 899 < 900 -> very narrow
    [InlineData(900.0,  "900",  false)]   // 900 >= 900 boundary
    public void Convert_returns_expected_bool(double width, string parameter, bool expected)
    {
        var rect = new Rect(0, 0, width, 600);
        var result = Converter.Convert(rect, typeof(bool), parameter, CultureInfo.InvariantCulture);
        result.Should().Be(expected);
    }

    [Fact]
    public void Convert_with_null_value_returns_false()
    {
        var result = Converter.Convert(null, typeof(bool), "1000", CultureInfo.InvariantCulture);
        result.Should().Be(false);
    }

    [Fact]
    public void Convert_with_non_rect_value_returns_false()
    {
        var result = Converter.Convert("not a rect", typeof(bool), "1000", CultureInfo.InvariantCulture);
        result.Should().Be(false);
    }

    [Fact]
    public void Convert_with_missing_parameter_defaults_to_1000()
    {
        var rect = new Rect(0, 0, 950, 600);
        var result = Converter.Convert(rect, typeof(bool), null, CultureInfo.InvariantCulture);
        result.Should().Be(true);   // 950 < 1000 default threshold
    }

    [Fact]
    public void ConvertBack_throws_NotSupportedException()
    {
        Action act = () => Converter.ConvertBack(true, typeof(Rect), "1000", CultureInfo.InvariantCulture);
        act.Should().Throw<NotSupportedException>();
    }
}
