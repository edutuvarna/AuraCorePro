using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.UI.Xaml;

namespace AuraCore.Desktop.Services;

/// <summary>
/// Background service that checks for app updates periodically.
/// Supports in-app download with progress, auto-launch installer, retry logic.
/// </summary>
public sealed class UpdateChecker
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };
    private readonly DispatcherTimer _timer;
    private static UpdateChecker? _instance;

    public const string CurrentVersion = "1.6.0";
    private const int CheckIntervalMinutes = 5;
    private const int MaxRetries = 3;

    public static UpdateChecker Instance => _instance ??= new UpdateChecker();

    // State
    public bool UpdateAvailable { get; private set; }
    public bool IsMandatory { get; private set; }
    public string LatestVersion { get; private set; } = "";
    public string DownloadUrl { get; private set; } = "";
    public string DeltaUrl { get; private set; } = "";
    public long? DeltaSize { get; private set; }
    public string ReleaseNotes { get; private set; } = "";
    public bool IsChecking { get; private set; }
    public bool IsDownloading { get; private set; }
    public double DownloadProgress { get; private set; }
    public string DownloadedFilePath { get; private set; } = "";
    public DateTimeOffset? LastCheckTime { get; private set; }
    public string? LastError { get; private set; }

    // Events
    public event Action<UpdateInfo>? UpdateFound;
    public event Action<double>? DownloadProgressChanged;
    public event Action<string>? DownloadCompleted;
    public event Action<string>? DownloadFailed;
    public event Action? CheckCompleted;

    private int _retryCount = 0;
    private string? _skippedVersion;

    public UpdateChecker()
    {
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(CheckIntervalMinutes) };
        _timer.Tick += async (s, e) => await CheckForUpdateAsync();

        // Load skipped version
        try
        {
            var skipFile = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AuraCorePro", "skip_version.txt");
            if (File.Exists(skipFile))
                _skippedVersion = File.ReadAllText(skipFile).Trim();
        }
        catch { }
    }

    public void Start()
    {
        _ = CheckForUpdateAsync();
        _timer.Start();
    }

    public void Stop() => _timer.Stop();

    /// <summary>Manual check - resets skip and forces a new check</summary>
    public async Task ManualCheckAsync()
    {
        _skippedVersion = null;
        _retryCount = 0;
        LastError = null;
        await CheckForUpdateAsync(forceNotify: true);
    }

    /// <summary>Skip this version for optional updates</summary>
    public void SkipVersion(string version)
    {
        _skippedVersion = version;
        UpdateAvailable = false;
        try
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AuraCorePro");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "skip_version.txt"), version);
        }
        catch { }
    }

    public async Task CheckForUpdateAsync(bool forceNotify = false)
    {
        if (IsChecking) return;
        IsChecking = true;
        LastError = null;

        try
        {
            var apiUrl = LoginWindow.ApiBaseUrl;
            var url = $"{apiUrl}/api/updates/check?currentVersion={CurrentVersion}&channel=stable";

            var request = new HttpRequestMessage(HttpMethod.Get, url);
            if (!string.IsNullOrEmpty(LoginWindow.AccessToken))
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", LoginWindow.AccessToken);

            var response = await Http.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                LastError = $"Server returned {(int)response.StatusCode}";
                HandleRetry();
                return;
            }

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var available = root.TryGetProperty("updateAvailable", out var avProp) && avProp.GetBoolean();
            LastCheckTime = DateTimeOffset.Now;
            _retryCount = 0;

            if (!available)
            {
                UpdateAvailable = false;
                CheckCompleted?.Invoke();
                return;
            }

            var version = root.TryGetProperty("version", out var vProp) ? vProp.GetString() ?? "" : "";
            var downloadUrl = root.TryGetProperty("downloadUrl", out var dProp) ? dProp.GetString() ?? "" : "";
            var releaseNotes = root.TryGetProperty("releaseNotes", out var rProp) ? rProp.GetString() ?? "" : "";
            var mandatory = root.TryGetProperty("isMandatory", out var mProp) && mProp.GetBoolean();
            var deltaUrl = root.TryGetProperty("deltaUrl", out var duProp) && duProp.ValueKind == JsonValueKind.String ? duProp.GetString() ?? "" : "";
            var deltaSize = root.TryGetProperty("deltaSize", out var dsProp) && dsProp.ValueKind == JsonValueKind.Number ? dsProp.GetInt64() : (long?)null;

            if (!mandatory && version == _skippedVersion && !forceNotify)
            {
                CheckCompleted?.Invoke();
                return;
            }

            if (version == LatestVersion && UpdateAvailable && !forceNotify)
            {
                CheckCompleted?.Invoke();
                return;
            }

            UpdateAvailable = true;
            IsMandatory = mandatory;
            LatestVersion = version;
            DownloadUrl = downloadUrl;
            DeltaUrl = deltaUrl;
            DeltaSize = deltaSize;
            ReleaseNotes = releaseNotes;

            UpdateFound?.Invoke(new UpdateInfo
            {
                Version = version,
                DownloadUrl = downloadUrl,
                DeltaUrl = deltaUrl,
                DeltaSize = deltaSize,
                ReleaseNotes = releaseNotes,
                IsMandatory = mandatory
            });
        }
        catch (TaskCanceledException)
        {
            LastError = "Connection timed out";
            HandleRetry();
        }
        catch (HttpRequestException ex)
        {
            LastError = $"Connection error: {ex.Message}";
            HandleRetry();
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
        }
        finally
        {
            IsChecking = false;
            CheckCompleted?.Invoke();
        }
    }

    /// <summary>Download the update installer to temp folder with progress</summary>
    public async Task<string?> DownloadUpdateAsync()
    {
        if (IsDownloading || string.IsNullOrEmpty(DownloadUrl)) return null;
        IsDownloading = true;
        DownloadProgress = 0;

        try
        {
            var response = await Http.GetAsync(DownloadUrl, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? -1;
            var fileName = $"AuraCorePro-v{LatestVersion}-Setup.exe";
            var tempDir = Path.Combine(Path.GetTempPath(), "AuraCorePro-Updates");
            Directory.CreateDirectory(tempDir);
            var filePath = Path.Combine(tempDir, fileName);

            if (File.Exists(filePath)) File.Delete(filePath);

            using var contentStream = await response.Content.ReadAsStreamAsync();
            using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

            var buffer = new byte[8192];
            long totalRead = 0;
            int bytesRead;

            while ((bytesRead = await contentStream.ReadAsync(buffer)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead));
                totalRead += bytesRead;

                if (totalBytes > 0)
                {
                    DownloadProgress = (double)totalRead / totalBytes * 100;
                    DownloadProgressChanged?.Invoke(DownloadProgress);
                }
            }

            DownloadProgress = 100;
            DownloadProgressChanged?.Invoke(100);
            DownloadedFilePath = filePath;
            DownloadCompleted?.Invoke(filePath);
            return filePath;
        }
        catch (Exception ex)
        {
            DownloadFailed?.Invoke(ex.Message);
            return null;
        }
        finally
        {
            IsDownloading = false;
        }
    }

    /// <summary>
    /// Launch the downloaded installer in SILENT mode for seamless auto-update.
    /// The installer will: close the running app (CloseApplications=yes),
    /// clean old files, install new files, then relaunch.
    /// </summary>
    public bool LaunchInstaller(bool silent = true)
    {
        if (string.IsNullOrEmpty(DownloadedFilePath) || !File.Exists(DownloadedFilePath))
            return false;

        try
        {
            var args = silent ? "/SILENT /CLOSEAPPLICATIONS /RESTARTAPPLICATIONS" : "";
            var proc = Process.Start(new ProcessStartInfo
            {
                FileName = DownloadedFilePath,
                Arguments = args,
                UseShellExecute = true
            });
            return proc != null;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Download and apply a delta update (small zip with only changed files).
    /// Returns true if successful, false to fall back to full installer.
    /// </summary>
    public async Task<bool> DownloadAndApplyDeltaAsync()
    {
        if (string.IsNullOrEmpty(DeltaUrl)) return false;
        IsDownloading = true;
        DownloadProgress = 0;

        try
        {
            // Download delta zip
            var response = await Http.GetAsync(DeltaUrl, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? DeltaSize ?? -1;
            var tempDir = Path.Combine(Path.GetTempPath(), "AuraCorePro-Updates");
            Directory.CreateDirectory(tempDir);
            var zipPath = Path.Combine(tempDir, $"delta-v{LatestVersion}.zip");

            using (var contentStream = await response.Content.ReadAsStreamAsync())
            using (var fileStream = new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
            {
                var buffer = new byte[8192];
                long totalRead = 0;
                int bytesRead;
                while ((bytesRead = await contentStream.ReadAsync(buffer)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead));
                    totalRead += bytesRead;
                    if (totalBytes > 0)
                    {
                        DownloadProgress = (double)totalRead / totalBytes * 100;
                        DownloadProgressChanged?.Invoke(DownloadProgress);
                    }
                }
            }

            DownloadProgress = 80;
            DownloadProgressChanged?.Invoke(80);

            // Determine app directory
            var appDir = AppContext.BaseDirectory;
            var extractDir = Path.Combine(tempDir, $"delta-v{LatestVersion}");
            if (Directory.Exists(extractDir)) Directory.Delete(extractDir, true);

            // Extract zip
            ZipFile.ExtractToDirectory(zipPath, extractDir, overwriteFiles: true);

            // Create a batch script that:
            // 1. Waits for app to close
            // 2. Copies new files over the app directory
            // 3. Relaunches the app
            // 4. Cleans up
            var appExe = Path.Combine(appDir, "AuraCore.Desktop.exe");
            var batchPath = Path.Combine(tempDir, "apply-delta.bat");
            var batchContent =
                "@echo off\r\n" +
                "echo Applying AuraCore Pro update...\r\n" +
                "timeout /t 3 /nobreak >nul\r\n" +
                $"xcopy /s /y /q \"{extractDir}\\*\" \"{appDir}\"\r\n" +
                $"start \"\" \"{appExe}\"\r\n" +
                $"rd /s /q \"{extractDir}\" 2>nul\r\n" +
                $"del \"{zipPath}\" 2>nul\r\n" +
                "del \"%~f0\" 2>nul\r\n";
            await File.WriteAllTextAsync(batchPath, batchContent);

            DownloadProgress = 100;
            DownloadProgressChanged?.Invoke(100);
            DownloadCompleted?.Invoke(batchPath);

            // Launch batch and exit
            Process.Start(new ProcessStartInfo
            {
                FileName = batchPath,
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Hidden
            });

            // Exit the app so files can be overwritten
            Environment.Exit(0);
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Delta] Failed: {ex.Message}, falling back to full installer");
            DownloadFailed?.Invoke($"Delta failed: {ex.Message}. Falling back to full download.");
            return false;
        }
        finally
        {
            IsDownloading = false;
        }
    }

    /// <summary>True if a delta update is available (smaller, faster download)</summary>
    public bool HasDelta => !string.IsNullOrEmpty(DeltaUrl);

    private void HandleRetry()
    {
        _retryCount++;
        if (_retryCount <= MaxRetries)
        {
            var delay = TimeSpan.FromSeconds(30 * Math.Pow(2, _retryCount - 1));
            _ = Task.Delay(delay).ContinueWith(_ => CheckForUpdateAsync());
        }
    }
}

public sealed class UpdateInfo
{
    public string Version { get; init; } = "";
    public string DownloadUrl { get; init; } = "";
    public string DeltaUrl { get; init; } = "";
    public long? DeltaSize { get; init; }
    public string ReleaseNotes { get; init; } = "";
    public bool IsMandatory { get; init; }
    public bool HasDelta => !string.IsNullOrEmpty(DeltaUrl);
}
