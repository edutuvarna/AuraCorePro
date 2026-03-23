namespace AuraCore.Desktop.Services;

/// <summary>
/// Simple global activity log. Dashboard reads this to show recent actions.
/// </summary>
public static class ActivityLog
{
    private static readonly List<ActivityEntry> _entries = new();
    private static readonly object _lock = new();
    public static event Action? Changed;

    public static void Add(string icon, string message)
    {
        lock (_lock)
        {
            _entries.Insert(0, new ActivityEntry(icon, message, DateTimeOffset.Now));
            if (_entries.Count > 20) _entries.RemoveAt(_entries.Count - 1);
        }
        Changed?.Invoke();
    }

    public static List<ActivityEntry> Recent(int count = 8)
    {
        lock (_lock) { return _entries.Take(count).ToList(); }
    }

    public sealed record ActivityEntry(string Icon, string Message, DateTimeOffset Timestamp);
}
