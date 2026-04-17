namespace AuraCore.Infrastructure.PrivilegeIpc.MacOS;

/// <summary>
/// Status values returned by SMAppService on macOS 13+.
/// Mirrors SMAppServiceStatus from ServiceManagement.framework.
/// </summary>
public enum SMAppServiceStatus
{
    /// <summary>The daemon has not been registered yet.</summary>
    NotRegistered,

    /// <summary>The daemon is running and authorized; ready to receive XPC calls.</summary>
    Enabled,

    /// <summary>Registered but the user needs to click "Allow in Settings" to activate.</summary>
    RequiresApproval,

    /// <summary>The plist is missing or the bundle layout is incorrect.</summary>
    NotFound,

    /// <summary>macOS < 13 OR the ObjC runtime call was not available on this host.</summary>
    NotSupported,
}

/// <summary>
/// Abstraction over the native SMAppService API.
/// Allows orchestrator logic to be fully tested without macOS infrastructure.
/// </summary>
public interface ISMAppServiceBridge
{
    /// <summary>
    /// Calls [SMAppService daemonWithPlistName:plistName] + registerAndReturnError.
    /// Returns the resulting status. Callers are expected to check status and
    /// surface RequiresApproval to the user if needed.
    /// </summary>
    SMAppServiceStatus RegisterDaemon(string plistName);

    /// <summary>Returns the current status without re-registering.</summary>
    SMAppServiceStatus GetStatus(string plistName);
}
