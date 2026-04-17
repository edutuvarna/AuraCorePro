using System.Runtime.InteropServices;

namespace AuraCore.Infrastructure.PrivilegeIpc.MacOS.Interop;

/// <summary>
/// P/Invoke declarations for the Objective-C runtime.
/// Used by <see cref="DefaultSMAppServiceBridge"/> to call SMAppService
/// via dynamic dispatch without requiring a Swift/ObjC compile-time dependency.
///
/// NOTE: The <c>objc_msgSend</c> family uses a variadic calling convention.
/// These overloads are typed for the specific message signatures we need.
/// The <c>registerAndReturnError:</c> selector requires passing an IntPtr* (out NSError)
/// as the first variadic argument — see Task 30b follow-up for a safer Swift shim.dylib
/// if this P/Invoke path proves brittle on ARM64 (stret variant considerations).
/// </summary>
internal static partial class ObjCRuntime
{
    private const string ObjCLib    = "/usr/lib/libobjc.A.dylib";
    private const string ServiceMgmt = "/System/Library/Frameworks/ServiceManagement.framework/ServiceManagement";

    // ── Runtime lookups ──────────────────────────────────────────────────────

    [LibraryImport(ObjCLib)]
    internal static partial IntPtr objc_getClass(
        [MarshalAs(UnmanagedType.LPStr)] string name);

    [LibraryImport(ObjCLib)]
    internal static partial IntPtr sel_registerName(
        [MarshalAs(UnmanagedType.LPStr)] string str);

    // ── objc_msgSend overloads ───────────────────────────────────────────────
    // Each overload models a distinct call signature we need.

    /// <summary>Class-method call that takes an NSString argument; returns id (IntPtr).</summary>
    [LibraryImport(ObjCLib, EntryPoint = "objc_msgSend")]
    internal static partial IntPtr objc_msgSend_id(IntPtr receiver, IntPtr selector, IntPtr arg0);

    /// <summary>Instance-method call that takes an IntPtr* (out NSError) argument; returns BOOL (byte).</summary>
    [LibraryImport(ObjCLib, EntryPoint = "objc_msgSend")]
    internal static partial byte objc_msgSend_bool_out_ptr(IntPtr receiver, IntPtr selector, out IntPtr outArg);

    /// <summary>Instance property getter that returns NSInteger (nint → long).</summary>
    [LibraryImport(ObjCLib, EntryPoint = "objc_msgSend")]
    internal static partial long objc_msgSend_long(IntPtr receiver, IntPtr selector);

    // ── NSString helpers ─────────────────────────────────────────────────────

    /// <summary>
    /// Creates an NSString* from a C UTF-8 string literal using
    /// +[NSString stringWithUTF8String:].
    /// </summary>
    internal static IntPtr NSStringFromString(string s)
    {
        var nsStringClass = objc_getClass("NSString");
        var sel = sel_registerName("stringWithUTF8String:");
        // objc_msgSend with const char* arg — use LPStr overload
        return objc_msgSend_string(nsStringClass, sel, s);
    }

    [LibraryImport(ObjCLib, EntryPoint = "objc_msgSend")]
    private static partial IntPtr objc_msgSend_string(
        IntPtr receiver, IntPtr selector,
        [MarshalAs(UnmanagedType.LPStr)] string arg0);

    // ── ServiceManagement framework load ─────────────────────────────────────

    /// <summary>
    /// Ensures ServiceManagement.framework is loaded into the process so that
    /// objc_getClass("SMAppService") can find the class.
    /// Calling this on macOS < 13 is safe — the dlopen will succeed but
    /// SMAppService class lookup will return NULL (handled by callers).
    /// </summary>
    [LibraryImport("libdl.dylib")]
    private static partial IntPtr dlopen(
        [MarshalAs(UnmanagedType.LPStr)] string path, int mode);

    private const int RTLD_LAZY = 0x1;

    internal static void EnsureServiceManagementLoaded()
    {
        // Best-effort — ignore return value; if framework is already loaded this is a no-op.
        dlopen(ServiceMgmt, RTLD_LAZY);
    }
}
