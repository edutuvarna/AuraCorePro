using AuraCore.UI.Avalonia.Converters;
using FluentAssertions;
using System.Globalization;
using Xunit;

namespace AuraCore.Tests.Platform.Converters;

public class UppercaseTransformConverterTests
{
    private static readonly UppercaseTransformConverter C = new();
    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

    [Fact]
    public void Convert_uppercases_standard_ascii()
    {
        C.Convert("Hello World", typeof(string), null, Culture).Should().Be("HELLO WORLD");
    }

    [Fact]
    public void Convert_preserves_already_upper()
    {
        C.Convert("ALREADY UPPER", typeof(string), null, Culture).Should().Be("ALREADY UPPER");
    }

    [Fact]
    public void Convert_handles_empty_string()
    {
        C.Convert("", typeof(string), null, Culture).Should().Be("");
    }

    [Fact]
    public void Convert_null_input_returns_null()
    {
        C.Convert(null, typeof(string), null, Culture).Should().BeNull();
    }

    [Fact]
    public void Convert_preserves_turkish_diacritics_in_uppercase_form()
    {
        // ToUpperInvariant is culture-neutral: ç→Ç, ğ→Ğ, ü→Ü, ş→Ş, ö→Ö, ı→I.
        // (Turkish-culture-aware uppercase would ı→İ; we use Invariant for
        // consistency across locales — the kicker text is typically short
        // English-like labels where this is fine.)
        C.Convert("değişiklik", typeof(string), null, Culture).Should().Be("DEĞIŞIKLIK");
    }

    [Fact]
    public void Convert_non_string_input_calls_ToString()
    {
        C.Convert(42, typeof(string), null, Culture).Should().Be("42");
    }

    [Fact]
    public void ConvertBack_throws_NotSupported()
    {
        Action act = () => C.ConvertBack("X", typeof(string), null, Culture);
        act.Should().Throw<NotSupportedException>();
    }
}
