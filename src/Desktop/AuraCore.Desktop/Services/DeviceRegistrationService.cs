using System;
using System.Management;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using AuraCore.Application;

namespace AuraCore.Desktop.Services;

/// <summary>Registers this device with the API on every login (fire-and-forget).</summary>
public static class DeviceRegistrationService
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };

    /// <summary>Register device in background - never throws, never blocks UI.</summary>
    public static async void RegisterAsync()
    {
        try
        {
            // Run entirely on background thread to avoid blocking UI with WMI calls
            await Task.Run(async () =>
            {
                if (string.IsNullOrEmpty(SessionState.AccessToken) || string.IsNullOrEmpty(SessionState.ApiBaseUrl))
                    return;

                var fingerprint = GetHardwareFingerprint();
                var machineName = Environment.MachineName;
                var osVersion = Environment.OSVersion.VersionString;

                var request = new HttpRequestMessage(HttpMethod.Post, $"{SessionState.ApiBaseUrl}/api/device/register-auto");
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", SessionState.AccessToken);
                request.Content = JsonContent.Create(new
                {
                    hardwareFingerprint = fingerprint,
                    machineName = machineName,
                    osVersion = osVersion
                });

                using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(8));
                var response = await Http.SendAsync(request, cts.Token);
                System.Diagnostics.Debug.WriteLine($"[DeviceReg] {(int)response.StatusCode}");

                // Capture deviceId from response for crash reporting
                if (response.IsSuccessStatusCode)
                {
                    try
                    {
                        var json = await response.Content.ReadAsStringAsync();
                        using var doc = System.Text.Json.JsonDocument.Parse(json);
                        if (doc.RootElement.TryGetProperty("deviceId", out var did) && Guid.TryParse(did.GetString(), out var deviceGuid))
                            SessionState.DeviceId = deviceGuid;
                    }
                    catch { }
                }
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DeviceReg] Error: {ex.Message}");
        }
    }

    private static string GetHardwareFingerprint()
    {
        var sb = new StringBuilder();
        sb.Append(Environment.MachineName);

        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT ProcessorId FROM Win32_Processor");
            foreach (var obj in searcher.Get())
            {
                sb.Append(obj["ProcessorId"]?.ToString() ?? "");
                break;
            }
        }
        catch { sb.Append("unknown-cpu"); }

        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT SerialNumber FROM Win32_BaseBoard");
            foreach (var obj in searcher.Get())
            {
                sb.Append(obj["SerialNumber"]?.ToString() ?? "");
                break;
            }
        }
        catch { sb.Append("unknown-mb"); }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
