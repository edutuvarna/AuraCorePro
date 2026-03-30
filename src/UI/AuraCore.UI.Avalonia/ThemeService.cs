using global::Avalonia;
using global::Avalonia.Styling;

namespace AuraCore.UI.Avalonia;

/// <summary>
/// Manages application theme (dark/light) and persists preference.
/// </summary>
public static class ThemeService
{
    private static readonly string PrefPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AuraCorePro", "theme.pref");

    /// <summary>True = dark mode (default), False = light mode</summary>
    public static bool IsDarkMode { get; private set; } = true;

    /// <summary>Fires when theme changes</summary>
    public static event Action? ThemeChanged;

    /// <summary>Load saved preference (call once at startup)</summary>
    public static void Initialize()
    {
        try
        {
            if (File.Exists(PrefPath))
            {
                var content = File.ReadAllText(PrefPath).Trim().ToLowerInvariant();
                IsDarkMode = content != "light";
            }
        }
        catch { /* default to dark */ }

        ApplyTheme();
    }

    /// <summary>Toggle between dark and light</summary>
    public static void Toggle()
    {
        IsDarkMode = !IsDarkMode;
        ApplyTheme();
        Save();
        ThemeChanged?.Invoke();
    }

    /// <summary>Set specific theme</summary>
    public static void SetTheme(bool dark)
    {
        if (IsDarkMode == dark) return;
        IsDarkMode = dark;
        ApplyTheme();
        Save();
        ThemeChanged?.Invoke();
    }

    private static void ApplyTheme()
    {
        if (global::Avalonia.Application.Current is { } app)
        {
            app.RequestedThemeVariant = IsDarkMode
                ? ThemeVariant.Dark
                : ThemeVariant.Light;
        }
    }

    private static void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(PrefPath)!);
            File.WriteAllText(PrefPath, IsDarkMode ? "dark" : "light");
        }
        catch { /* non-critical */ }
    }
}
