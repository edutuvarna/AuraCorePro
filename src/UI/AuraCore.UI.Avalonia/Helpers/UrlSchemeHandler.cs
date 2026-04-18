using System.Collections.Generic;

namespace AuraCore.UI.Avalonia.Helpers;

public enum IntentKind { Section, Module }

public sealed record SectionNavigationIntent(IntentKind Kind, string Id);

public static class UrlSchemeHandler
{
    public const string Scheme = "auracore";

    public static SectionNavigationIntent? Parse(
        string? input,
        IReadOnlyCollection<string> knownSectionIds,
        IReadOnlyCollection<string> knownModuleIds)
    {
        if (string.IsNullOrEmpty(input)) return null;

        // No whitespace tolerance (spec §6.1).
        for (int i = 0; i < input.Length; i++)
        {
            if (char.IsWhiteSpace(input[i])) return null;
        }

        // Reject null bytes + control characters outright.
        for (int i = 0; i < input.Length; i++)
        {
            char c = input[i];
            if (c == '\0' || c < 0x20) return null;
        }

        // Must start with exactly "auracore://" (lowercase, double-slash).
        const string Prefix = "auracore://";
        if (!input.StartsWith(Prefix, System.StringComparison.Ordinal)) return null;

        var rest = input.AsSpan(Prefix.Length);

        // Empty path → reject.
        if (rest.Length == 0) return null;

        // Reject triple-slash at start.
        if (rest[0] == '/') return null;

        // Split on '/' — expect 1 or 2 segments.
        int firstSlash = rest.IndexOf('/');
        if (firstSlash < 0)
        {
            // Single-segment shape: "auracore://<id>"
            var id = rest.ToString();
            if (!IsSafeSegment(id)) return null;
            if (knownSectionIds.Contains(id))
            {
                return new SectionNavigationIntent(IntentKind.Section, id);
            }
            return null;
        }

        // Two-segment shape: "auracore://<prefix>/<id>"
        var prefixSegment = rest.Slice(0, firstSlash).ToString();
        var tail = rest.Slice(firstSlash + 1);

        // Further slashes in tail → 3+ segments → reject.
        if (tail.IndexOf('/') >= 0) return null;

        if (tail.Length == 0) return null;

        var tailStr = tail.ToString();
        if (!IsSafeSegment(prefixSegment) || !IsSafeSegment(tailStr)) return null;

        // Only "module" is a recognized prefix segment (case-sensitive).
        if (!string.Equals(prefixSegment, "module", System.StringComparison.Ordinal)) return null;

        if (knownModuleIds.Contains(tailStr))
        {
            return new SectionNavigationIntent(IntentKind.Module, tailStr);
        }

        return null;
    }

    private static bool IsSafeSegment(string segment)
    {
        if (segment.Length == 0) return false;
        // ASCII letters, digits, '-', '_' only. Blocks %-encoding, unicode
        // homographs, path-traversal dots, shell metachars, null bytes.
        for (int i = 0; i < segment.Length; i++)
        {
            char c = segment[i];
            bool ok = (c >= 'a' && c <= 'z')
                   || (c >= 'A' && c <= 'Z')
                   || (c >= '0' && c <= '9')
                   || c == '-' || c == '_';
            if (!ok) return false;
        }
        return true;
    }
}
