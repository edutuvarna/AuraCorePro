using AuraCore.API.Helpers;
using System.Text.RegularExpressions;
using Xunit;

namespace AuraCore.Tests.API.AdminRebuild;

public class LicenseKeyGeneratorTests
{
    [Fact]
    public void Generate_returns_AC_prefix_with_4_dash_separated_quads()
    {
        var key = LicenseKeyGenerator.Generate();
        Assert.Matches(@"^AC-[A-F0-9]{4}-[A-F0-9]{4}-[A-F0-9]{4}-[A-F0-9]{4}$", key);
    }

    [Fact]
    public void Generate_keys_are_unique_across_1000_invocations()
    {
        var keys = Enumerable.Range(0, 1000).Select(_ => LicenseKeyGenerator.Generate()).ToHashSet();
        Assert.Equal(1000, keys.Count);
    }

    [Theory]
    [InlineData("AC-1234-5678-90AB-CDEF", true)]
    [InlineData("AC-FFFF-0000-A1B2-C3D4", true)]
    [InlineData("c8a91e2d4f7b3091b2a45e8c3d6f9012", true)]  // Legacy 32-char hex
    [InlineData("invalid", false)]
    [InlineData("AC-1234-5678", false)]
    [InlineData("", false)]
    public void License_key_validation_regex_accepts_both_formats(string key, bool expectedValid)
    {
        var pattern = new Regex(@"^(AC-[A-F0-9]{4}-[A-F0-9]{4}-[A-F0-9]{4}-[A-F0-9]{4}|[a-f0-9]{32})$");
        Assert.Equal(expectedValid, pattern.IsMatch(key));
    }
}
