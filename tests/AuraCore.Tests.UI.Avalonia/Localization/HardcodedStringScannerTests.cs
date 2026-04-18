using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.Localization;

/// <summary>
/// Regression gate: detect hardcoded user-facing string literals in UI XAML.
/// Ratchet design — current-state files are grandfathered; the test fails when
/// a NEW file gains offenders or a grandfathered file gets cleaned up (the
/// list must be kept in sync with reality).
/// </summary>
public class HardcodedStringScannerTests
{
    // Relative paths from repo root. Alphabetical.
    // When you localize a module, REMOVE its path from this list.
    // Legitimate short literals (acronyms, single chars, symbols) are handled
    // by IsWhitelistedString — don't expand this list for those.
    private static readonly HashSet<string> GrandfatheredFiles = new(StringComparer.OrdinalIgnoreCase)
    {
        // Phase 6.4 Batch 10 complete — all modules localized. List is empty.
    };

    private static readonly string[] ScannedAttributes =
    {
        "Text", "Content", "ToolTip", "ToolTip.Tip",
        "Watermark", "Header", "Title", "Label",
    };

    // File paths ending with any of these are excluded (developer-only).
    private static readonly string[] ExcludedSuffixes =
    {
        "Dev/ComponentGalleryWindow.axaml",
    };

    [Fact]
    public void No_new_files_with_hardcoded_UI_strings()
    {
        var offenderFiles = ScanViewsForOffenders().ToHashSet(StringComparer.OrdinalIgnoreCase);
        var newOffenders = offenderFiles.Except(GrandfatheredFiles).OrderBy(p => p).ToList();
        var stalelyGrandfathered = GrandfatheredFiles.Except(offenderFiles).OrderBy(p => p).ToList();

        var issues = new List<string>();
        if (newOffenders.Count > 0)
        {
            issues.Add("New files with hardcoded UI strings (add LocalizationService keys + use bindings, OR add to GrandfatheredFiles if intentional):");
            issues.AddRange(newOffenders.Select(f => $"  + {f}"));
        }
        if (stalelyGrandfathered.Count > 0)
        {
            issues.Add("Files cleaned up but still in GrandfatheredFiles (remove them from the list):");
            issues.AddRange(stalelyGrandfathered.Select(f => $"  - {f}"));
        }

        Assert.True(issues.Count == 0, string.Join(Environment.NewLine, issues));
    }

    /// <summary>
    /// Phase 6.4 shipped with every UI XAML file localizable — the grandfather
    /// list is empty and should stay empty. Adding entries here to bypass the
    /// main scanner would silently re-introduce the regression the sweep fixed.
    /// If there's a genuine reason to re-introduce a grandfather list (e.g., a
    /// large import from an external project with hundreds of literals), delete
    /// this test along with that justification.
    /// </summary>
    [Fact]
    public void GrandfatheredFiles_is_empty_per_phase_6_4_commitment()
    {
        Assert.True(
            GrandfatheredFiles.Count == 0,
            $"GrandfatheredFiles has {GrandfatheredFiles.Count} entries. Phase 6.4 TR Completion Sweep " +
            "committed to zero grandfathering — every UI XAML must be localizable. Don't silence the " +
            "main scanner by adding files here; localize the file's literals instead. If you truly " +
            "need to re-introduce a grandfather list, delete this test with a written justification.");
    }

    private static IEnumerable<string> ScanViewsForOffenders()
    {
        var viewsRoot = FindViewsRoot();
        if (viewsRoot == null) yield break;

        var repoRoot = FindRepoRoot(viewsRoot);
        var axamlFiles = Directory.EnumerateFiles(viewsRoot, "*.axaml", SearchOption.AllDirectories);

        foreach (var file in axamlFiles)
        {
            var rel = Path.GetRelativePath(repoRoot, file).Replace('\\', '/');
            if (ExcludedSuffixes.Any(ex => rel.EndsWith(ex.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase)))
                continue;

            if (FileHasHardcodedOffenders(file))
                yield return rel;
        }
    }

    private static bool FileHasHardcodedOffenders(string path)
    {
        var text = File.ReadAllText(path);
        foreach (var attr in ScannedAttributes)
        {
            var pattern = $@"\b{Regex.Escape(attr)}\s*=\s*""([^""]*)""";
            foreach (Match m in Regex.Matches(text, pattern))
            {
                var value = m.Groups[1].Value;
                if (!IsWhitelistedString(value))
                    return true;
            }
        }
        return false;
    }

    private static bool IsWhitelistedString(string value)
    {
        if (value.StartsWith("{", StringComparison.Ordinal)) return true;
        if (string.IsNullOrWhiteSpace(value)) return true;

        // XML character entity reference for decorative icons e.g. &#x1F50D; &#x25C6;
        // These are icon glyphs rendered in decorative TextBlocks — not user-facing text.
        if (Regex.IsMatch(value, @"^&#x[0-9A-Fa-f]+;$", RegexOptions.None)) return true;

        // Pure numeric / punctuation-only / whitespace-only.
        if (value.All(c => char.IsDigit(c) || c == '.' || c == ',' || c == '/' || c == '-' || c == '+' || c == ' '))
            return true;

        var trimmed = value.Trim();
        if (trimmed.Length <= 1) return true;

        // Pure non-alphanumeric (separators, arrows, bullets, ellipsis).
        if (!trimmed.Any(char.IsLetterOrDigit)) return true;

        // All-uppercase acronym <= 5 chars (CPU, RAM, GPU, OS, API, SMART, URL).
        if (trimmed.Length <= 5 &&
            trimmed.All(c => !char.IsLetter(c) || char.IsUpper(c)) &&
            trimmed.Any(char.IsLetter))
            return true;

        return false;
    }

    private static string? FindViewsRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "src", "UI", "AuraCore.UI.Avalonia", "Views");
            if (Directory.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }
        return null;
    }

    private static string FindRepoRoot(string viewsRoot)
    {
        // viewsRoot = <repoRoot>/src/UI/AuraCore.UI.Avalonia/Views
        return Path.GetFullPath(Path.Combine(viewsRoot, "..", "..", "..", ".."));
    }
}
