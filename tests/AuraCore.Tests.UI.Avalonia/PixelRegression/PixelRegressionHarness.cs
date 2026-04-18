using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;

namespace AuraCore.Tests.UI.Avalonia.PixelRegression;

/// <summary>
/// Renders an Avalonia view to an in-memory PNG byte array.
/// Must be called from inside an <c>[AvaloniaFact]</c> — relies on the
/// Avalonia.Headless harness's UI-thread dispatcher + synchronization context.
/// </summary>
public static class PixelRegressionHarness
{
    /// <summary>
    /// Constructs <typeparamref name="TView"/>, hosts it in a sized headless
    /// <see cref="Window"/>, forces a full layout + render pass, captures the
    /// rendered frame, and returns the frame as a PNG byte array.
    /// </summary>
    /// <typeparam name="TView">
    /// An Avalonia <see cref="Control"/> with a public parameterless constructor.
    /// </typeparam>
    /// <param name="width">Window and view width in device-independent pixels.</param>
    /// <param name="height">Window and view height in device-independent pixels.</param>
    /// <returns>PNG-encoded bytes of the rendered frame.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the headless renderer produced no frame (e.g. nothing was drawn).
    /// </exception>
    public static Task<byte[]> RenderViewAsync<TView>(int width, int height)
        where TView : Control, new()
    {
        var view = new TView();
        var window = new Window
        {
            Width = width,
            Height = height,
            SystemDecorations = SystemDecorations.None,
            Content = view,
        };

        window.Show();
        try
        {
            window.Measure(new Size(width, height));
            window.Arrange(new Rect(0, 0, width, height));
            // Drain the dispatcher so binding + converter evaluations complete.
            Dispatcher.UIThread.RunJobs();

            using var frame = window.CaptureRenderedFrame()
                ?? throw new InvalidOperationException(
                    $"Headless render produced no frame for {typeof(TView).Name}");

            using var ms = new MemoryStream();
            frame.Save(ms);
            return Task.FromResult(ms.ToArray());
        }
        finally
        {
            window.Close();
        }
    }
}
