using System.Runtime.InteropServices;
using AuraCore.Infrastructure.PrivilegeIpc.MacOS.Interop;
using Microsoft.Extensions.Logging;

namespace AuraCore.Infrastructure.PrivilegeIpc.MacOS;

/// <summary>
/// Production implementation of <see cref="IBundleSignatureDetector"/> that uses
/// <c>SecCodeCopySelf</c> + <c>SecCodeCopySigningInformation</c> from the Security
/// framework to inspect the running app's code signature.
///
/// Returns <c>true</c> only when a non-empty, non-placeholder team ID is found in the
/// signing information — i.e., the build was signed with a real Apple Developer account.
///
/// On non-macOS hosts, or when the Security framework is unavailable, or on any
/// exception, returns <c>false</c> (conservative: treat as unsigned → DevModeFallback).
/// </summary>
public sealed class DefaultBundleSignatureDetector : IBundleSignatureDetector
{
    // Placeholder team IDs used by ad-hoc or self-signed builds.
    // Real Apple-issued team IDs are 10 alphanumeric chars (e.g. "A1B2C3D4E5").
    private static readonly HashSet<string> PlaceholderTeamIds = new(StringComparer.OrdinalIgnoreCase)
    {
        "",
        "-",
        "?",
        "0000000000",
    };

    private readonly ILogger<DefaultBundleSignatureDetector>? _logger;

    // Constructor with logger — used via DI.
    public DefaultBundleSignatureDetector(ILogger<DefaultBundleSignatureDetector> logger)
        => _logger = logger;

    // Parameterless constructor — convenience for tests that don't need logging.
    public DefaultBundleSignatureDetector()
        => _logger = null;

    /// <inheritdoc/>
    public bool IsBundleProperlySignedWithTeamId()
    {
        if (!OperatingSystem.IsMacOS())
            return false;

        try
        {
            return ReadTeamIdFromSelf() is { Length: > 0 } teamId
                   && !PlaceholderTeamIds.Contains(teamId);
        }
        catch (DllNotFoundException ex)
        {
            _logger?.LogDebug(ex, "[bundle-sig] Security framework not available");
            return false;
        }
        catch (EntryPointNotFoundException ex)
        {
            _logger?.LogDebug(ex, "[bundle-sig] Security framework entry point not found");
            return false;
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "[bundle-sig] IsBundleProperlySignedWithTeamId threw");
            return false;
        }
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    /// <summary>
    /// Reads the kSecCodeInfoTeamIdentifier value from the running process's own
    /// code-signing information. Returns null if any step fails.
    /// </summary>
    private string? ReadTeamIdFromSelf()
    {
        var err = SecCodeSelf.SecCodeCopySelf(SecCodeSelf.DefaultFlags, out var selfCode);
        if (err != 0 || selfCode == IntPtr.Zero)
        {
            _logger?.LogDebug("[bundle-sig] SecCodeCopySelf failed (err={Err})", err);
            return null;
        }

        try
        {
            err = SecCodeSelf.SecCodeCopySigningInformation(
                selfCode,
                SecCodeSelf.SigningInformation,
                out var infoDict);

            if (err != 0 || infoDict == IntPtr.Zero)
            {
                _logger?.LogDebug("[bundle-sig] SecCodeCopySigningInformation failed (err={Err})", err);
                return null;
            }

            try
            {
                return ExtractTeamId(infoDict);
            }
            finally
            {
                SecCodeSelf.CFRelease(infoDict);
            }
        }
        finally
        {
            SecCodeSelf.CFRelease(selfCode);
        }
    }

    private string? ExtractTeamId(IntPtr infoDict)
    {
        // Resolve the kSecCodeInfoTeamIdentifier CFString key at runtime.
        var keyPtr = SecCodeSelf.GetTeamIdentifierKey();
        if (keyPtr == IntPtr.Zero)
        {
            _logger?.LogDebug("[bundle-sig] kSecCodeInfoTeamIdentifier key not found");
            return null;
        }

        // Dereference: keyPtr is a pointer-to-CFStringRef (an exported symbol).
        var cfStringKey = Marshal.ReadIntPtr(keyPtr);
        if (cfStringKey == IntPtr.Zero)
            return null;

        var valuePtr = SecCodeSelf.CFDictionaryGetValue(infoDict, cfStringKey);
        if (valuePtr == IntPtr.Zero)
        {
            _logger?.LogDebug("[bundle-sig] kSecCodeInfoTeamIdentifier absent from info dict (unsigned build)");
            return null;
        }

        // Read the CFString value into a managed string.
        var buffer = new byte[64];
        if (!SecCodeSelf.CFStringGetCString(valuePtr, buffer, buffer.Length, SecCodeSelf.UTF8Encoding))
        {
            _logger?.LogDebug("[bundle-sig] CFStringGetCString failed for team ID");
            return null;
        }

        // Find the null terminator in the buffer and return the string.
        var nullIdx = Array.IndexOf(buffer, (byte)0);
        return System.Text.Encoding.UTF8.GetString(buffer, 0, nullIdx < 0 ? buffer.Length : nullIdx);
    }
}
