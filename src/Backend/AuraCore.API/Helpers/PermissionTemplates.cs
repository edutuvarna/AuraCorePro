namespace AuraCore.API.Helpers;

/// <summary>
/// Four permission templates per spec D6. Default/Trusted/ReadOnly produce a
/// deterministic permission key list; Custom is configured per-grant by the
/// superadmin (see Superadmin AdminManagementController create-admin flow).
/// </summary>
public static class PermissionTemplates
{
    public const string Default  = "Default";
    public const string Trusted  = "Trusted";
    public const string ReadOnly = "ReadOnly";
    public const string Custom   = "Custom";

    public static readonly IReadOnlyList<string> AllTemplates = new[]
    {
        Default, Trusted, ReadOnly, Custom,
    };

    public static IReadOnlyList<string> GetPermissionsForTemplate(string template) => template switch
    {
        Default  => Array.Empty<string>(),
        Trusted  => PermissionKeys.AllTier2,
        ReadOnly => Array.Empty<string>(),
        Custom   => throw new InvalidOperationException(
            "Custom template is configured per-grant by the superadmin, not via this helper"),
        _ => throw new ArgumentException($"Unknown template: {template}"),
    };

    public static bool RequiresIsReadonlyFlag(string template) => template == ReadOnly;

    public static bool IsValidTemplate(string template) => AllTemplates.Contains(template);
}
