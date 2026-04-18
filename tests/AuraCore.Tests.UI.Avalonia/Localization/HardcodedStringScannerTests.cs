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
        // Current-state offenders (2026-04-18 baseline). Remove each entry once the module is localized.
        "src/UI/AuraCore.UI.Avalonia/Views/Controls/AIFeatureCard.axaml",
        "src/UI/AuraCore.UI.Avalonia/Views/Controls/DiskHealthSummaryCard.axaml",
        "src/UI/AuraCore.UI.Avalonia/Views/Dialogs/AIConsentDialog.axaml",
        "src/UI/AuraCore.UI.Avalonia/Views/Dialogs/ChatOptInDialog.axaml",
        "src/UI/AuraCore.UI.Avalonia/Views/Dialogs/ModelManagerDialog.axaml",
        "src/UI/AuraCore.UI.Avalonia/Views/Dialogs/PrivilegeHelperInstallDialog.axaml",
        "src/UI/AuraCore.UI.Avalonia/Views/Dialogs/TierUpgradePlaceholderDialog.axaml",
        "src/UI/AuraCore.UI.Avalonia/Views/Pages/AdminPanelView.axaml",
        "src/UI/AuraCore.UI.Avalonia/Views/Pages/AppInstallerView.axaml",
        "src/UI/AuraCore.UI.Avalonia/Views/Pages/AutorunManagerView.axaml",
        "src/UI/AuraCore.UI.Avalonia/Views/Pages/BatteryOptimizerView.axaml",
        "src/UI/AuraCore.UI.Avalonia/Views/Pages/BloatwareRemovalView.axaml",
        "src/UI/AuraCore.UI.Avalonia/Views/Pages/BrewManagerView.axaml",
        "src/UI/AuraCore.UI.Avalonia/Views/Pages/CategoryCleanView.axaml",
        "src/UI/AuraCore.UI.Avalonia/Views/Pages/CronManagerView.axaml",
        "src/UI/AuraCore.UI.Avalonia/Views/Pages/DefaultsOptimizerView.axaml",
        "src/UI/AuraCore.UI.Avalonia/Views/Pages/DefenderManagerView.axaml",
        "src/UI/AuraCore.UI.Avalonia/Views/Pages/DiskHealthView.axaml",
        "src/UI/AuraCore.UI.Avalonia/Views/Pages/DnsBenchmarkView.axaml",
        "src/UI/AuraCore.UI.Avalonia/Views/Pages/DnsFlusherView.axaml",
        "src/UI/AuraCore.UI.Avalonia/Views/Pages/DockerCleanerView.axaml",
        "src/UI/AuraCore.UI.Avalonia/Views/Pages/DriverUpdaterView.axaml",
        "src/UI/AuraCore.UI.Avalonia/Views/Pages/EnvironmentVariablesView.axaml",
        "src/UI/AuraCore.UI.Avalonia/Views/Pages/FileShredderView.axaml",
        "src/UI/AuraCore.UI.Avalonia/Views/Pages/FirewallRulesView.axaml",
        "src/UI/AuraCore.UI.Avalonia/Views/Pages/FontManagerView.axaml",
        "src/UI/AuraCore.UI.Avalonia/Views/Pages/GamingModeView.axaml",
        "src/UI/AuraCore.UI.Avalonia/Views/Pages/GenericModuleView.axaml",
        "src/UI/AuraCore.UI.Avalonia/Views/Pages/GrubManagerView.axaml",
        "src/UI/AuraCore.UI.Avalonia/Views/Pages/HostsEditorView.axaml",
        "src/UI/AuraCore.UI.Avalonia/Views/Pages/IsoBuilderView.axaml",
        "src/UI/AuraCore.UI.Avalonia/Views/Pages/JournalCleanerView.axaml",
        "src/UI/AuraCore.UI.Avalonia/Views/Pages/KernelCleanerView.axaml",
        "src/UI/AuraCore.UI.Avalonia/Views/Pages/LaunchAgentManagerView.axaml",
        "src/UI/AuraCore.UI.Avalonia/Views/Pages/LinuxAppInstallerView.axaml",
        "src/UI/AuraCore.UI.Avalonia/Views/Pages/MacAppInstallerView.axaml",
        "src/UI/AuraCore.UI.Avalonia/Views/Pages/NetworkMonitorView.axaml",
        "src/UI/AuraCore.UI.Avalonia/Views/Pages/NetworkOptimizerView.axaml",
        "src/UI/AuraCore.UI.Avalonia/Views/Pages/OnboardingView.axaml",
        "src/UI/AuraCore.UI.Avalonia/Views/Pages/PackageCleanerView.axaml",
        "src/UI/AuraCore.UI.Avalonia/Views/Pages/PaymentView.axaml",
        "src/UI/AuraCore.UI.Avalonia/Views/Pages/ProcessMonitorView.axaml",
        "src/UI/AuraCore.UI.Avalonia/Views/Pages/RamOptimizerView.axaml",
        "src/UI/AuraCore.UI.Avalonia/Views/Pages/RegistryOptimizerView.axaml",
        "src/UI/AuraCore.UI.Avalonia/Views/Pages/ScanOptimizeView.axaml",
        "src/UI/AuraCore.UI.Avalonia/Views/Pages/ServiceManagerView.axaml",
        "src/UI/AuraCore.UI.Avalonia/Views/Pages/SnapFlatpakCleanerView.axaml",
        "src/UI/AuraCore.UI.Avalonia/Views/Pages/SpaceAnalyzerView.axaml",
        "src/UI/AuraCore.UI.Avalonia/Views/Pages/SpotlightManagerView.axaml",
        "src/UI/AuraCore.UI.Avalonia/Views/Pages/StartupOptimizerView.axaml",
        "src/UI/AuraCore.UI.Avalonia/Views/Pages/SwapOptimizerView.axaml",
        "src/UI/AuraCore.UI.Avalonia/Views/Pages/SymlinkManagerView.axaml",
        "src/UI/AuraCore.UI.Avalonia/Views/Pages/SystemdManagerView.axaml",
        "src/UI/AuraCore.UI.Avalonia/Views/Pages/SystemHealthView.axaml",
        "src/UI/AuraCore.UI.Avalonia/Views/Pages/TimeMachineManagerView.axaml",
        "src/UI/AuraCore.UI.Avalonia/Views/Pages/TweakListView.axaml",
        "src/UI/AuraCore.UI.Avalonia/Views/Pages/UpgradeView.axaml",
        "src/UI/AuraCore.UI.Avalonia/Views/Pages/WakeOnLanView.axaml",
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
