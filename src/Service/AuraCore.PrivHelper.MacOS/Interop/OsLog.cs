using System.Runtime.InteropServices;

namespace AuraCore.PrivHelper.MacOS.Interop;

/// <summary>
/// P/Invoke signatures for <c>os_log</c> from <c>libsystem_trace.dylib</c>.
/// Used by <see cref="AuraCore.PrivHelper.MacOS.AuditLogger"/> to write
/// structured audit events visible via <c>log show --predicate</c>.
///
/// <para>
/// On non-macOS hosts (Windows dev boxes) these P/Invokes will throw
/// <see cref="DllNotFoundException"/> at call-time. <see cref="AuditLogger"/>
/// catches that and falls back to <c>Console.Error.WriteLine</c> with a
/// <c>[PRIVHELPER]</c> prefix so that <c>launchd</c>'s
/// <c>StandardErrorPath</c> captures audit events even without os_log.
/// </para>
///
/// Subsystem: <c>pro.auracore.privhelper</c>.
/// Categories: <c>audit</c>, <c>xpc</c>, <c>lifecycle</c>.
/// </summary>
internal static partial class OsLog
{
    private const string Library = "/usr/lib/system/libsystem_trace.dylib";

    // os_log_type_t constants — matches <os/log.h>
    internal const byte OS_LOG_TYPE_DEFAULT = 0x00;
    internal const byte OS_LOG_TYPE_INFO    = 0x01;
    internal const byte OS_LOG_TYPE_DEBUG   = 0x02;
    internal const byte OS_LOG_TYPE_ERROR   = 0x10;
    internal const byte OS_LOG_TYPE_FAULT   = 0x11;

    /// <summary>
    /// Creates a named os_log handle for the given subsystem + category pair.
    /// Returns OS_LOG_DISABLED (<see cref="IntPtr.Zero"/>) if the subsystem/category
    /// are not enabled, which the caller must handle gracefully.
    /// </summary>
    [LibraryImport(Library)]
    internal static partial IntPtr os_log_create(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string subsystem,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string category);

    /// <summary>
    /// Low-level os_log write. The <paramref name="format"/> is a C-string
    /// printf-style format; for privacy reasons we emit pre-formatted strings
    /// (the format IS the full message, with no substitution args) so that
    /// <c>%{public}s</c> annotations are not needed.
    /// </summary>
    [LibraryImport(Library)]
    internal static partial void os_log_impl(
        IntPtr dso,
        IntPtr log,
        byte type,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string format,
        IntPtr buf,
        uint size);
}
