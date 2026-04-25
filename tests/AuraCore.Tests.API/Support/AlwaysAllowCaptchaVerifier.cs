#if PHASE_6_12_CAPTCHA_READY
using AuraCore.API.Application.Services.Security;

namespace AuraCore.Tests.API.Support;

/// <summary>
/// Test substitute for ICaptchaVerifier — always returns true so existing
/// auth-flow integration tests don't need to construct real Turnstile tokens.
/// Phase 6.12 introduced ICaptchaVerifier as a hard requirement on four auth
/// endpoints; this stub keeps pre-6.12 fixtures green.
/// </summary>
public sealed class AlwaysAllowCaptchaVerifier : ICaptchaVerifier
{
    public Task<bool> VerifyAsync(string token, string remoteIp, CancellationToken ct = default)
        => Task.FromResult(true);
}
#endif
