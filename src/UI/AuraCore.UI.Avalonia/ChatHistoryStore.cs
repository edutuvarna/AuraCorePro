namespace AuraCore.UI.Avalonia;

/// <summary>
/// Static in-memory store for AI Chat messages. Persists across page navigations.
/// Cleared on app exit. Max 50 messages.
/// </summary>
public static class ChatHistoryStore
{
    private static readonly List<ChatMessage> _messages = new();
    private static readonly object _lock = new();
    private const int MaxMessages = 50;

    public static IReadOnlyList<ChatMessage> Messages
    {
        get { lock (_lock) { return _messages.ToList(); } }
    }

    public static void Add(string role, string text)
    {
        lock (_lock)
        {
            _messages.Add(new ChatMessage(role, text, DateTime.Now));
            while (_messages.Count > MaxMessages)
                _messages.RemoveAt(0);
        }
    }

    public static void Clear()
    {
        lock (_lock) { _messages.Clear(); }
    }
}

public sealed record ChatMessage(string Role, string Text, DateTime Timestamp);
