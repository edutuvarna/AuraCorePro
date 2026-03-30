namespace AuraCore.UI.Avalonia;

/// <summary>
/// In-app notification center. Stores up to 50 notifications.
/// Modules and background services post notifications here.
/// </summary>
public sealed class NotificationService
{
    private static NotificationService? _instance;
    public static NotificationService Instance => _instance ??= new();

    private readonly List<AppNotification> _notifications = new();
    private readonly object _lock = new();

    public int UnreadCount { get; private set; }

    public event Action<AppNotification>? NotificationAdded;
    public event Action<int>? UnreadCountChanged;

    public void Post(string title, string message, NotificationType type = NotificationType.Info, string? moduleId = null)
    {
        var notification = new AppNotification
        {
            Id = Guid.NewGuid().ToString(),
            Title = title,
            Message = message,
            Type = type,
            ModuleId = moduleId,
            Timestamp = DateTime.Now,
            IsRead = false
        };

        lock (_lock)
        {
            _notifications.Insert(0, notification);
            if (_notifications.Count > 50)
                _notifications.RemoveRange(50, _notifications.Count - 50);
            UnreadCount++;
        }

        NotificationAdded?.Invoke(notification);
        UnreadCountChanged?.Invoke(UnreadCount);
    }

    public List<AppNotification> GetAll()
    {
        lock (_lock) return _notifications.ToList();
    }

    public void MarkAllRead()
    {
        lock (_lock)
        {
            foreach (var n in _notifications) n.IsRead = true;
            UnreadCount = 0;
        }
        UnreadCountChanged?.Invoke(0);
    }

    public void Clear()
    {
        lock (_lock)
        {
            _notifications.Clear();
            UnreadCount = 0;
        }
        UnreadCountChanged?.Invoke(0);
    }
}

public sealed class AppNotification
{
    public string Id { get; init; } = "";
    public string Title { get; init; } = "";
    public string Message { get; init; } = "";
    public NotificationType Type { get; init; }
    public string? ModuleId { get; init; }
    public DateTime Timestamp { get; set; }
    public bool IsRead { get; set; }
}

public enum NotificationType { Info, Success, Warning, Error }
