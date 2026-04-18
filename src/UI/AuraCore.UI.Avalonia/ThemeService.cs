using global::Avalonia;
using global::Avalonia.Platform;
using global::Avalonia.Styling;

namespace AuraCore.UI.Avalonia;

/// <summary>
/// Manages application theme (Dark / Light / System) and persists preference.
/// File: %LOCALAPPDATA%/AuraCorePro/theme.pref — one of "dark" | "light" | "system".
/// </summary>
public static class ThemeService
{
    public enum AppTheme { Dark, Light, System }

    private static readonly string PrefPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AuraCorePro", "theme.pref");

    public static AppTheme CurrentTheme { get; private set; } = AppTheme.Dark;

    /// <summary>
    /// Actual variant applied to Application.Current. When CurrentTheme is System,
    /// this is the OS-resolved Dark or Light — never ThemeVariant.Default.
    /// </summary>
    public static ThemeVariant EffectiveVariant { get; private set; } = ThemeVariant.Dark;

    /// <summary>Back-compat shim — true when EffectiveVariant is Dark.</summary>
    public static bool IsDarkMode => EffectiveVariant == ThemeVariant.Dark;

    public static event Action? ThemeChanged;

    /// <summary>Load saved preference. Call once at startup before MainWindow shows.</summary>
    public static void Initialize()
    {
        var loaded = AppTheme.Dark;
        try
        {
            if (File.Exists(PrefPath))
            {
                var content = File.ReadAllText(PrefPath).Trim().ToLowerInvariant();
                loaded = content switch
                {
                    "light" => AppTheme.Light,
                    "system" => AppTheme.System,
                    _ => AppTheme.Dark,
                };
            }
        }
        catch { /* default to dark */ }

        CurrentTheme = loaded;
        ApplyTheme();
    }

    /// <summary>Set specific theme. Persists to disk and fires ThemeChanged if changed.</summary>
    public static void SetTheme(AppTheme theme)
    {
        if (CurrentTheme == theme) return;
        CurrentTheme = theme;
        ApplyTheme();
        Save();
        ThemeChanged?.Invoke();
    }

    /// <summary>Toggle Dark ↔ Light. System must be selected explicitly via SetTheme.</summary>
    public static void Toggle()
    {
        SetTheme(CurrentTheme == AppTheme.Dark ? AppTheme.Light : AppTheme.Dark);
    }

    private static void ApplyTheme()
    {
        EffectiveVariant = CurrentTheme switch
        {
            AppTheme.Dark => ThemeVariant.Dark,
            AppTheme.Light => ThemeVariant.Light,
            AppTheme.System => ResolveSystemVariant(),
            _ => ThemeVariant.Dark,
        };

        if (global::Avalonia.Application.Current is { } app)
        {
            app.RequestedThemeVariant = EffectiveVariant;
        }
    }

    /// <summary>
    /// Resolve AppTheme.System to a concrete ThemeVariant via Avalonia's
    /// PlatformSettings. Returns Dark if the OS preference is unknown.
    /// </summary>
    private static ThemeVariant ResolveSystemVariant()
    {
        try
        {
            if (global::Avalonia.Application.Current is { } app &&
                app.PlatformSettings is { } settings)
            {
                var os = settings.GetColorValues().ThemeVariant;
                if (os == PlatformThemeVariant.Light) return ThemeVariant.Light;
                if (os == PlatformThemeVariant.Dark) return ThemeVariant.Dark;
            }
        }
        catch { /* fall through */ }

        return ThemeVariant.Dark;
    }

    private static void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(PrefPath)!);
            File.WriteAllText(PrefPath, CurrentTheme switch
            {
                AppTheme.Light => "light",
                AppTheme.System => "system",
                _ => "dark",
            });
        }
        catch { /* non-critical */ }
    }
}
