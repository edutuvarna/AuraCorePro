using System.Runtime.InteropServices;

namespace AuraCore.PrivHelper.MacOS.Interop;

/// <summary>
/// P/Invoke signatures for the subset of the Security framework
/// the daemon uses to verify the XPC client's code signature + team ID
/// before accepting privileged operations.
///
/// Task 25 ships SIGNATURES; Task 26 wires the actual verification logic
/// (SecCodeCheckValidity with a code requirement of "anchor apple generic
/// and identifier &quot;pro.auracore.auracorepro&quot; and certificate
/// leaf[subject.OU] = &quot;&lt;team-id&gt;&quot;").
/// </summary>
internal static partial class SecCode
{
    private const string Library = "/System/Library/Frameworks/Security.framework/Security";

    /// <summary>Returns the SecCode that represents the given PID as a guest of the host.</summary>
    [LibraryImport(Library)]
    internal static partial int SecCodeCopyGuestWithAttributes(
        IntPtr host,
        IntPtr attributes,
        uint flags,
        out IntPtr outGuest);

    /// <summary>Validates the code signature + optional requirement.</summary>
    [LibraryImport(Library)]
    internal static partial int SecCodeCheckValidity(
        IntPtr code,
        uint flags,
        IntPtr requirement);

    /// <summary>Creates a SecRequirement from a requirement text.</summary>
    [LibraryImport(Library)]
    internal static partial int SecRequirementCreateWithString(
        IntPtr requirementText,
        uint flags,
        out IntPtr outRequirement);

    /// <summary>Flag: kSecCSDefaultFlags = 0.</summary>
    internal const uint DefaultFlags = 0;

    /// <summary>Flag: kSecCSStrictValidate — strict cert chain validation.</summary>
    internal const uint StrictValidate = 1 << 3;
}
