using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Json;
using System.Runtime.InteropServices;
using System.Text.Json;
using AuraCore.Application;

namespace AuraCore.UI.Avalonia;

/// <summary>
/// Catches unhandled exceptions and sends crash reports to the API.
/// Falls back to local file if API is unreachable.
/// Port from WinUI3 — uses Avalonia-compatible exception hooks.
/// </summary>
public static class CrashReportService
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };
    private static readonly string LocalCrashDir = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AuraCorePro", "CrashReports");
    private static bool s_initialized;

    /// <summary>Call once from App startup to hook all exception handlers.</summary>
    public static void Initialize()
    {
        if (s_initialized) return;
        s_initialized = true;

        // Unobserved Task exceptions (async void, forgotten awaits)
        TaskScheduler.UnobservedTaskException += (sender, e) =>
        {
            e.SetObserved();
            _ = SendReportAsync(e.Exception?.InnerException ?? e.Exception, "UnobservedTaskException");
        };

        // AppDomain unhandled (last resort)
        AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
        {
            if (e.ExceptionObject is Exception ex)
                SendReportSync(ex, "AppDomainUnhandled");
        };

        // Flush pending reports from previous crashes
        _ = Task.Run(FlushPendingReportsAsync);
    }

    private static async Task SendReportAsync(Exception? ex, string source)
    {
        if (ex is null) return;
        try
        {
            var report = BuildReport(ex, source);
            var sent = await TrySendToApiAsync(report);
            if (!sent) SaveLocally(report);
        }
        catch { }
    }

    private static void SendReportSync(Exception ex, string source)
    {
        try
        {
            var report = BuildReport(ex, source);
            try
            {
                var task = TrySendToApiAsync(report);
                task.Wait(TimeSpan.FromSeconds(3));
                if (!task.IsCompletedSuccessfully || !task.Result)
                    SaveLocally(report);
            }
            catch { SaveLocally(report); }
        }
        catch { }
    }

    private static CrashPayload BuildReport(Exception ex, string source)
    {
        var innerMsg = ex.InnerException?.Message;
        var exType = ex.GetType().FullName ?? ex.GetType().Name;
        if (innerMsg != null) exType += $" -> {ex.InnerException?.GetType().Name}";

        var stackTrace = ex.ToString();
        if (stackTrace.Length > 8000) stackTrace = stackTrace[..8000] + "\n... (truncated)";

        var sysInfo = new
        {
            os = RuntimeInformation.OSDescription,
            arch = RuntimeInformation.OSArchitecture.ToString(),
            machine = Environment.MachineName,
            dotnet = RuntimeInformation.FrameworkDescription,
            processors = Environment.ProcessorCount,
            memoryMB = (int)(Process.GetCurrentProcess().WorkingSet64 / (1024 * 1024)),
            uiFramework = "Avalonia",
            source,
            user = SessionState.UserEmail ?? "unknown",
            tier = SessionState.UserTier ?? "free"
        };

        return new CrashPayload
        {
            DeviceId = SessionState.DeviceId ?? Guid.Empty,
            AppVersion = UpdateChecker.CurrentVersion,
            ExceptionType = exType,
            StackTrace = stackTrace,
            SystemInfo = JsonSerializer.Serialize(sysInfo)
        };
    }

    private static async Task<bool> TrySendToApiAsync(CrashPayload report)
    {
        if (string.IsNullOrEmpty(SessionState.AccessToken) || string.IsNullOrEmpty(SessionState.ApiBaseUrl))
            return false;

        try
        {
            var request = new HttpRequestMessage(HttpMethod.Post, $"{SessionState.ApiBaseUrl}/api/crashreport");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", SessionState.AccessToken);
            request.Content = JsonContent.Create(new
            {
                deviceId = report.DeviceId,
                appVersion = report.AppVersion,
                exceptionType = report.ExceptionType,
                stackTrace = report.StackTrace,
                systemInfo = report.SystemInfo
            });

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var response = await Http.SendAsync(request, cts.Token);
            Debug.WriteLine($"[CrashReport] Sent: {(int)response.StatusCode} {report.ExceptionType}");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CrashReport] API send failed: {ex.Message}");
            return false;
        }
    }

    private static void SaveLocally(CrashPayload report)
    {
        try
        {
            Directory.CreateDirectory(LocalCrashDir);
            var fileName = $"crash_{DateTime.UtcNow:yyyyMMdd_HHmmss}_{Guid.NewGuid().ToString("N")[..6]}.json";
            var path = System.IO.Path.Combine(LocalCrashDir, fileName);
            File.WriteAllText(path, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }

    private static async Task FlushPendingReportsAsync()
    {
        try
        {
            if (!Directory.Exists(LocalCrashDir)) return;
            await Task.Delay(TimeSpan.FromSeconds(15));
            if (string.IsNullOrEmpty(SessionState.AccessToken)) return;

            var files = Directory.GetFiles(LocalCrashDir, "crash_*.json");
            foreach (var file in files)
            {
                try
                {
                    var json = await File.ReadAllTextAsync(file);
                    var report = JsonSerializer.Deserialize<CrashPayload>(json);
                    if (report is null) { File.Delete(file); continue; }
                    if (await TrySendToApiAsync(report)) File.Delete(file);
                }
                catch { }
            }
        }
        catch { }
    }

    private sealed class CrashPayload
    {
        public Guid DeviceId { get; set; }
        public string AppVersion { get; set; } = "";
        public string ExceptionType { get; set; } = "";
        public string StackTrace { get; set; } = "";
        public string SystemInfo { get; set; } = "{}";
    }
}
