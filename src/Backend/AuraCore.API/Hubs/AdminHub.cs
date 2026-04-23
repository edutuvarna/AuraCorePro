using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace AuraCore.API.Hubs;

/// <summary>
/// Real-time admin event hub. Authorized callers (role=admin) join the
/// "admins" group on connect and receive UserRegistered / UserLogin /
/// Payment / CrashReport / Telemetry events broadcast by controllers
/// via IHubContext&lt;AdminHub&gt;.
/// </summary>
[Authorize(Roles = "admin")]
public class AdminHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "admins");
        await Clients.Group("admins").SendAsync("AdminCount", new { count = AdminConnectionCount.Increment() });
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, "admins");
        await Clients.Group("admins").SendAsync("AdminCount", new { count = AdminConnectionCount.Decrement() });
        await base.OnDisconnectedAsync(exception);
    }
}

/// <summary>
/// Process-local count of active admin connections. Resets on app restart.
/// Phase 6.11+ may upgrade to Redis-backed shared count if multi-instance.
/// </summary>
internal static class AdminConnectionCount
{
    private static int _count = 0;
    public static int Increment() => Interlocked.Increment(ref _count);
    public static int Decrement() => Interlocked.Decrement(ref _count);
    public static int Current => Volatile.Read(ref _count);
}
