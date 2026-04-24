using AuraCore.API.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace AuraCore.API.Hubs;

/// <summary>
/// Real-time admin event hub. Accepts admin + superadmin roles (superadmin
/// inherits admin via dual-role JWT). Rejects scope-limited tokens. Admins
/// join "admins" group; superadmins additionally join "superadmins" group
/// for permission-request broadcasts (spec D15, D17).
/// </summary>
[Authorize(Roles = "admin,superadmin")]
public class AdminHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        // Reject scope-limited JWTs — they must not hold a live hub connection.
        if (Context.User?.IsScopeLimited() == true)
        {
            Context.Abort();
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, "admins");
        if (Context.User?.GetPrimaryRole() == "superadmin")
            await Groups.AddToGroupAsync(Context.ConnectionId, "superadmins");

        await Clients.Group("admins").SendAsync("AdminCount",
            new { count = AdminConnectionCount.Increment() });
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, "admins");
        if (Context.User?.GetPrimaryRole() == "superadmin")
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, "superadmins");
        await Clients.Group("admins").SendAsync("AdminCount",
            new { count = AdminConnectionCount.Decrement() });
        await base.OnDisconnectedAsync(exception);
    }
}

internal static class AdminConnectionCount
{
    private static int _count = 0;
    public static int Increment() => Interlocked.Increment(ref _count);
    public static int Decrement() => Interlocked.Decrement(ref _count);
    public static int Current => Volatile.Read(ref _count);
}
