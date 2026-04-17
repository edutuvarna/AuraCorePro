using AuraCore.Infrastructure.PrivilegeIpc.MacOS.Interop;
using Microsoft.Extensions.Logging;

namespace AuraCore.Infrastructure.PrivilegeIpc.MacOS;

/// <summary>
/// Production implementation of <see cref="ISMAppServiceBridge"/> that uses the
/// Objective-C runtime via P/Invoke to call <c>SMAppService</c> from
/// ServiceManagement.framework (macOS 13+).
///
/// On non-macOS hosts, or whenever the ObjC runtime calls are unavailable
/// (macOS &lt; 13, missing framework, entry point not found), all methods
/// return <see cref="SMAppServiceStatus.NotSupported"/> — callers treat this
/// as a signal to fall back to InProcess mode.
///
/// TODO(Task 30b): If the P/Invoke-to-objc_msgSend path proves brittle for
/// the <c>registerAndReturnError:</c> NSError out-param on ARM64 (stret calling
/// convention edge cases), replace the inner dispatch with a thin Swift shim
/// .dylib that wraps the Swift-native SMAppService API.  The interface seam
/// (<see cref="ISMAppServiceBridge"/>) already isolates this from the rest of
/// the codebase.
/// </summary>
public sealed class DefaultSMAppServiceBridge : ISMAppServiceBridge
{
    // SMAppServiceStatus enum values from ServiceManagement.framework headers (macOS 13+).
    // SMAppServiceStatusNotRegistered = 0
    // SMAppServiceStatusEnabled       = 1
    // SMAppServiceStatusRequiresApproval = 2
    // SMAppServiceStatusNotFound      = 3
    private const long NativeNotRegistered     = 0;
    private const long NativeEnabled           = 1;
    private const long NativeRequiresApproval  = 2;
    private const long NativeNotFound          = 3;

    private readonly ILogger<DefaultSMAppServiceBridge> _logger;

    public DefaultSMAppServiceBridge(ILogger<DefaultSMAppServiceBridge> logger)
        => _logger = logger;

    /// <inheritdoc/>
    public SMAppServiceStatus RegisterDaemon(string plistName)
    {
        if (!OperatingSystem.IsMacOS())
            return SMAppServiceStatus.NotSupported;

        try
        {
            ObjCRuntime.EnsureServiceManagementLoaded();

            var smClass = ObjCRuntime.objc_getClass("SMAppService");
            if (smClass == IntPtr.Zero)
            {
                _logger.LogWarning("[smappservice] SMAppService class not found — macOS < 13?");
                return SMAppServiceStatus.NotSupported;
            }

            // [SMAppService daemonWithPlistName:plistName]
            var daemonSel   = ObjCRuntime.sel_registerName("daemonWithPlistName:");
            var nsString    = ObjCRuntime.NSStringFromString(plistName);
            var instance    = ObjCRuntime.objc_msgSend_id(smClass, daemonSel, nsString);
            if (instance == IntPtr.Zero)
            {
                _logger.LogWarning("[smappservice] daemonWithPlistName: returned nil for {Plist}", plistName);
                return SMAppServiceStatus.NotFound;
            }

            // [instance registerAndReturnError:&error]
            // TODO(Task 30b): NSError out-param marshalling may need a Swift shim on ARM64.
            var registerSel = ObjCRuntime.sel_registerName("registerAndReturnError:");
            _ = ObjCRuntime.objc_msgSend_bool_out_ptr(instance, registerSel, out _);
            // We intentionally ignore the NSError pointer — SMAppService status is the
            // authoritative result source; the error is only for diagnostic text.

            // Read current status after register attempt
            return GetStatusForInstance(instance, plistName);
        }
        catch (DllNotFoundException ex)
        {
            _logger.LogWarning(ex, "[smappservice] ObjC runtime library not available");
            return SMAppServiceStatus.NotSupported;
        }
        catch (EntryPointNotFoundException ex)
        {
            _logger.LogWarning(ex, "[smappservice] ObjC runtime entry point not found");
            return SMAppServiceStatus.NotSupported;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[smappservice] RegisterDaemon threw unexpectedly for {Plist}", plistName);
            return SMAppServiceStatus.NotSupported;
        }
    }

    /// <inheritdoc/>
    public SMAppServiceStatus GetStatus(string plistName)
    {
        if (!OperatingSystem.IsMacOS())
            return SMAppServiceStatus.NotSupported;

        try
        {
            ObjCRuntime.EnsureServiceManagementLoaded();

            var smClass = ObjCRuntime.objc_getClass("SMAppService");
            if (smClass == IntPtr.Zero)
                return SMAppServiceStatus.NotSupported;

            var daemonSel = ObjCRuntime.sel_registerName("daemonWithPlistName:");
            var nsString  = ObjCRuntime.NSStringFromString(plistName);
            var instance  = ObjCRuntime.objc_msgSend_id(smClass, daemonSel, nsString);
            if (instance == IntPtr.Zero)
                return SMAppServiceStatus.NotFound;

            return GetStatusForInstance(instance, plistName);
        }
        catch (DllNotFoundException ex)
        {
            _logger.LogWarning(ex, "[smappservice] ObjC runtime library not available");
            return SMAppServiceStatus.NotSupported;
        }
        catch (EntryPointNotFoundException ex)
        {
            _logger.LogWarning(ex, "[smappservice] ObjC runtime entry point not found");
            return SMAppServiceStatus.NotSupported;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[smappservice] GetStatus threw unexpectedly for {Plist}", plistName);
            return SMAppServiceStatus.NotSupported;
        }
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    private SMAppServiceStatus GetStatusForInstance(IntPtr instance, string plistName)
    {
        var statusSel  = ObjCRuntime.sel_registerName("status");
        var nativeStatus = ObjCRuntime.objc_msgSend_long(instance, statusSel);

        var result = MapNativeStatus(nativeStatus);
        _logger.LogDebug("[smappservice] status={Status} (native={Native}) plist={Plist}",
            result, nativeStatus, plistName);
        return result;
    }

    private static SMAppServiceStatus MapNativeStatus(long native) => native switch
    {
        NativeEnabled          => SMAppServiceStatus.Enabled,
        NativeRequiresApproval => SMAppServiceStatus.RequiresApproval,
        NativeNotFound         => SMAppServiceStatus.NotFound,
        NativeNotRegistered    => SMAppServiceStatus.NotRegistered,
        _                      => SMAppServiceStatus.NotSupported,
    };
}
