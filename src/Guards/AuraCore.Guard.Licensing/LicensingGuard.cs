using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using AuraCore.Application;
using AuraCore.Application.Interfaces.Guards;

namespace AuraCore.Guard.Licensing;

public sealed class LicensingGuard : IOperationGuard
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };
    private static DateTimeOffset _lastValidation = DateTimeOffset.MinValue;
    private static string _cachedTier = "free";
    private static string? _cachedSignature;
    private static readonly TimeSpan ValidationInterval = TimeSpan.FromMinutes(30);

    public async Task<GuardResult> CheckAsync(string moduleId, string operationId, CancellationToken ct = default)
    {
        var tier = await ValidateTierAsync(ct);
        var allowed = IsTierSufficient(moduleId, tier);
        return new GuardResult(allowed, allowed ? null : "This feature requires a Pro subscription. Upgrade to unlock.");
    }

    public static async Task<string> ValidateTierAsync(CancellationToken ct = default)
    {
        if (DateTimeOffset.UtcNow - _lastValidation < ValidationInterval && VerifyIntegrity())
            return _cachedTier;

        try
        {
            var token = SessionState.AccessToken;
            var baseUrl = SessionState.ApiBaseUrl;
            if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(baseUrl))
                return CacheTier("free");

            Http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var resp = await Http.GetAsync($"{baseUrl}/api/auth/me", ct);
            if (resp.IsSuccessStatusCode)
            {
                var json = await resp.Content.ReadAsStringAsync(ct);
                var doc = JsonDocument.Parse(json);
                var role = doc.RootElement.TryGetProperty("role", out var r) ? r.GetString() : "user";
                if (role == "admin") return CacheTier("admin");
            }

            var licResp = await Http.GetAsync($"{baseUrl}/api/license/validate?key=self&device=self", ct);
            if (licResp.IsSuccessStatusCode)
            {
                var json = await licResp.Content.ReadAsStringAsync(ct);
                var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("tier", out var tier))
                    return CacheTier(tier.GetString() ?? "free");
            }
        }
        catch { }

        return CacheTier(_cachedTier);
    }

    private static string CacheTier(string tier)
    {
        _cachedTier = tier;
        _lastValidation = DateTimeOffset.UtcNow;
        _cachedSignature = ComputeSignature(tier);
        SessionState.UserTier = tier;
        return tier;
    }

    private static string ComputeSignature(string tier)
    {
        var key = DeriveKey();
        var data = Encoding.UTF8.GetBytes($"{tier}:{_lastValidation.Ticks}:{Environment.ProcessId}");
        using var hmac = new HMACSHA256(key);
        return Convert.ToBase64String(hmac.ComputeHash(data));
    }

    private static bool VerifyIntegrity()
    {
        if (_cachedSignature is null) return false;
        var expected = ComputeSignature(_cachedTier);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(_cachedSignature));
    }

    private static byte[] DeriveKey()
    {
        var id = $"{Environment.MachineName}:{Environment.UserName}:{Environment.ProcessorCount}";
        return SHA256.HashData(Encoding.UTF8.GetBytes(id));
    }

    private static bool IsTierSufficient(string moduleId, string tier)
    {
        if (tier == "admin") return true;
        var tierLevel = tier switch { "enterprise" => 2, "pro" => 1, _ => 0 };
        var required = moduleId switch
        {
            "storage-compression" or "registry-optimizer" or "bloatware-removal" or "context-menu" => 1,
            _ => 0
        };
        return tierLevel >= required;
    }
}

public static class LicensingRegistration
{
    public static IServiceCollection AddLicensingGuard(this IServiceCollection services)
    {
        services.AddSingleton<IOperationGuard, LicensingGuard>();
        return services;
    }
}
