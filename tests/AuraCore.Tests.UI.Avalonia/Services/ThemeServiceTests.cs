using System;
using AuraCore.UI.Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.Services;

public class ThemeServiceTests
{
    // ThemeService is static + persists to %LOCALAPPDATA%. Tests share process
    // state; use try/finally with SetTheme to restore between assertions.

    [AvaloniaFact]
    public void CurrentTheme_default_is_Dark()
    {
        Assert.Equal(ThemeService.AppTheme.Dark, ThemeService.CurrentTheme);
    }

    [AvaloniaFact]
    public void SetTheme_Light_sets_EffectiveVariant_to_Light()
    {
        var prev = ThemeService.CurrentTheme;
        try
        {
            ThemeService.SetTheme(ThemeService.AppTheme.Light);
            Assert.Equal(ThemeService.AppTheme.Light, ThemeService.CurrentTheme);
            Assert.Equal(ThemeVariant.Light, ThemeService.EffectiveVariant);
        }
        finally
        {
            ThemeService.SetTheme(prev);
        }
    }

    [AvaloniaFact]
    public void SetTheme_Dark_sets_EffectiveVariant_to_Dark()
    {
        var prev = ThemeService.CurrentTheme;
        try
        {
            ThemeService.SetTheme(ThemeService.AppTheme.Dark);
            Assert.Equal(ThemeService.AppTheme.Dark, ThemeService.CurrentTheme);
            Assert.Equal(ThemeVariant.Dark, ThemeService.EffectiveVariant);
        }
        finally
        {
            ThemeService.SetTheme(prev);
        }
    }

    [AvaloniaFact]
    public void SetTheme_System_resolves_EffectiveVariant_to_Dark_or_Light()
    {
        var prev = ThemeService.CurrentTheme;
        try
        {
            ThemeService.SetTheme(ThemeService.AppTheme.System);
            Assert.Equal(ThemeService.AppTheme.System, ThemeService.CurrentTheme);
            Assert.True(
                ThemeService.EffectiveVariant == ThemeVariant.Dark ||
                ThemeService.EffectiveVariant == ThemeVariant.Light,
                $"EffectiveVariant was {ThemeService.EffectiveVariant}, expected Dark or Light");
        }
        finally
        {
            ThemeService.SetTheme(prev);
        }
    }

    [AvaloniaFact]
    public void IsDarkMode_reflects_EffectiveVariant()
    {
        var prev = ThemeService.CurrentTheme;
        try
        {
            ThemeService.SetTheme(ThemeService.AppTheme.Light);
            Assert.False(ThemeService.IsDarkMode);

            ThemeService.SetTheme(ThemeService.AppTheme.Dark);
            Assert.True(ThemeService.IsDarkMode);
        }
        finally
        {
            ThemeService.SetTheme(prev);
        }
    }

    [AvaloniaFact]
    public void Toggle_cycles_Dark_to_Light_to_Dark_skipping_System()
    {
        var prev = ThemeService.CurrentTheme;
        try
        {
            ThemeService.SetTheme(ThemeService.AppTheme.Dark);
            ThemeService.Toggle();
            Assert.Equal(ThemeService.AppTheme.Light, ThemeService.CurrentTheme);

            ThemeService.Toggle();
            Assert.Equal(ThemeService.AppTheme.Dark, ThemeService.CurrentTheme);
        }
        finally
        {
            ThemeService.SetTheme(prev);
        }
    }

    [AvaloniaFact]
    public void ThemeChanged_fires_when_theme_actually_changes()
    {
        var prev = ThemeService.CurrentTheme;
        ThemeService.SetTheme(ThemeService.AppTheme.Dark);
        var fired = 0;
        Action handler = () => fired++;
        ThemeService.ThemeChanged += handler;
        try
        {
            ThemeService.SetTheme(ThemeService.AppTheme.Light);
            Assert.Equal(1, fired);

            // No-op (already Light) — event should NOT fire.
            ThemeService.SetTheme(ThemeService.AppTheme.Light);
            Assert.Equal(1, fired);

            ThemeService.SetTheme(ThemeService.AppTheme.Dark);
            Assert.Equal(2, fired);
        }
        finally
        {
            ThemeService.ThemeChanged -= handler;
            ThemeService.SetTheme(prev);
        }
    }
}
