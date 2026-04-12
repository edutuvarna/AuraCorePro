using AuraCore.Module.DefaultsOptimizer.Models;

namespace AuraCore.Module.DefaultsOptimizer;

public static class DefaultsTweaksCatalog
{
    public static readonly IReadOnlyList<DefaultsTweak> All = new List<DefaultsTweak>
    {
        // Finder
        new(Id: "finder-hidden-files", Category: "Finder", Name: "Show Hidden Files",
            Description: "Show hidden files in Finder",
            Domain: "com.apple.finder", Key: "AppleShowAllFiles",
            Type: "bool", RecommendedValue: "true"),

        new(Id: "finder-path-bar", Category: "Finder", Name: "Show Path Bar",
            Description: "Show path bar at bottom of Finder",
            Domain: "com.apple.finder", Key: "ShowPathbar",
            Type: "bool", RecommendedValue: "true"),

        new(Id: "finder-status-bar", Category: "Finder", Name: "Show Status Bar",
            Description: "Show status bar in Finder",
            Domain: "com.apple.finder", Key: "ShowStatusBar",
            Type: "bool", RecommendedValue: "true"),

        // Dock
        new(Id: "dock-autohide", Category: "Dock", Name: "Dock Auto-Hide",
            Description: "Automatically hide the Dock",
            Domain: "com.apple.dock", Key: "autohide",
            Type: "bool", RecommendedValue: "true"),

        new(Id: "dock-no-bounce", Category: "Dock", Name: "Disable Dock Bounce",
            Description: "Stop icons bouncing in Dock",
            Domain: "com.apple.dock", Key: "no-bouncing",
            Type: "bool", RecommendedValue: "true"),

        new(Id: "dock-fast-animation", Category: "Dock", Name: "Fast Dock Animation",
            Description: "Speed up Dock show/hide",
            Domain: "com.apple.dock", Key: "autohide-time-modifier",
            Type: "float", RecommendedValue: "0.12"),

        // Desktop Services
        new(Id: "no-ds-store-network", Category: "System", Name: "Disable .DS_Store on Network",
            Description: "No .DS_Store on network volumes",
            Domain: "com.apple.desktopservices", Key: "DSDontWriteNetworkStores",
            Type: "bool", RecommendedValue: "true"),

        // Screenshots
        new(Id: "screenshot-format-png", Category: "Screenshots", Name: "Screenshot Format PNG",
            Description: "Save screenshots as PNG",
            Domain: "com.apple.screencapture", Key: "type",
            Type: "string", RecommendedValue: "png"),

        new(Id: "screenshot-no-shadow", Category: "Screenshots", Name: "Disable Screenshot Shadow",
            Description: "Remove window shadow in screenshots",
            Domain: "com.apple.screencapture", Key: "disable-shadow",
            Type: "bool", RecommendedValue: "true"),

        // Global
        new(Id: "disable-autocorrect", Category: "System", Name: "Disable Auto-Correct",
            Description: "Disable auto-correct system-wide",
            Domain: "NSGlobalDomain", Key: "NSAutomaticSpellingCorrectionEnabled",
            Type: "bool", RecommendedValue: "false"),

        new(Id: "expand-save-dialog", Category: "System", Name: "Expand Save Dialog",
            Description: "Expand save dialogs by default",
            Domain: "NSGlobalDomain", Key: "NSNavPanelExpandedStateForSaveMode",
            Type: "bool", RecommendedValue: "true"),

        new(Id: "show-all-extensions", Category: "System", Name: "Show All File Extensions",
            Description: "Show file extensions for all files",
            Domain: "NSGlobalDomain", Key: "AppleShowAllExtensions",
            Type: "bool", RecommendedValue: "true"),

        new(Id: "fast-key-repeat", Category: "System", Name: "Faster Key Repeat",
            Description: "Speed up keyboard key repeat",
            Domain: "NSGlobalDomain", Key: "KeyRepeat",
            Type: "int", RecommendedValue: "2"),

        new(Id: "fast-initial-key-delay", Category: "System", Name: "Faster Initial Key Delay",
            Description: "Reduce initial keyboard delay",
            Domain: "NSGlobalDomain", Key: "InitialKeyRepeat",
            Type: "int", RecommendedValue: "15"),

        new(Id: "disable-autocapitalize", Category: "System", Name: "Disable Auto-Capitalize",
            Description: "Disable automatic capitalization",
            Domain: "NSGlobalDomain", Key: "NSAutomaticCapitalizationEnabled",
            Type: "bool", RecommendedValue: "false"),
    };

    public static DefaultsTweak? FindById(string id) =>
        All.FirstOrDefault(t => t.Id == id);
}
