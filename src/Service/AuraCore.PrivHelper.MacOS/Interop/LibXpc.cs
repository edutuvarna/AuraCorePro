using System.Runtime.InteropServices;

namespace AuraCore.PrivHelper.MacOS.Interop;

/// <summary>
/// P/Invoke signatures for the subset of libxpc.dylib functions the
/// daemon needs. Deliberately narrow — each signature here becomes an
/// attack-surface entry, so we only declare what's used.
///
/// Task 25 ships SIGNATURES; Task 26 wires the actual xpc_main event loop.
///
/// All strings marshal as UTF-8 via <see cref="LibXpcMarshal"/> helpers
/// (Task 26) to avoid ANSI codepage pitfalls.
/// </summary>
internal static partial class LibXpc
{
    private const string Library = "libxpc.dylib";

    // --- Connection lifecycle ---

    [LibraryImport(Library)]
    internal static partial IntPtr xpc_connection_create_mach_service(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string name,
        IntPtr targetq,
        ulong flags);

    [LibraryImport(Library)]
    internal static partial void xpc_connection_resume(IntPtr connection);

    [LibraryImport(Library)]
    internal static partial void xpc_connection_cancel(IntPtr connection);

    // --- Dictionary construction (used for XPC replies) ---

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

    // --- Dictionary reading (used for XPC incoming calls) ---

    [LibraryImport(Library)]
    internal static partial IntPtr xpc_dictionary_get_string(
        IntPtr xdict,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string key);

    [LibraryImport(Library)]
    internal static partial IntPtr xpc_dictionary_get_array(
        IntPtr xdict,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string key);

    [LibraryImport(Library)]
    internal static partial long xpc_dictionary_get_int64(
        IntPtr xdict,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string key);

    // --- Array reading ---

    [LibraryImport(Library)]
    internal static partial nuint xpc_array_get_count(IntPtr xarray);

    [LibraryImport(Library)]
    internal static partial IntPtr xpc_array_get_string(IntPtr xarray, nuint index);

    // --- Message reply ---

    [LibraryImport(Library)]
    internal static partial void xpc_connection_send_message(IntPtr connection, IntPtr message);

    // --- Peer identity (used by handler to enforce code signature — Task 26) ---

    [LibraryImport(Library)]
    internal static partial int xpc_connection_get_pid(IntPtr connection);

    [LibraryImport(Library)]
    internal static partial uint xpc_connection_get_euid(IntPtr connection);
}
