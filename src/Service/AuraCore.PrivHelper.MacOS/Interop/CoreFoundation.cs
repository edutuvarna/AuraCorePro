using System.Runtime.InteropServices;

namespace AuraCore.PrivHelper.MacOS.Interop;

/// <summary>
/// Minimal P/Invoke surface for Core Foundation — just what
/// <see cref="PeerVerifier"/> needs to build a
/// <c>CFDictionary{kSecGuestAttributePid: CFNumber(pid)}</c> to pass
/// to <c>SecCodeCopyGuestWithAttributes</c>.
///
/// All refs returned by Create/Copy functions must be released via
/// <see cref="CFRelease"/>. Use try/finally in callers.
///
/// The <c>kCFTypeDictionaryKeyCallBacks</c> and
/// <c>kCFTypeDictionaryValueCallBacks</c> exported data symbols are resolved
/// lazily via <see cref="NativeLibrary.GetExport"/> so CoreFoundation is NOT
/// loaded on Windows/Linux dev hosts (lazy init only fires on first access,
/// which is gated by <c>OperatingSystem.IsMacOS() &amp;&amp; !stubMode</c>
/// in <see cref="PeerVerifier"/>).
/// </summary>
internal static partial class CoreFoundation
{
    private const string Library =
        "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";

    // --- Apple constants ---

    /// <summary>kCFStringEncodingUTF8 per CFString.h.</summary>
    internal const uint KCFStringEncodingUTF8 = 0x08000100;

    /// <summary>kCFNumberIntType (C int = 32-bit signed) per CFNumber.h.</summary>
    internal const int KCFNumberIntType = 9;

    // --- String / number / dictionary creation ---

    /// <summary>
    /// Creates an immutable CFString from a C (UTF-8) string.
    /// Returns IntPtr.Zero on failure. Caller must CFRelease the result.
    /// </summary>
    [LibraryImport(Library)]
    internal static partial IntPtr CFStringCreateWithCString(
        IntPtr allocator,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string cstr,
        uint encoding);

    /// <summary>
    /// Creates a CFNumber of the given type, reading the value from
    /// <paramref name="valuePtr"/>. Caller must CFRelease the result.
    /// </summary>
    [LibraryImport(Library)]
    internal static partial IntPtr CFNumberCreate(
        IntPtr allocator,
        int type,
        IntPtr valuePtr);

    /// <summary>
    /// Creates a CFDictionary with the given parallel key/value arrays.
    /// Caller must CFRelease the result.
    /// </summary>
    [LibraryImport(Library)]
    internal static partial IntPtr CFDictionaryCreate(
        IntPtr allocator,
        IntPtr[] keys,
        IntPtr[] values,
        nint numValues,
        IntPtr keyCallBacks,
        IntPtr valueCallBacks);

    /// <summary>
    /// Releases a Core Foundation object. Safe to call with IntPtr.Zero
    /// per Apple docs, but callers should guard explicitly for clarity.
    /// </summary>
    [LibraryImport(Library)]
    internal static partial void CFRelease(IntPtr cf);

    // --- Exported data symbols (dictionary callback tables) ---
    //
    // These are data symbols (not functions), so they cannot be resolved via
    // [LibraryImport]. We resolve them lazily via NativeLibrary.GetExport.
    // The Lazy<T> ensures CoreFoundation.framework is NOT loaded until the
    // first call into the real macOS verification path.

    private static readonly Lazy<IntPtr> TypeDictionaryKeyCallBacksLazy = new(() =>
    {
        var lib = NativeLibrary.Load(Library);
        return NativeLibrary.GetExport(lib, "kCFTypeDictionaryKeyCallBacks");
    });

    private static readonly Lazy<IntPtr> TypeDictionaryValueCallBacksLazy = new(() =>
    {
        var lib = NativeLibrary.Load(Library);
        return NativeLibrary.GetExport(lib, "kCFTypeDictionaryValueCallBacks");
    });

    /// <summary>Pointer to kCFTypeDictionaryKeyCallBacks — lazily loaded from CoreFoundation.</summary>
    internal static IntPtr KCFTypeDictionaryKeyCallBacks => TypeDictionaryKeyCallBacksLazy.Value;

    /// <summary>Pointer to kCFTypeDictionaryValueCallBacks — lazily loaded from CoreFoundation.</summary>
    internal static IntPtr KCFTypeDictionaryValueCallBacks => TypeDictionaryValueCallBacksLazy.Value;
}
