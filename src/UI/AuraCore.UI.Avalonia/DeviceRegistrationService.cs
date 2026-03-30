using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Json;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AuraCore.Application;

namespace AuraCore.UI.Avalonia;

/// <summary>
/// Registers this device with the API on every login (fire-and-forget).
/// Cross-platform: uses WMI on Windows, /etc/machine-id on Linux, ioreg on macOS.
/// </summary>
public static class DeviceRegistrationService
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };

    public static async void RegisterAsync()
    {
        try
        {
            await Task.Run(async () =>
            {
                if (string.IsNullOrEmpty(SessionState.AccessToken) || string.IsNullOrEmpty(SessionState.ApiBaseUrl))
                    return;

                var fingerprint = GetHardwareFingerprint();
                var machineName = Environment.MachineName;
                var osVersion = RuntimeInformation.OSDescription;

                var request = new HttpRequestMessage(HttpMethod.Post, $"{SessionState.ApiBaseUrl}/api/device/register-auto");
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", SessionState.AccessToken);
                request.Content = JsonContent.Create(new
                {
                    hardwareFingerprint = fingerprint,
                    machineName,
                    osVersion
                });

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
                var response = await Http.SendAsync(request, cts.Token);
                Debug.WriteLine($"[DeviceReg] {(int)response.StatusCode}");

                if (response.IsSuccessStatusCode)
                {
                    try
                    {
                        var json = await response.Content.ReadAsStringAsync();
                        using var doc = JsonDocument.Parse(json);
                        if (doc.RootElement.TryGetProperty("deviceId", out var did) && Guid.TryParse(did.GetString(), out var deviceGuid))
                            SessionState.DeviceId = deviceGuid;
                    }
                    catch { }
                }
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[DeviceReg] Error: {ex.Message}");
        }
    }

    private static string GetHardwareFingerprint()
    {
        var sb = new StringBuilder();
        sb.Append(Environment.MachineName);

        if (OperatingSystem.IsWindows())
        {
            sb.Append(GetWindowsFingerprint());
        }
        else if (OperatingSystem.IsLinux())
        {
            sb.Append(GetLinuxFingerprint());
        }
        else if (OperatingSystem.IsMacOS())
        {
            sb.Append(GetMacFingerprint());
        }
        else
        {
            sb.Append("unknown-platform");
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string GetWindowsFingerprint()
    {
        var sb = new StringBuilder();
        try
        {
            // WMI for ProcessorId + BaseBoard serial
            if (OperatingSystem.IsWindows())
            {
                using var cpuSearcher = new System.Management.ManagementObjectSearcher("SELECT ProcessorId FROM Win32_Processor");
                foreach (var obj in cpuSearcher.Get()) { sb.Append(obj["ProcessorId"]?.ToString() ?? ""); break; }

                using var mbSearcher = new System.Management.ManagementObjectSearcher("SELECT SerialNumber FROM Win32_BaseBoard");
                foreach (var obj in mbSearcher.Get()) { sb.Append(obj["SerialNumber"]?.ToString() ?? ""); break; }
            }
        }
        catch { sb.Append("unknown-hw"); }
        return sb.ToString();
    }

    private static string GetLinuxFingerprint()
    {
        try
        {
            // /etc/machine-id is a stable unique identifier on most Linux distros
            if (File.Exists("/etc/machine-id"))
                return File.ReadAllText("/etc/machine-id").Trim();
        }
        catch { }
        return "unknown-linux";
    }

    private static string GetMacFingerprint()
    {
        try
        {
            // Use ioreg to get hardware UUID
            var psi = new ProcessStartInfo("ioreg", "-rd1 -c IOPlatformExpertDevice")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            if (proc != null)
            {
                var output = proc.StandardOutput.ReadToEnd();
                proc.WaitForExit(3000);
                // Extract IOPlatformUUID
                foreach (var line in output.Split('\n'))
                {
                    if (line.Contains("IOPlatformUUID"))
                    {
                        var parts = line.Split('=');
                        if (parts.Length > 1) return parts[1].Trim().Trim('"');
                    }
                }
            }
        }
        catch { }
        return "unknown-mac";
    }
}
