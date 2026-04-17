using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Layout;

namespace AuraCore.Tests.UI.Avalonia;

/// <summary>
/// Disposable handle for a headless test window. Closing on dispose ensures
/// the Avalonia visual tree is torn down between tests.
/// </summary>
public sealed class TestWindowHandle : IDisposable
{
    public Window Window { get; }

    internal TestWindowHandle(Window window) => Window = window;

    public void Dispose()
    {
        try { Window.Close(); }
        catch { /* headless platform may already be torn down */ }
    }

    // Convenience: allow tests written against Window to use the handle transparently
    public static implicit operator Window(TestWindowHandle h) => h.Window;
}

public static class AvaloniaTestBase
{
    /// <summary>
    /// Attach <paramref name="control"/> to a headless Window, measure + arrange,
    /// then return a disposable handle. Use with <c>using var</c> so the window
    /// closes when the test method exits.
    /// </summary>
    public static TestWindowHandle RenderInWindow(Control control, double width = 400, double height = 200)
    {
        var window = new Window
        {
            Width = width,
            Height = height,
            Content = control
        };
        window.Show();
        // Force layout pass (measure + arrange) so IsMeasureValid becomes true.
        // Avoid GetLastRenderedFrame — it requires Skia + UseHeadlessDrawing=false.
        window.Measure(new Size(width, height));
        window.Arrange(new Rect(0, 0, width, height));
        return new TestWindowHandle(window);
    }
}
