using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.UI.Xaml;

namespace AuraCore.Desktop.Services;

/// <summary>
/// Background service that checks for app updates every 5 minutes.
/// Shows notification for optional updates, blocks app for mandatory updates.
/// </summary>
public sealed class UpdateChecker
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };
    private readonly DispatcherTimer _timer;
    private static UpdateChecker? _instance;

    public const string CurrentVersion = "1.2.0";

    public static UpdateChecker Instance => _instance ??= new UpdateChecker();

    // Update info
    public bool UpdateAvailable { get; private set; }
    public bool IsMandatory { get; private set; }
    public string LatestVersion { get; private set; } = "";
    public string DownloadUrl { get; private set; } = "";
    public string ReleaseNotes { get; private set; } = "";

    public event Action<UpdateInfo>? UpdateFound;

    public UpdateChecker()
    {
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(5) };
        _timer.Tick += async (s, e) => await CheckForUpdateAsync();
    }

    public void Start()
    {
        // Check immediately on start, then every 5 minutes
        _ = CheckForUpdateAsync();
        _timer.Start();
    }

    public void Stop() => _timer.Stop();

    public async Task CheckForUpdateAsync()
    {
        try
        {
            var apiUrl = LoginWindow.ApiBaseUrl;
            var url = $"{apiUrl}/api/updates/check?currentVersion={CurrentVersion}&channel=stable";

            var request = new HttpRequestMessage(HttpMethod.Get, url);
            if (!string.IsNullOrEmpty(LoginWindow.AccessToken))
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", LoginWindow.AccessToken);

            var response = await Http.SendAsync(request);
            if (!response.IsSuccessStatusCode) return;

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var available = root.TryGetProperty("updateAvailable", out var avProp) && avProp.GetBoolean();
            if (!available) return;

            var version = root.TryGetProperty("version", out var vProp) ? vProp.GetString() ?? "" : "";
            var downloadUrl = root.TryGetProperty("downloadUrl", out var dProp) ? dProp.GetString() ?? "" : "";
            var releaseNotes = root.TryGetProperty("releaseNotes", out var rProp) ? rProp.GetString() ?? "" : "";
            var mandatory = root.TryGetProperty("isMandatory", out var mProp) && mProp.GetBoolean();

            // Don't re-notify for same version
            if (version == LatestVersion && UpdateAvailable) return;

            UpdateAvailable = true;
            IsMandatory = mandatory;
            LatestVersion = version;
            DownloadUrl = downloadUrl;
            ReleaseNotes = releaseNotes;

            UpdateFound?.Invoke(new UpdateInfo
            {
                Version = version,
                DownloadUrl = downloadUrl,
                ReleaseNotes = releaseNotes,
                IsMandatory = mandatory
            });
        }
        catch { /* Silently fail — will retry in 5 min */ }
    }
}

public sealed class UpdateInfo
{
    public string Version { get; init; } = "";
    public string DownloadUrl { get; init; } = "";
    public string ReleaseNotes { get; init; } = "";
    public bool IsMandatory { get; init; }
}
