using System;

namespace AuraCore.UI.Avalonia;

/// <summary>
/// Static service for updating the global status bar from any page.
/// </summary>
public static class StatusBarService
{
    /// <summary>Fired when the status text changes.</summary>
    public static event Action<string>? StatusChanged;

    /// <summary>Fired when an operation name + progress fraction changes (0..1, or -1 for indeterminate).</summary>
    public static event Action<string, double>? ProgressChanged;

    private static string _currentStatus = "Ready";

    public static string CurrentStatus => _currentStatus;

    public static void SetStatus(string text)
    {
        _currentStatus = text;
        StatusChanged?.Invoke(text);
    }

    public static void SetProgress(string operationName, double fraction)
    {
        ProgressChanged?.Invoke(operationName, fraction);
    }

    public static void ClearProgress()
    {
        ProgressChanged?.Invoke("", -1);
    }
}
