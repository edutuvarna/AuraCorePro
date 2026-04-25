using System.Reflection;
using Xunit;

namespace AuraCore.Tests.API.SuperadminFoundation;

/// <summary>
/// Phase 6.12.W3.T5 — verifies that ResendEmailService.ApplyPlaceholders
/// HTML-encodes every placeholder value before substituting into the
/// template, preventing XSS via free-text admin inputs (reason, reviewNote)
/// that flow into permission-event email bodies.
///
/// ApplyPlaceholders is private; test reaches it via reflection. This is
/// acceptable for a single-method security-critical helper — the alternative
/// (extract to a public utility class) would over-expand surface area for
/// one test.
/// </summary>
public class EmailServiceXssTests
{
    private static string Apply(string template, object data)
    {
        var t = typeof(AuraCore.API.Infrastructure.Services.Email.ResendEmailService);
        var m = t.GetMethod("ApplyPlaceholders", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ApplyPlaceholders not found");
        return (string)m.Invoke(null, new object[] { template, data })!;
    }

    public sealed record Data(string reason);

    [Fact]
    public void Encodes_script_tag_payload()
    {
        var output = Apply("body: {{reason}}", new Data("<script>alert(1)</script>"));
        Assert.Equal("body: &lt;script&gt;alert(1)&lt;/script&gt;", output);
        Assert.DoesNotContain("<script>", output);
    }

    [Fact]
    public void Encodes_all_reserved_chars()
    {
        var output = Apply("{{reason}}", new Data("a & b < c > d \" e ' f"));
        Assert.Contains("&amp;", output);
        Assert.Contains("&lt;", output);
        Assert.Contains("&gt;", output);
        Assert.Contains("&quot;", output);
        Assert.Contains("&#39;", output);
    }

    [Fact]
    public void Plain_text_passes_through_unchanged()
    {
        var output = Apply("Hello {{reason}}", new Data("admin needs access"));
        Assert.Equal("Hello admin needs access", output);
    }

    [Fact]
    public void Null_value_renders_empty_string()
    {
        var output = Apply("[{{reason}}]", new Data(null!));
        Assert.Equal("[]", output);
    }
}
