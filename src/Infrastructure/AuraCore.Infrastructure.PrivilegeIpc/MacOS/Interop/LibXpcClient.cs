using System.Runtime.InteropServices;

namespace AuraCore.Infrastructure.PrivilegeIpc.MacOS.Interop;

/// <summary>
/// Client-side P/Invoke to libxpc.dylib. Narrower than the daemon's
/// <c>AuraCore.PrivHelper.MacOS.Interop.LibXpc</c> — client only needs
/// send + sync-reply.
/// </summary>
internal static partial class LibXpcClient
{
    private const string Library = "libxpc.dylib";

    /// <summary>Flag for creating an outgoing (client) connection — pass 0.</summary>
    internal const ulong XpcConnectionClientFlag = 0;

    [LibraryImport(Library)]
    internal static partial IntPtr xpc_connection_create_mach_service(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string name,
        IntPtr targetq,
        ulong flags);

    [LibraryImport(Library)]
    internal static partial void xpc_connection_resume(IntPtr connection);

    [LibraryImport(Library)]
    internal static partial void xpc_connection_cancel(IntPtr connection);

    [LibraryImport(Library)]
    internal static partial void xpc_release(IntPtr obj);

    [LibraryImport(Library)]
    internal static partial IntPtr xpc_dictionary_create(
        IntPtr keys, IntPtr values, nuint count);

    [LibraryImport(Library)]
    internal static partial void xpc_dictionary_set_string(
        IntPtr xdict,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string key,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string value);

    [LibraryImport(Library)]
    internal static partial void xpc_dictionary_set_int64(
        IntPtr xdict,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string key,
        long value);

    [LibraryImport(Library)]
    internal static partial IntPtr xpc_array_create(IntPtr objects, nuint count);

    [LibraryImport(Library)]
    internal static partial void xpc_array_append_value(IntPtr xarray, IntPtr value);

    [LibraryImport(Library)]
    internal static partial IntPtr xpc_string_create(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string str);

    [LibraryImport(Library)]
    internal static partial void xpc_dictionary_set_value(
        IntPtr xdict,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string key,
        IntPtr value);

    [LibraryImport(Library)]
    internal static partial IntPtr xpc_dictionary_get_string(
        IntPtr xdict,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string key);

    [LibraryImport(Library)]
    internal static partial long xpc_dictionary_get_int64(
        IntPtr xdict,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string key);

    /// <summary>
    /// Synchronous send-with-reply. Returns an xpc_dictionary reply
    /// or xpc_error_* on failure. Caller releases both request and reply.
    /// </summary>
    [LibraryImport(Library)]
    internal static partial IntPtr xpc_connection_send_message_with_reply_sync(
        IntPtr connection, IntPtr message);

    /// <summary>
    /// Returns the xpc_type_t id of an object. Used to detect error replies
    /// (xpc_error_connection_interrupted / xpc_error_connection_invalid).
    /// </summary>
    [LibraryImport(Library)]
    internal static partial IntPtr xpc_get_type(IntPtr obj);
}
