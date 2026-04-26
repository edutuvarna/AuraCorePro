using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AuraCore.API.Application.Services.Push;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AuraCore.API.Infrastructure.Services.Push;

public sealed class FcmService : IFcmService
{
    private readonly HttpClient _http;
    private readonly ILogger<FcmService> _log;
    private readonly string? _serviceAccountJson;
    private readonly string? _projectId;
    private (string token, DateTimeOffset expiresAt)? _cachedAccessToken;

    public FcmService(HttpClient http, IConfiguration cfg, ILogger<FcmService> log)
    {
        _http = http;
        _log = log;
        _serviceAccountJson = cfg["FCM_SERVICE_ACCOUNT_JSON"];
        _projectId = cfg["FCM_PROJECT_ID"];
    }

    public async Task SendAsync(string deviceToken, FcmPayload payload, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(_serviceAccountJson) || string.IsNullOrEmpty(_projectId))
        {
            _log.LogWarning("FCM not configured (missing FCM_SERVICE_ACCOUNT_JSON or FCM_PROJECT_ID); skipping push to {Token}", deviceToken[..Math.Min(12, deviceToken.Length)]);
            return;
        }

        var accessToken = await GetAccessTokenAsync(ct);
        var url = $"https://fcm.googleapis.com/v1/projects/{_projectId}/messages:send";
        var msg = new
        {
            message = new
            {
                token = deviceToken,
                notification = new { title = payload.Title, body = payload.Body },
                data = payload.Data ?? new Dictionary<string, string>(),
                android = new { priority = "HIGH" },
            }
        };
        using var req = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(msg) };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var resp = await _http.SendAsync(req, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync(ct);
            _log.LogWarning("FCM send failed: {Status} {Body}", resp.StatusCode, body);
        }
    }

    private async Task<string> GetAccessTokenAsync(CancellationToken ct)
    {
        if (_cachedAccessToken.HasValue && _cachedAccessToken.Value.expiresAt > DateTimeOffset.UtcNow.AddMinutes(2))
            return _cachedAccessToken.Value.token;

        var sa = JsonSerializer.Deserialize<ServiceAccount>(_serviceAccountJson!)
            ?? throw new InvalidOperationException("FCM_SERVICE_ACCOUNT_JSON could not be parsed");

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var jwtHeader = JsonSerializer.Serialize(new { alg = "RS256", typ = "JWT" });
        var jwtClaims = JsonSerializer.Serialize(new
        {
            iss = sa.client_email,
            scope = "https://www.googleapis.com/auth/firebase.messaging",
            aud = "https://oauth2.googleapis.com/token",
            iat = now,
            exp = now + 3600,
        });
        var unsigned = $"{Base64UrlEncode(Encoding.UTF8.GetBytes(jwtHeader))}.{Base64UrlEncode(Encoding.UTF8.GetBytes(jwtClaims))}";
        var rsa = RSA.Create();
        rsa.ImportFromPem(sa.private_key);
        var signature = rsa.SignData(Encoding.UTF8.GetBytes(unsigned), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var signedJwt = $"{unsigned}.{Base64UrlEncode(signature)}";

        var tokenResp = await _http.PostAsync("https://oauth2.googleapis.com/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "urn:ietf:params:oauth:grant-type:jwt-bearer",
            ["assertion"] = signedJwt,
        }), ct);
        tokenResp.EnsureSuccessStatusCode();
        var tokenBody = await tokenResp.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken: ct)
            ?? throw new InvalidOperationException("FCM token response empty");
        _cachedAccessToken = (tokenBody.access_token, DateTimeOffset.UtcNow.AddSeconds(tokenBody.expires_in));
        return tokenBody.access_token;
    }

    private static string Base64UrlEncode(byte[] data) =>
        Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private sealed record ServiceAccount(string client_email, string private_key);
    private sealed record TokenResponse(string access_token, int expires_in);
}
