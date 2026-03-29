namespace AuraCore.Application;

/// <summary>Global session state — set by the Desktop app, read by Guards and modules</summary>
public static class SessionState
{
    public static string? AccessToken { get; set; }
    public static string? ApiBaseUrl { get; set; }
    public static string? UserEmail { get; set; }
    public static string? UserRole { get; set; }
    public static string? UserTier { get; set; } = "free";
    public static string? UserId { get; set; }
    public static Guid? DeviceId { get; set; }

    public static bool IsAdmin => UserRole == "admin";
    public static bool IsAuthenticated => !string.IsNullOrEmpty(AccessToken);
}
