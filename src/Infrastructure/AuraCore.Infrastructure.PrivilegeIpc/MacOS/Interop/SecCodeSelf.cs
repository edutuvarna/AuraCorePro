using System.Runtime.InteropServices;

namespace AuraCore.Infrastructure.PrivilegeIpc.MacOS.Interop;

/// <summary>
/// P/Invoke signatures for reading the running process's own code signature
/// via the Security framework. Used by <see cref="DefaultBundleSignatureDetector"/>
/// to determine whether the main app bundle is properly signed with a real Team ID.
///
/// This is the client-side mirror of the daemon's SecCode.cs (AuraCore.PrivHelper.MacOS.Interop).
/// The daemon uses SecCodeCopyGuestWithAttributes to inspect XPC caller PIDs;
/// we use SecCodeCopySelf + SecCodeCopySigningInformation to inspect our own signature.
/// </summary>
internal static partial class SecCodeSelf
{
    private const string Library = "/System/Library/Frameworks/Security.framework/Security";

    // ── SecCode constants ────────────────────────────────────────────────────

    /// <summary>kSecCSDefaultFlags = 0</summary>
    internal const uint DefaultFlags = 0;

    /// <summary>kSecCSSigningInformation = 0x02 — include signing identity in info dict.</summary>
    internal const uint SigningInformation = 0x02;

    // ── API declarations ─────────────────────────────────────────────────────

    /// <summary>
    /// Returns the SecStaticCode representing the running process's own code object.
    /// Equivalent to <c>SecCodeCopySelf(kSecCSDefaultFlags, &amp;outCode)</c>.
    /// </summary>
    [LibraryImport(Library)]
    internal static partial int SecCodeCopySelf(uint flags, out IntPtr outCode);

    /// <summary>
    /// Copies the signing information dictionary for a code object.
    /// On success, <paramref name="outInfo"/> is a CFDictionary (must be CFRelease'd).
    /// </summary>
    [LibraryImport(Library)]
    internal static partial int SecCodeCopySigningInformation(
        IntPtr code, uint flags, out IntPtr outInfo);

    /// <summary>
    /// Decrements the retain count of a Core Foundation object.
    /// Call on any CF object returned by the APIs above when you're done.
    /// </summary>
    [LibraryImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation",
        EntryPoint = "CFRelease")]
    internal static partial void CFRelease(IntPtr cfObj);

    // ── CFDictionary / CFString helpers ──────────────────────────────────────

    /// <summary>
    /// Returns the value for a given key from a CFDictionary, or IntPtr.Zero
    /// if the key is absent. The returned value is NOT retained.
    /// </summary>
    [LibraryImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation",
        EntryPoint = "CFDictionaryGetValue")]
    internal static partial IntPtr CFDictionaryGetValue(IntPtr dict, IntPtr key);

    /// <summary>
    /// Retrieves the characters of a CFString into a pre-allocated char buffer.
    /// Returns true on success.
    /// </summary>
    [LibraryImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation",
        EntryPoint = "CFStringGetCString")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool CFStringGetCString(
        IntPtr cfString, byte[] buffer, int bufferSize, uint encoding);

    /// <summary>kCFStringEncodingUTF8 = 0x08000100</summary>
    internal const uint UTF8Encoding = 0x08000100;

    // ── kSecCodeInfoTeamIdentifier key ───────────────────────────────────────
    // This is a CFStringRef constant exported from Security.framework.
    // We obtain it via dlsym rather than a direct P/Invoke since CF string
    // constants are not directly expressible as IntPtr P/Invoke return values.

    [LibraryImport("libdl.dylib")]
    internal static partial IntPtr dlsym(IntPtr handle, [MarshalAs(UnmanagedType.LPStr)] string symbol);

    [LibraryImport("libdl.dylib")]
    internal static partial IntPtr dlopen([MarshalAs(UnmanagedType.LPStr)] string path, int mode);

    private const int RTLD_LAZY   = 0x1;
    private const int RTLD_NOLOAD = 0x10;

    /// <summary>
    /// Reads the value of the <c>kSecCodeInfoTeamIdentifier</c> CF constant from the
    /// Security framework at runtime.  Returns IntPtr.Zero if the framework is not loaded.
    /// </summary>
    internal static IntPtr GetTeamIdentifierKey()
    {
        // RTLD_NOLOAD — only succeed if already in memory (no side-effect load).
        // Fall back to RTLD_LAZY load if not yet present (edge case in thin hosts).
        const string SecurityFw =
            "/System/Library/Frameworks/Security.framework/Security";

        var handle = dlopen(SecurityFw, RTLD_NOLOAD);
        if (handle == IntPtr.Zero)
            handle = dlopen(SecurityFw, RTLD_LAZY);

        return handle == IntPtr.Zero
            ? IntPtr.Zero
            : dlsym(handle, "kSecCodeInfoTeamIdentifier");
    }
}
