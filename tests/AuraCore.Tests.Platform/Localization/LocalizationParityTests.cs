using AuraCore.UI.Avalonia;
using FluentAssertions;
using System.Reflection;
using Xunit;

namespace AuraCore.Tests.Platform.Localization;

public class LocalizationParityTests
{
    private static IReadOnlyDictionary<string, string> GetDictionary(string fieldName)
    {
        var field = typeof(LocalizationService).GetField(fieldName,
            BindingFlags.NonPublic | BindingFlags.Static);
        field.Should().NotBeNull($"{fieldName} field must exist on LocalizationService");
        return (IReadOnlyDictionary<string, string>)field!.GetValue(null)!;
    }

    [Fact]
    public void EN_and_TR_dictionaries_have_identical_key_sets()
    {
        var en = GetDictionary("EN");
        var tr = GetDictionary("TR");

        var enOnly = en.Keys.Except(tr.Keys).OrderBy(k => k).ToList();
        var trOnly = tr.Keys.Except(en.Keys).OrderBy(k => k).ToList();

        enOnly.Should().BeEmpty("EN has keys TR lacks: " + string.Join(", ", enOnly));
        trOnly.Should().BeEmpty("TR has keys EN lacks: " + string.Join(", ", trOnly));
    }

    [Fact]
    public void Both_dictionaries_are_non_empty()
    {
        GetDictionary("EN").Should().NotBeEmpty();
        GetDictionary("TR").Should().NotBeEmpty();
    }
}
