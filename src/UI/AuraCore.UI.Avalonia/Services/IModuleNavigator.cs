using global::Avalonia.Controls;

namespace AuraCore.UI.Avalonia.Services;

public interface IModuleNavigator
{
    void RegisterView(string moduleId, Func<UserControl> factory);

    /// <summary>
    /// Resolve a module id to its rendered UserControl.
    /// </summary>
    /// <param name="moduleId">Module id to resolve.</param>
    /// <param name="onRetryRequested">
    /// Optional callback invoked when the user clicks "Try Again" on an
    /// UnavailableModuleView. The shell typically passes its own re-render
    /// helper so the navigator can re-resolve the same id and the shell
    /// swaps the rendered control. May be null (Try-Again button hidden).
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    Task<UserControl> ResolveAsync(
        string moduleId,
        Func<string, Task>? onRetryRequested = null,
        CancellationToken ct = default);
}
