using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.Localization;

/// <summary>
/// Regression gate companion to HardcodedStringScannerTests. The XAML scanner
/// only covers .axaml attribute values; this scanner covers C# code-behind,
/// ViewModels, and services where UI-facing string literals are assigned
/// programmatically — e.g. <c>MyLabel.Text = "Hello"</c>, or
/// <c>penalties.Add("Disk low")</c>.
///
/// Motivated by Phase 6.4 Batch 2 discovery: InsightsSection.axaml.cs had
/// 3 Turkish literals that the XAML scanner missed (only caught because the
/// user read the diff manually). This gate closes that loop.
///
/// Three pattern classes are checked:
///  1. Direct property assignment:  .Text = "..."  (Text/Title/ToolTip/Content/Header/Label/Watermark)
///  2. ToolTip.SetTip attached call: ToolTip.SetTip(ctrl, "...")
///  3. Single-arg collection .Add("..."):  penalties.Add("Disk low")
///
/// Whitelist rules mirror the XAML scanner (short/acronym/pure-symbol/numeric)
/// plus resource-key-like strings (no spaces + contains dot/colon/dash/slash)
/// and identifier-like single-word tokens (CSS classes, action keys, role tokens).
/// </summary>
public class CSharpHardcodedStringScannerTests
{
    // Files that are known to contain English-by-design literals
    // that should NOT go through LocalizationService.
    //
    // ChatSection.axaml.cs: alerts.Add("CPU anomaly detected") and
    // alerts.Add("RAM anomaly detected") feed an LlmContext record that
    // is passed verbatim to the local LLM model as prompt context.
    // These are intentionally English — the AI model receives English
    // context regardless of the UI language setting.
    private static readonly HashSet<string> GrandfatheredFiles = new(StringComparer.OrdinalIgnoreCase)
    {
        "src/UI/AuraCore.UI.Avalonia/Views/Pages/AI/ChatSection.axaml.cs",
    };

    private static readonly Regex[] Patterns =
    {
        // 1. Direct property assignment to a UI-visible property
        new(@"\.(Text|Title|ToolTip|Content|Header|Label|Watermark)\s*=\s*""([^""]+)""",
            RegexOptions.Compiled),
        // 2. Attached property ToolTip.SetTip(control, "...")
        new(@"ToolTip\.SetTip\s*\(\s*[^,]+,\s*""([^""]+)""\s*\)",
            RegexOptions.Compiled),
        // 3. Single-arg collection .Add("...") — catches penalties.Add("Disk low")
        //    Requires the closing ) immediately after the string (no second arg).
        new(@"\.Add\s*\(\s*""([A-Za-z][^""]{3,})""\s*\)",
            RegexOptions.Compiled),
    };

    // File paths (by suffix) that are excluded from scanning entirely.
    private static readonly string[] ExcludedSuffixes =
    {
        // The localization source itself legitimately holds every literal.
        "LocalizationService.cs",
        // Dev-only gallery; not shipped to end users.
        "Dev/ComponentGalleryWindow.axaml.cs",
    };

    [Fact]
    public void No_new_files_with_hardcoded_UI_CSharp_strings()
    {
        var offenders = ScanForOffenders().ToHashSet(StringComparer.OrdinalIgnoreCase);
        var newOffenders = offenders.Except(GrandfatheredFiles).OrderBy(p => p).ToList();
        var stale = GrandfatheredFiles.Except(offenders).OrderBy(p => p).ToList();

        var issues = new List<string>();
        if (newOffenders.Count > 0)
        {
            issues.Add("New files with hardcoded user-facing C# string literals" +
                       " (replace with LocalizationService._(\"key\") or add to GrandfatheredFiles with justification):");
            issues.AddRange(newOffenders.Select(f => $"  + {f}"));
        }
        if (stale.Count > 0)
        {
            issues.Add("Files in GrandfatheredFiles are now clean — remove them from the list:");
            issues.AddRange(stale.Select(f => $"  - {f}"));
        }

        Assert.True(issues.Count == 0, string.Join(Environment.NewLine, issues));
    }

    /// <summary>
    /// The GrandfatheredFiles list must be kept to a minimum. The one current
    /// entry (ChatSection) is grandfathered specifically because its
    /// <c>alerts.Add("CPU anomaly detected")</c> calls feed LLM prompt context —
    /// they are intentionally English.  If you need to add a new entry, document
    /// the reason in a comment next to the path.
    /// </summary>
    [Fact]
    public void GrandfatheredFiles_is_exactly_one_known_entry_for_llm_context()
    {
        // The only permitted grandfather is the LLM-context file.  If it gets
        // cleaned up (e.g. the LLM context is refactored to use a dedicated
        // class outside the View layer) this test will tell you to remove it.
        var expected = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "src/UI/AuraCore.UI.Avalonia/Views/Pages/AI/ChatSection.axaml.cs",
        };

        var extra = GrandfatheredFiles.Except(expected).OrderBy(p => p).ToList();
        var missing = expected.Except(GrandfatheredFiles).OrderBy(p => p).ToList();

        var issues = new List<string>();
        if (extra.Count > 0)
        {
            issues.Add("Unexpected entries added to GrandfatheredFiles — localize them instead of grandfathering:");
            issues.AddRange(extra.Select(f => $"  + {f}"));
        }
        if (missing.Count > 0)
        {
            issues.Add("Expected grandfather entry is missing (was it cleaned up? Great — remove from expected too):");
            issues.AddRange(missing.Select(f => $"  - {f}"));
        }

        Assert.True(issues.Count == 0, string.Join(Environment.NewLine, issues));
    }

    // ── Scanning helpers ──────────────────────────────────────────────────────

    private static IEnumerable<string> ScanForOffenders()
    {
        var srcRoot = FindSrcUiRoot();
        if (srcRoot == null) yield break;
        var repoRoot = FindRepoRoot(srcRoot);

        foreach (var file in Directory.EnumerateFiles(srcRoot, "*.cs", SearchOption.AllDirectories))
        {
            // Skip obj/ generated files
            var filePath = file.Replace('\\', '/');
            if (filePath.Contains("/obj/")) continue;

            var rel = Path.GetRelativePath(repoRoot, file).Replace('\\', '/');

            if (ExcludedSuffixes.Any(s => rel.EndsWith(s, StringComparison.OrdinalIgnoreCase)))
                continue;

            if (FileHasOffender(file))
                yield return rel;
        }
    }

    private static bool FileHasOffender(string path)
    {
        var text = File.ReadAllText(path);
        foreach (var pattern in Patterns)
        {
            foreach (Match m in pattern.Matches(text))
            {
                // The captured string literal is the LAST capture group.
                var value = m.Groups[m.Groups.Count - 1].Value;
                if (!IsWhitelistedString(value))
                    return true;
            }
        }
        return false;
    }

    private static bool IsWhitelistedString(string value)
    {
        // Empty / whitespace
        if (string.IsNullOrWhiteSpace(value)) return true;

        var trimmed = value.Trim();

        // Single character  (e.g. "!" in catch blocks)
        if (trimmed.Length <= 1) return true;

        // No alphanumeric content (separators, arrows, bullets, em-dashes)
        if (!trimmed.Any(char.IsLetterOrDigit)) return true;

        // Pure numeric / unit-like  (e.g. "--", "N/A", "0.0", "100%")
        if (trimmed.All(c => char.IsDigit(c) || c == '.' || c == ',' || c == '/' ||
                              c == '-' || c == '+' || c == ' ' || c == '%'))
            return true;

        // All-uppercase acronym ≤ 5 chars (CPU, RAM, GPU, OS, API, SMART, URL)
        if (trimmed.Length <= 5 &&
            trimmed.All(c => !char.IsLetter(c) || char.IsUpper(c)) &&
            trimmed.Any(char.IsLetter))
            return true;

        // Binding-like (rare in C# but possible for XAML binding string args)
        if (trimmed.StartsWith("{", StringComparison.Ordinal)) return true;

        // Resource-key-like: no spaces + contains a structural separator.
        // Covers: "ai.memory.unavailable", "#FF0000", "/api/foo", "avares://..."
        if (!trimmed.Contains(' ') && trimmed.Length <= 80 &&
            (trimmed.Contains('.') || trimmed.Contains(':') || trimmed.Contains('/') ||
             trimmed.StartsWith("#", StringComparison.Ordinal) ||
             trimmed.StartsWith("avares:", StringComparison.OrdinalIgnoreCase)))
            return true;

        // Identifier-like single-word token: CSS class names, action keys, role tokens.
        // Pattern: starts with letter, only letters/digits/dash/underscore, no spaces.
        // Covers: "accent", "narrow-nav", "dark_mode", "assistant", "disabled"
        if (!trimmed.Contains(' ') &&
            Regex.IsMatch(trimmed, @"^[A-Za-z][A-Za-z0-9_\-]*$", RegexOptions.None))
            return true;

        return false;
    }

    // ── Path helpers ──────────────────────────────────────────────────────────

    private static string? FindSrcUiRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "src", "UI", "AuraCore.UI.Avalonia");
            if (Directory.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }
        return null;
    }

    private static string FindRepoRoot(string srcUiRoot) =>
        // srcUiRoot = <repoRoot>/src/UI/AuraCore.UI.Avalonia
        Path.GetFullPath(Path.Combine(srcUiRoot, "..", "..", ".."));
}
