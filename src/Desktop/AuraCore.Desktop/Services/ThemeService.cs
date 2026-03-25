using System.Text.Json;
using Microsoft.UI.Xaml;

namespace AuraCore.Desktop.Services;

/// <summary>
/// Manages Dark/Light/System theme preference with persistence.
/// </summary>
public static class ThemeService
{
    public enum AppTheme { System, Light, Dark }

    private static readonly string ConfigPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AuraCorePro", "theme.json");

    public static AppTheme CurrentTheme { get; private set; } = AppTheme.System;

    /// <summary>Apply saved theme on startup</summary>
    public static void Initialize(FrameworkElement root)
    {
        Load();
        Apply(root, CurrentTheme);
    }

    /// <summary>Switch theme and persist</summary>
    public static void SetTheme(FrameworkElement root, AppTheme theme)
    {
        CurrentTheme = theme;
        Apply(root, theme);
        Save();
    }

    private static void Apply(FrameworkElement root, AppTheme theme)
    {
        var elementTheme = theme switch
        {
            AppTheme.Light => ElementTheme.Light,
            AppTheme.Dark => ElementTheme.Dark,
            _ => ElementTheme.Default
        };

        // Apply to root content element
        root.RequestedTheme = elementTheme;

        // Apply to all child elements recursively for full propagation
        ApplyToChildren(root, elementTheme);
    }

    private static void ApplyToChildren(FrameworkElement parent, ElementTheme theme)
    {
        if (parent is Microsoft.UI.Xaml.Controls.Panel panel)
        {
            foreach (var child in panel.Children)
            {
                if (child is FrameworkElement fe)
                {
                    fe.RequestedTheme = theme;
                    ApplyToChildren(fe, theme);
                }
            }
        }
        else if (parent is Microsoft.UI.Xaml.Controls.ContentControl cc && cc.Content is FrameworkElement cfe)
        {
            cfe.RequestedTheme = theme;
            ApplyToChildren(cfe, theme);
        }
        else if (parent is Microsoft.UI.Xaml.Controls.NavigationView nav)
        {
            nav.RequestedTheme = theme;
            if (nav.Content is FrameworkElement navContent)
            {
                navContent.RequestedTheme = theme;
                ApplyToChildren(navContent, theme);
            }
        }
    }

    private static void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(ConfigPath)!;
            Directory.CreateDirectory(dir);
            File.WriteAllText(ConfigPath, JsonSerializer.Serialize(new { Theme = CurrentTheme.ToString() }));
        }
        catch { }
    }

    private static void Load()
    {
        try
        {
            if (!File.Exists(ConfigPath)) return;
            var json = File.ReadAllText(ConfigPath);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("Theme", out var prop))
            {
                if (Enum.TryParse<AppTheme>(prop.GetString(), out var t))
                    CurrentTheme = t;
            }
        }
        catch { }
    }
}
