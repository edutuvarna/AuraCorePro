using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using AuraCore.Application;
using AuraCore.Application.Interfaces.Modules;
using AuraCore.Application.Interfaces.Platform;
using AuraCore.Application.Shared;
using AuraCore.Domain.Enums;
using AuraCore.Module.DnsFlusher.Models;

namespace AuraCore.Module.DnsFlusher;

public sealed class DnsFlusherModule : IOptimizationModule
{
    private readonly IShellCommandService _shell;

    public DnsFlusherModule(IShellCommandService shell)
    {
        _shell = shell;
    }

    public string Id => "dns-flusher";
    public string DisplayName => "DNS Flusher";
    public OptimizationCategory Category => OptimizationCategory.NetworkOptimization;
    public RiskLevel Risk => RiskLevel.Low;
    public SupportedPlatform Platform => SupportedPlatform.MacOS;

    public DnsFlusherReport? LastReport { get; private set; }

    private static string GetStateFilePath()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var dir = Path.Combine(home, "Library", "Application Support", "AuraCorePro");
        return Path.Combine(dir, "dns_flusher_last.json");
    }

    public async Task<ScanResult> ScanAsync(ScanOptions options, CancellationToken ct = default)
    {
        if (!OperatingSystem.IsMacOS())
            return new ScanResult(Id, false, 0, 0, $"{DisplayName} is macOS-only.");

        try
        {
            var available = await ProcessRunner.CommandExistsAsync("dscacheutil", ct);
            DateTime? lastFlush = ReadLastFlush();

            var report = new DnsFlusherReport(available, lastFlush);
            LastReport = report;

            if (!available)
                return new ScanResult(Id, false, 0, 0, "dscacheutil not available");

            return new ScanResult(Id, true, 1, 0);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            Debug.WriteLine($"[{Id}] Scan error: {ex.Message}");
            return new ScanResult(Id, false, 0, 0, ex.Message);
        }
    }

    public async Task<OptimizationResult> OptimizeAsync(
        OptimizationPlan plan,
        IProgress<TaskProgress>? progress = null,
        CancellationToken ct = default)
    {
        var start = DateTime.UtcNow;
        var operationId = Guid.NewGuid().ToString("N")[..8];
        int processed = 0;

        if (!OperatingSystem.IsMacOS())
            return new OptimizationResult(Id, operationId, false, 0, 0, TimeSpan.Zero);

        try
        {
            var items = plan.SelectedItemIds?.ToList() ?? new List<string>();
            int totalSteps = Math.Max(items.Count, 1);

            for (int i = 0; i < items.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                var itemId = items[i];
                progress?.Report(new TaskProgress(Id, (double)i / totalSteps * 100, $"Flushing DNS ({itemId})..."));

                if (itemId == "flush")
                {
                    bool anyOk = false;

                    // Flush primary DNS cache via dscacheutil (requires privilege escalation).
                    // Per Task 27 DnsFlushArgvValidator, daemon hardcodes /usr/bin/dscacheutil.
                    var r1 = await _shell.RunPrivilegedAsync(
                        new PrivilegedCommand(
                            Id: "dns-flush",
                            Executable: "dscacheutil",
                            Arguments: new[] { "-flushcache" },
                            TimeoutSeconds: 60),
                        ct);
                    if (r1.Success) anyOk = true;

                    // Signal mDNSResponder daemon to reload caches (unprivileged, best-effort).
                    var r2 = await ProcessRunner.RunAsync("killall", "-HUP mDNSResponder", ct);
                    if (r2.Success) anyOk = true;

                    // Kill mDNSResponderHelper (unprivileged, best-effort; may not exist).
                    var r3 = await ProcessRunner.RunAsync("killall", "mDNSResponderHelper", ct);
                    // mDNSResponderHelper may not always be present, ignore failure

                    if (anyOk)
                    {
                        processed++;
                        WriteLastFlush(DateTime.UtcNow);
                    }
                }
            }

            progress?.Report(new TaskProgress(Id, 100, "Complete"));
            return new OptimizationResult(Id, operationId, true, processed, 0, DateTime.UtcNow - start);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            Debug.WriteLine($"[{Id}] Optimize error: {ex.Message}");
            return new OptimizationResult(Id, operationId, false, processed, 0, DateTime.UtcNow - start);
        }
    }

    public Task<bool> CanRollbackAsync(string operationId, CancellationToken ct = default) => Task.FromResult(false);
    public Task RollbackAsync(string operationId, CancellationToken ct = default) => Task.CompletedTask;

    // ---- State persistence ----

    private static DateTime? ReadLastFlush()
    {
        try
        {
            var path = GetStateFilePath();
            if (!File.Exists(path)) return null;
            var json = File.ReadAllText(path);
            var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("lastFlush", out var el) && el.TryGetDateTime(out var dt))
                return dt;
        }
        catch { /* ignore parse errors */ }
        return null;
    }

    private static void WriteLastFlush(DateTime when)
    {
        try
        {
            var path = GetStateFilePath();
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(new { lastFlush = when });
            File.WriteAllText(path, json);
        }
        catch { /* ignore write errors */ }
    }
}

public static class DnsFlusherRegistration
{
    public static IServiceCollection AddDnsFlusherModule(this IServiceCollection services)
    {
        services.AddSingleton<DnsFlusherModule>();
        services.AddSingleton<IOptimizationModule>(sp => sp.GetRequiredService<DnsFlusherModule>());
        return services;
    }
}
