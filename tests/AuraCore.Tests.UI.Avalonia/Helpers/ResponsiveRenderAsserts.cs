using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;

namespace AuraCore.Tests.UI.Avalonia.Helpers;

/// <summary>
/// Phase 5.3 responsive-wave render smoke helper. Hosts the given
/// view inside a sized Avalonia window, forces a layout pass, and
/// asserts no exceptions bubble. Used by Tasks 9-15 for wide + narrow
/// render tests.
///
/// Note: this helper must be called from inside an <c>[AvaloniaFact]</c>
/// — the Avalonia.Headless harness provides the UI-thread dispatcher
/// and synchronization context that <c>Window.Show</c> + layout requires.
/// </summary>
public static class ResponsiveRenderAsserts
{
    /// <summary>
    /// Renders <typeparamref name="TView"/> at the given size and
    /// asserts the render completes without throwing and produces
    /// non-negative bounds.
    /// </summary>
    public static void AssertRendersAtSize<TView>(double width, double height)
        where TView : Control, new()
    {
        var view = new TView();
        var window = new Window
        {
            Width = width,
            Height = height,
            Content = view,
        };

        window.Show();
        try
        {
            window.Measure(new Size(width, height));
            window.Arrange(new Rect(0, 0, width, height));
            // Drain the dispatcher so binding + converter evaluations complete.
            Dispatcher.UIThread.RunJobs();

            if (view.Bounds.Width < 0 || view.Bounds.Height < 0)
            {
                throw new Exception(
                    $"View {typeof(TView).Name} rendered with invalid bounds: {view.Bounds}");
            }
        }
        finally
        {
            window.Close();
        }
    }
}
