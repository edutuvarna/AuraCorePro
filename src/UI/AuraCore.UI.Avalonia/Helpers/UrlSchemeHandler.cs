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
        // Task 2 fills this in.
        return null;
    }
}
