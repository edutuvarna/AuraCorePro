using AuraCore.PrivHelper.MacOS.Interop;
using Microsoft.Extensions.Logging;

namespace AuraCore.PrivHelper.MacOS;

// ---------------------------------------------------------------------------
// PeerVerifier — domain types
// ---------------------------------------------------------------------------

/// <summary>Verified identity of an accepted XPC peer.</summary>
internal readonly record struct PeerIdentity(int Pid, string BundleId, string TeamId);

/// <summary>Outcome of a <see cref="IPeerVerifier.Verify"/> call.</summary>
internal sealed record PeerVerificationResult(bool Ok, PeerIdentity? Identity, string? RejectionReason);

// ---------------------------------------------------------------------------
// IPeerVerifier contract
// ---------------------------------------------------------------------------

/// <summary>
/// Verifies an XPC peer (identified by PID) matches the expected bundle id +
/// team id via <c>SecCodeCheckValidity</c>. Returns a <see cref="PeerVerificationResult"/>
/// with <see cref="PeerVerificationResult.Ok"/> = <c>true</c> on success or a
/// human-readable <see cref="PeerVerificationResult.RejectionReason"/> on failure.
/// </summary>
internal interface IPeerVerifier
{
    PeerVerificationResult Verify(int peerPid);
}

// ---------------------------------------------------------------------------
// PeerVerifier implementation
// ---------------------------------------------------------------------------

/// <summary>
/// Production implementation of <see cref="IPeerVerifier"/>.
/// <para>
/// On a macOS host it calls <c>SecCodeCopyGuestWithAttributes</c> +
/// <c>SecCodeCheckValidity</c> from the Security framework to enforce that
/// the calling process is code-signed with the expected bundle ID and team ID.
/// </para>
/// <para>
/// On non-macOS hosts (Windows dev boxes) the P/Invoke calls would throw
/// <see cref="DllNotFoundException"/>. The constructor catches this eagerly and
/// sets <see cref="_nonMacSoftFail"/> = <c>true</c>, which causes every
/// <see cref="Verify"/> call to return Ok=true with a
/// <c>TeamId="SOFT_FAIL_DEBUG"</c> marker so callers can detect the mode.
/// </para>
/// <para>
/// <paramref name="stubMode"/> = <c>true</c> bypasses all P/Invoke entirely —
/// used exclusively by unit tests to exercise the rejection/audit code paths
/// without a real macOS host.
/// </para>
/// </summary>
internal sealed class PeerVerifier : IPeerVerifier
{
    private readonly bool _stubMode;
    private readonly bool _nonMacSoftFail;
    private readonly ILogger<PeerVerifier>? _logger;

    // Requirement text: caller must be code-signed, have our expected
    // bundle id, and come from our expected Apple Developer Team.
    private const string RequirementTemplate =
        "anchor apple generic and identifier \"{0}\" " +
        "and certificate leaf[subject.OU] = \"{1}\"";

    public PeerVerifier(bool stubMode = false, ILogger<PeerVerifier>? logger = null)
    {
        _stubMode = stubMode;
        _logger = logger;

        if (!stubMode && !OperatingSystem.IsMacOS())
        {
            // Non-Mac host: eagerly record that real SecCode P/Invoke will fail.
            _nonMacSoftFail = true;
            _logger?.LogWarning(
                "[PeerVerifier] Non-macOS host detected — SecCode verification soft-failing " +
                "(all peers accepted with SOFT_FAIL_DEBUG marker). This MUST NOT reach production.");
        }
    }

    /// <inheritdoc />
    public PeerVerificationResult Verify(int peerPid)
    {
        if (peerPid < SecurityConfig.MinValidPid)
        {
            return new PeerVerificationResult(
                false, null, $"invalid pid {peerPid}: must be >= {SecurityConfig.MinValidPid}");
        }

        // --- Stub mode (tests only) ---
        if (_stubMode)
        {
            return new PeerVerificationResult(
                true,
                new PeerIdentity(peerPid, SecurityConfig.ExpectedBundleId, SecurityConfig.ExpectedTeamId),
                null);
        }

        // --- Non-Mac soft-fail (Windows dev host) ---
        if (_nonMacSoftFail)
        {
#if RELEASE
            // Hard-fail in Release builds: cannot verify, must reject.
            return new PeerVerificationResult(false, null,
                "SecCode verification unavailable on non-macOS host (RELEASE build)");
#else
            _logger?.LogWarning(
                "[PeerVerifier] SOFT_FAIL_DEBUG — pid={Pid} accepted without real SecCode check", peerPid);
            return new PeerVerificationResult(
                true,
                new PeerIdentity(peerPid, SecurityConfig.ExpectedBundleId, "SOFT_FAIL_DEBUG"),
                null);
#endif
        }

        // --- Real macOS SecCode verification ---
        return VerifyWithSecCode(peerPid);
    }

    private PeerVerificationResult VerifyWithSecCode(int peerPid)
    {
        // Check placeholder team ID — soft-fail in DEBUG, hard-fail in RELEASE.
        if (SecurityConfig.IsTeamIdPlaceholder(SecurityConfig.ExpectedTeamId))
        {
#if RELEASE
            return new PeerVerificationResult(false, null,
                "AURACORE_TEAM_ID_PLACEHOLDER not substituted at signing time (RELEASE hard-fail)");
#else
            _logger?.LogWarning(
                "[PeerVerifier] ExpectedTeamId is placeholder — soft-failing for DEBUG build. " +
                "Signing pipeline must substitute before Release.");
            return new PeerVerificationResult(
                true,
                new PeerIdentity(peerPid, SecurityConfig.ExpectedBundleId, "SOFT_FAIL_PLACEHOLDER"),
                null);
#endif
        }

        // CF refs allocated in this method — released unconditionally in the finally block.
        // Release order is reverse of allocation order (6 refs total).
        IntPtr cfKey              = IntPtr.Zero;  // (1) CFString "pid"
        IntPtr cfValue            = IntPtr.Zero;  // (2) CFNumber(peerPid)
        IntPtr cfAttrs            = IntPtr.Zero;  // (3) CFDictionary { "pid": pid }
        IntPtr guestCode          = IntPtr.Zero;  // (4) SecCodeRef for peer
        IntPtr cfRequirementStr   = IntPtr.Zero;  // (5) CFString requirement text
        IntPtr requirement        = IntPtr.Zero;  // (6) SecRequirementRef

        try
        {
            // ── Step 1: Build CFString key "pid" (= kSecGuestAttributePid) ──────────
            cfKey = CoreFoundation.CFStringCreateWithCString(
                IntPtr.Zero, "pid", CoreFoundation.KCFStringEncodingUTF8);
            if (cfKey == IntPtr.Zero)
                return new PeerVerificationResult(false, null,
                    "CFStringCreateWithCString for 'pid' key failed");

            // ── Step 2: Build CFNumber wrapping the peer PID ──────────────────────
            // CFNumberCreate reads the value through a pointer; use unsafe to take
            // the address of a stack-local int. AllowUnsafeBlocks=true (Task 25).
            int pidBoxed = peerPid;
            unsafe
            {
                cfValue = CoreFoundation.CFNumberCreate(
                    IntPtr.Zero, CoreFoundation.KCFNumberIntType, (IntPtr)(&pidBoxed));
            }
            if (cfValue == IntPtr.Zero)
                return new PeerVerificationResult(false, null,
                    "CFNumberCreate for pid failed");

            // ── Step 3: Build CFDictionary { "pid": <pidCFNumber> } ───────────────
            var keysArr   = new[] { cfKey };
            var valuesArr = new[] { cfValue };
            cfAttrs = CoreFoundation.CFDictionaryCreate(
                IntPtr.Zero, keysArr, valuesArr, 1,
                CoreFoundation.KCFTypeDictionaryKeyCallBacks,
                CoreFoundation.KCFTypeDictionaryValueCallBacks);
            if (cfAttrs == IntPtr.Zero)
                return new PeerVerificationResult(false, null,
                    "CFDictionaryCreate for attrs failed");

            // ── Step 4: Resolve SecCode for the peer PID ─────────────────────────
            int rc = SecCode.SecCodeCopyGuestWithAttributes(
                host: IntPtr.Zero,
                attributes: cfAttrs,
                flags: SecCode.DefaultFlags,
                outGuest: out guestCode);
            if (rc != 0 || guestCode == IntPtr.Zero)
                return new PeerVerificationResult(false, null,
                    $"SecCodeCopyGuestWithAttributes returned OSStatus={rc} for pid {peerPid}");

            // ── Step 5: Build requirement CFString ────────────────────────────────
            var requirementText = string.Format(RequirementTemplate,
                SecurityConfig.ExpectedBundleId, SecurityConfig.ExpectedTeamId);
            cfRequirementStr = CoreFoundation.CFStringCreateWithCString(
                IntPtr.Zero, requirementText, CoreFoundation.KCFStringEncodingUTF8);
            if (cfRequirementStr == IntPtr.Zero)
                return new PeerVerificationResult(false, null,
                    "CFStringCreateWithCString for requirement text failed");

            // ── Step 6: Create SecRequirementRef from the requirement text ────────
            int reqRc = SecCode.SecRequirementCreateWithString(
                cfRequirementStr, SecCode.DefaultFlags, out requirement);
            if (reqRc != 0 || requirement == IntPtr.Zero)
                return new PeerVerificationResult(false, null,
                    $"SecRequirementCreateWithString returned OSStatus={reqRc}");

            // ── Step 7: Enforce strict validation against the requirement ─────────
            int checkRc = SecCode.SecCodeCheckValidity(
                code: guestCode,
                flags: SecCode.DefaultFlags | SecCode.StrictValidate,
                requirement: requirement);
            if (checkRc != 0)
                return new PeerVerificationResult(false, null,
                    $"SecCodeCheckValidity rejected pid {peerPid} with OSStatus={checkRc}");

            return new PeerVerificationResult(
                true,
                new PeerIdentity(peerPid, SecurityConfig.ExpectedBundleId, SecurityConfig.ExpectedTeamId),
                null);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "[PeerVerifier] CF/SecCode verification exception for pid={Pid}", peerPid);
            return new PeerVerificationResult(false, null,
                $"SecCode verification exception: {ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            // Release in reverse-allocation order. Guard each with != IntPtr.Zero
            // for clarity even though CFRelease(NULL) is safe per Apple docs.
            if (requirement      != IntPtr.Zero) CoreFoundation.CFRelease(requirement);      // (6)
            if (cfRequirementStr != IntPtr.Zero) CoreFoundation.CFRelease(cfRequirementStr); // (5)
            if (guestCode        != IntPtr.Zero) CoreFoundation.CFRelease(guestCode);        // (4)
            if (cfAttrs          != IntPtr.Zero) CoreFoundation.CFRelease(cfAttrs);          // (3)
            if (cfValue          != IntPtr.Zero) CoreFoundation.CFRelease(cfValue);          // (2)
            if (cfKey            != IntPtr.Zero) CoreFoundation.CFRelease(cfKey);            // (1)
        }
    }
}
