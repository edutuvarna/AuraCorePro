using Microsoft.Extensions.Logging;

namespace AuraCore.Infrastructure.PrivilegeIpc.MacOS;

/// <summary>
/// The plist name registered in the main app's LaunchDaemons bundle directory.
/// The daemon's launchd plist must be at:
///   AuraCorePro.app/Contents/Library/LaunchDaemons/pro.auracore.privhelper.plist
/// </summary>
file static class PrivHelperPlist
{
    internal const string Name = "pro.auracore.privhelper.plist";
}

// ── Outcome types ────────────────────────────────────────────────────────────

/// <summary>
/// High-level result of attempting to register the macOS privilege helper
/// via SMAppService. The UI uses this to decide what to show the user.
/// </summary>
public enum RegistrationOutcome
{
    /// <summary>Helper is now live; XPC calls can proceed.</summary>
    Registered,

    /// <summary>
    /// Registration call succeeded but the user must approve the daemon in
    /// System Settings → Privacy &amp; Security → Login Items. UI should surface
    /// an actionable prompt directing the user there.
    /// </summary>
    RequiresUserApproval,

    /// <summary>
    /// Unsigned dev build OR SMAppService is not available (macOS &lt; 13 or
    /// non-macOS host). The caller should fall back to
    /// <c>InProcessShellCommandService</c> — helper registration is skipped.
    /// </summary>
    DevModeFallback,

    /// <summary>Something went wrong; see <see cref="RegistrationResult.ErrorMessage"/>.</summary>
    Failed,
}

/// <summary>
/// Carries the full registration result, including the native SMAppService status
/// and an optional diagnostic message for logging or UI display.
/// </summary>
public sealed record RegistrationResult(
    RegistrationOutcome Outcome,
    SMAppServiceStatus Status,
    string? ErrorMessage);

// ── Registrar ────────────────────────────────────────────────────────────────

/// <summary>
/// Orchestrator that the UI's <c>PrivilegeInstallCoordinator</c> can invoke to
/// register the AuraCore privilege helper with macOS's launchd via SMAppService
/// (macOS 13+).
///
/// <para>
/// On macOS 13+, calling <see cref="RegisterHelper"/> triggers an OS-level
/// prompt ("Allow AuraCorePro helper?") the first time. Subsequent calls
/// are idempotent — if already <see cref="SMAppServiceStatus.Enabled"/> the OS
/// simply returns Enabled without re-prompting.
/// </para>
///
/// <para>
/// On unsigned dev builds, or when SMAppService is not available, the registrar
/// returns <see cref="RegistrationOutcome.DevModeFallback"/> and the caller should
/// continue using <c>InProcessShellCommandService</c>.
/// </para>
///
/// <para>
/// This class is the macOS parallel to the Linux <c>PrivHelperInstaller</c> —
/// both follow the factory + bridge + interfaces testability pattern.
/// </para>
/// </summary>
public sealed class SMAppServiceRegistrar
{
    private readonly ISMAppServiceBridge _bridge;
    private readonly IBundleSignatureDetector _signatureDetector;
    private readonly ILogger<SMAppServiceRegistrar> _logger;

    public SMAppServiceRegistrar(
        ISMAppServiceBridge bridge,
        IBundleSignatureDetector signatureDetector,
        ILogger<SMAppServiceRegistrar> logger)
    {
        _bridge            = bridge;
        _signatureDetector = signatureDetector;
        _logger            = logger;
    }

    /// <summary>
    /// Registers <c>pro.auracore.privhelper.plist</c> with SMAppService.
    /// Falls back to <see cref="RegistrationOutcome.DevModeFallback"/> on
    /// unsigned builds or unsupported macOS versions.
    ///
    /// This method never throws — all exceptions from native calls are caught
    /// and returned as <see cref="RegistrationOutcome.Failed"/> with a diagnostic message.
    /// </summary>
    public RegistrationResult RegisterHelper()
    {
        // Guard: must be running on macOS at all.
        if (!OperatingSystem.IsMacOS())
        {
            _logger.LogDebug("[smapp-registrar] non-macOS dev host — DevModeFallback");
            return new RegistrationResult(
                RegistrationOutcome.DevModeFallback,
                SMAppServiceStatus.NotSupported,
                "non-macOS dev host");
        }

        // Guard: unsigned / ad-hoc build cannot register with SMAppService.
        bool isSigned;
        try
        {
            isSigned = _signatureDetector.IsBundleProperlySignedWithTeamId();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[smapp-registrar] signature check threw — treating as unsigned");
            isSigned = false;
        }

        if (!isSigned)
        {
            _logger.LogWarning(
                "[smapp-registrar] unsigned or ad-hoc build detected; " +
                "SMAppService registration skipped — using InProcess fallback");
            return new RegistrationResult(
                RegistrationOutcome.DevModeFallback,
                SMAppServiceStatus.NotSupported,
                "unsigned build — SMAppService registration not available");
        }

        // Attempt registration via the bridge.
        SMAppServiceStatus status;
        try
        {
            status = _bridge.RegisterDaemon(PrivHelperPlist.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[smapp-registrar] bridge.RegisterDaemon threw unexpectedly");
            return new RegistrationResult(
                RegistrationOutcome.Failed,
                SMAppServiceStatus.NotSupported,
                $"RegisterDaemon threw: {ex.Message}");
        }

        return MapStatusToResult(status);
    }

    /// <summary>
    /// Polls the current SMAppService status without re-registering.
    /// Useful for UI polling loops after a <see cref="RegistrationOutcome.RequiresUserApproval"/>
    /// result — callers can check whether the user has approved in System Settings.
    /// </summary>
    public SMAppServiceStatus GetCurrentStatus()
    {
        try
        {
            return _bridge.GetStatus(PrivHelperPlist.Name);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[smapp-registrar] GetCurrentStatus threw");
            return SMAppServiceStatus.NotSupported;
        }
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    private RegistrationResult MapStatusToResult(SMAppServiceStatus status)
    {
        switch (status)
        {
            case SMAppServiceStatus.Enabled:
                _logger.LogInformation("[smapp-registrar] helper registered and enabled");
                return new RegistrationResult(RegistrationOutcome.Registered, status, null);

            case SMAppServiceStatus.RequiresApproval:
                _logger.LogInformation(
                    "[smapp-registrar] helper registered; user approval required in System Settings");
                return new RegistrationResult(RegistrationOutcome.RequiresUserApproval, status, null);

            case SMAppServiceStatus.NotSupported:
                _logger.LogWarning("[smapp-registrar] SMAppService not supported — DevModeFallback");
                return new RegistrationResult(
                    RegistrationOutcome.DevModeFallback,
                    status,
                    "SMAppService not available on this macOS version");

            case SMAppServiceStatus.NotRegistered:
                // Registration call completed but status is still NotRegistered — unexpected.
                _logger.LogWarning("[smapp-registrar] status is NotRegistered after register call");
                return new RegistrationResult(
                    RegistrationOutcome.Failed,
                    status,
                    "Registration call succeeded but status is still NotRegistered");

            case SMAppServiceStatus.NotFound:
            default:
                _logger.LogError(
                    "[smapp-registrar] registration failed with status={Status}", status);
                return new RegistrationResult(
                    RegistrationOutcome.Failed,
                    status,
                    $"SMAppService registration returned status: {status}");
        }
    }
}
