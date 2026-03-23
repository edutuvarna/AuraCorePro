using System.Diagnostics;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Microsoft.Extensions.DependencyInjection;
using AuraCore.Application;
using AuraCore.Application.Interfaces.Modules;
using AuraCore.Domain.Enums;
using AuraCore.Module.NetworkOptimizer.Models;

namespace AuraCore.Module.NetworkOptimizer;

public sealed class NetworkOptimizerModule : IOptimizationModule
{
    public string Id => "network-optimizer";
    public string DisplayName => "Network Optimizer";
    public OptimizationCategory Category => OptimizationCategory.NetworkOptimization;
    public RiskLevel Risk => RiskLevel.Medium;

    public NetworkReport? LastReport { get; private set; }

    private static readonly List<DnsPreset> DnsPresets = new()
    {
        new() { Name = "Cloudflare", Description = "Fast, privacy-focused DNS", Primary = "1.1.1.1", Secondary = "1.0.0.1", Category = "Speed" },
        new() { Name = "Google", Description = "Reliable, widely used DNS", Primary = "8.8.8.8", Secondary = "8.8.4.4", Category = "Reliability" },
        new() { Name = "Quad9", Description = "Security-focused, blocks malware domains", Primary = "9.9.9.9", Secondary = "149.112.112.112", Category = "Security" },
        new() { Name = "OpenDNS", Description = "Cisco's DNS with phishing protection", Primary = "208.67.222.222", Secondary = "208.67.220.220", Category = "Security" },
        new() { Name = "Cloudflare Family", Description = "Blocks malware + adult content", Primary = "1.1.1.3", Secondary = "1.0.0.3", Category = "Family" },
        new() { Name = "AdGuard", Description = "Blocks ads and trackers at DNS level", Primary = "94.140.14.14", Secondary = "94.140.15.15", Category = "Ad Blocking" },
    };

    private static readonly (string host, string label)[] PingTargets =
    {
        ("1.1.1.1", "Cloudflare"),
        ("8.8.8.8", "Google"),
        ("208.67.222.222", "OpenDNS"),
        ("google.com", "Google.com"),
        ("microsoft.com", "Microsoft.com"),
    };

    public async Task<ScanResult> ScanAsync(ScanOptions options, CancellationToken ct = default)
    {
        var report = await Task.Run(async () =>
        {
            var adapters = GetAdapters();
            var dns = GetCurrentDns();
            var pings = await RunPingTestsAsync(ct);

            // Mark current DNS in presets
            var presets = DnsPresets.Select(p => p with
            {
                IsCurrentlyActive = p.Primary == dns.Primary
            }).ToList();

            int issues = 0;
            if (dns.ResponseTimeMs > 100) issues++;
            if (pings.Any(p => !p.Success)) issues++;
            if (string.IsNullOrEmpty(dns.Primary) || dns.Primary.StartsWith("192.168") || dns.Primary.StartsWith("10.")) issues++;

            return new NetworkReport
            {
                Adapters = adapters,
                CurrentDns = dns,
                PingResults = pings,
                AvailableDnsPresets = presets,
                IssuesFound = issues
            };
        }, ct);

        LastReport = report;
        return new ScanResult(Id, true, report.Adapters.Count + report.PingResults.Count, 0);
    }

    public async Task<OptimizationResult> OptimizeAsync(
        OptimizationPlan plan, IProgress<TaskProgress>? progress = null, CancellationToken ct = default)
    {
        // plan.SelectedItemIds[0] = action type, [1+] = parameters
        if (plan.SelectedItemIds.Count == 0)
            return new OptimizationResult(Id, "", false, 0, 0, TimeSpan.Zero);

        var start = DateTime.UtcNow;
        var action = plan.SelectedItemIds[0];
        bool success = false;

        switch (action)
        {
            case "change-dns":
                if (plan.SelectedItemIds.Count >= 3)
                {
                    var primary = plan.SelectedItemIds[1];
                    var secondary = plan.SelectedItemIds[2];
                    progress?.Report(new TaskProgress(Id, 50, $"Setting DNS to {primary}..."));
                    success = await ChangeDnsAsync(primary, secondary);
                }
                break;

            case "flush-dns":
                progress?.Report(new TaskProgress(Id, 50, "Flushing DNS cache..."));
                success = await FlushDnsAsync();
                break;

            case "reset-adapter":
                progress?.Report(new TaskProgress(Id, 30, "Disabling adapter..."));
                success = await ResetAdapterAsync();
                break;

            case "reset-winsock":
                progress?.Report(new TaskProgress(Id, 50, "Resetting Winsock catalog..."));
                success = await RunNetshAsync("winsock reset");
                break;

            case "release-renew-ip":
                progress?.Report(new TaskProgress(Id, 30, "Releasing IP address..."));
                await RunCmdAsync("ipconfig /release");
                progress?.Report(new TaskProgress(Id, 70, "Renewing IP address..."));
                success = await RunCmdAsync("ipconfig /renew");
                break;

            case "custom-dns":
                if (plan.SelectedItemIds.Count >= 3)
                {
                    var customPrimary = plan.SelectedItemIds[1];
                    var customSecondary = plan.SelectedItemIds[2];
                    progress?.Report(new TaskProgress(Id, 50, $"Setting custom DNS to {customPrimary} / {customSecondary}..."));
                    success = await ChangeDnsAsync(customPrimary, customSecondary);
                }
                break;
        }

        progress?.Report(new TaskProgress(Id, 100, "Done"));
        return new OptimizationResult(Id, Guid.NewGuid().ToString(), success, success ? 1 : 0, 0, DateTime.UtcNow - start);
    }

    public Task<bool> CanRollbackAsync(string operationId, CancellationToken ct = default)
        => Task.FromResult(false);

    public Task RollbackAsync(string operationId, CancellationToken ct = default)
        => Task.CompletedTask;

    private static List<NetworkAdapterInfo> GetAdapters()
    {
        var list = new List<NetworkAdapterInfo>();
        try
        {
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.NetworkInterfaceType != NetworkInterfaceType.Loopback))
            {
                var ipProps = nic.GetIPProperties();
                var ipv4 = ipProps.UnicastAddresses
                    .FirstOrDefault(a => a.Address.AddressFamily == AddressFamily.InterNetwork);

                var speed = nic.Speed switch
                {
                    > 1_000_000_000 => $"{nic.Speed / 1_000_000_000.0:F0} Gbps",
                    > 1_000_000 => $"{nic.Speed / 1_000_000.0:F0} Mbps",
                    > 0 => $"{nic.Speed / 1000.0:F0} Kbps",
                    _ => "Unknown"
                };

                list.Add(new NetworkAdapterInfo
                {
                    Name = nic.Name,
                    Description = nic.Description,
                    Status = nic.OperationalStatus.ToString(),
                    Speed = speed,
                    IpAddress = ipv4?.Address.ToString() ?? "N/A",
                    MacAddress = FormatMac(nic.GetPhysicalAddress().ToString()),
                    AdapterType = nic.NetworkInterfaceType.ToString()
                });
            }
        }
        catch { }
        return list;
    }

    private static DnsInfo GetCurrentDns()
    {
        try
        {
            var activeNic = NetworkInterface.GetAllNetworkInterfaces()
                .FirstOrDefault(n => n.OperationalStatus == OperationalStatus.Up
                    && n.NetworkInterfaceType != NetworkInterfaceType.Loopback);

            if (activeNic is null) return new DnsInfo();

            var dnsAddrs = activeNic.GetIPProperties().DnsAddresses
                .Where(a => a.AddressFamily == AddressFamily.InterNetwork)
                .ToList();

            var primary = dnsAddrs.FirstOrDefault()?.ToString() ?? "";
            var secondary = dnsAddrs.Skip(1).FirstOrDefault()?.ToString() ?? "";

            // Identify provider
            var provider = IdentifyDnsProvider(primary);

            // Measure response time
            double responseMs = 0;
            if (!string.IsNullOrEmpty(primary))
            {
                try
                {
                    var sw = Stopwatch.StartNew();
                    using var ping = new Ping();
                    var reply = ping.Send(primary, 2000);
                    sw.Stop();
                    if (reply.Status == IPStatus.Success)
                        responseMs = sw.Elapsed.TotalMilliseconds;
                }
                catch { }
            }

            return new DnsInfo { Primary = primary, Secondary = secondary, ProviderName = provider, ResponseTimeMs = responseMs };
        }
        catch { return new DnsInfo(); }
    }

    private static string IdentifyDnsProvider(string ip) => ip switch
    {
        "1.1.1.1" or "1.0.0.1" => "Cloudflare",
        "1.1.1.3" or "1.0.0.3" => "Cloudflare Family",
        "8.8.8.8" or "8.8.4.4" => "Google",
        "9.9.9.9" or "149.112.112.112" => "Quad9",
        "208.67.222.222" or "208.67.220.220" => "OpenDNS",
        "94.140.14.14" or "94.140.15.15" => "AdGuard",
        _ when ip.StartsWith("192.168") || ip.StartsWith("10.") || ip.StartsWith("172.") => "Router/ISP Default",
        _ => "ISP Default"
    };

    private static async Task<List<PingResult>> RunPingTestsAsync(CancellationToken ct)
    {
        var results = new List<PingResult>();
        foreach (var (host, label) in PingTargets)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                using var ping = new Ping();
                var reply = await ping.SendPingAsync(host, 3000);
                results.Add(new PingResult
                {
                    Host = host,
                    Label = label,
                    LatencyMs = reply.Status == IPStatus.Success ? reply.RoundtripTime : -1,
                    Success = reply.Status == IPStatus.Success
                });
            }
            catch
            {
                results.Add(new PingResult { Host = host, Label = label, LatencyMs = -1, Success = false });
            }
        }
        return results;
    }

    private static async Task<bool> ChangeDnsAsync(string primary, string secondary)
    {
        try
        {
            // Find the active adapter name
            var activeNic = NetworkInterface.GetAllNetworkInterfaces()
                .FirstOrDefault(n => n.OperationalStatus == OperationalStatus.Up
                    && n.NetworkInterfaceType != NetworkInterfaceType.Loopback);

            if (activeNic is null) return false;
            var name = activeNic.Name;

            var r1 = await RunNetshAsync($"interface ip set dns name=\"{name}\" static {primary}");
            var r2 = await RunNetshAsync($"interface ip add dns name=\"{name}\" {secondary} index=2");

            // Also flush DNS after changing
            await FlushDnsAsync();
            return r1;
        }
        catch { return false; }
    }

    private static async Task<bool> FlushDnsAsync()
    {
        return await RunCmdAsync("ipconfig /flushdns");
    }

    private static async Task<bool> ResetAdapterAsync()
    {
        var activeNic = NetworkInterface.GetAllNetworkInterfaces()
            .FirstOrDefault(n => n.OperationalStatus == OperationalStatus.Up
                && n.NetworkInterfaceType != NetworkInterfaceType.Loopback);

        if (activeNic is null) return false;
        var name = activeNic.Name;

        await RunNetshAsync($"interface set interface \"{name}\" disable");
        await Task.Delay(2000);
        await RunNetshAsync($"interface set interface \"{name}\" enable");
        return true;
    }

    private static async Task<bool> RunNetshAsync(string args)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "netsh",
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            if (proc is null) return false;
            await proc.WaitForExitAsync();
            return proc.ExitCode == 0;
        }
        catch { return false; }
    }

    private static async Task<bool> RunCmdAsync(string command)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c {command}",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            if (proc is null) return false;
            await proc.WaitForExitAsync();
            return proc.ExitCode == 0;
        }
        catch { return false; }
    }

    private static string FormatMac(string raw)
    {
        if (string.IsNullOrEmpty(raw) || raw.Length < 12) return "N/A";
        return string.Join(":", Enumerable.Range(0, 6).Select(i => raw.Substring(i * 2, 2)));
    }

    public async Task<DnsBenchmarkResult> BenchmarkDnsAsync(CancellationToken ct = default)
    {
        var presets = DnsPresets.ToList();
        var testDomains = new[] { "google.com", "cloudflare.com", "amazon.com", "github.com", "microsoft.com" };

        // Ping each DNS preset's primary address + resolve test domains
        foreach (var preset in presets)
        {
            double totalMs = 0;
            int successCount = 0;

            using var ping = new System.Net.NetworkInformation.Ping();
            for (int attempt = 0; attempt < 3; attempt++)
            {
                try
                {
                    ct.ThrowIfCancellationRequested();
                    var reply = await ping.SendPingAsync(preset.Primary, 3000);
                    if (reply.Status == System.Net.NetworkInformation.IPStatus.Success)
                    {
                        totalMs += reply.RoundtripTime;
                        successCount++;
                    }
                }
                catch { }
            }

            preset.LatencyMs = successCount > 0 ? totalMs / successCount : 9999;
        }

        // Sort by latency
        var ranked = presets.OrderBy(p => p.LatencyMs).ToList();
        var best = ranked.FirstOrDefault();
        var current = ranked.FirstOrDefault(p => p.IsCurrentlyActive);

        double? improvement = null;
        if (best is not null && current is not null && best.LatencyMs < current.LatencyMs)
            improvement = current.LatencyMs - best.LatencyMs;

        return new DnsBenchmarkResult
        {
            Rankings = ranked,
            Recommended = best,
            Current = current,
            ImprovementMs = improvement
        };
    }

    /// <summary>
    /// Performs a download speed test by fetching well-known test files from CDNs.
    /// Downloads a ~10MB file and measures throughput.
    /// </summary>
    public async Task<SpeedTestResult> RunSpeedTestAsync(
        IProgress<TaskProgress>? progress = null, CancellationToken ct = default)
    {
        // Multiple test URLs — try in order until one works
        var testUrls = new[]
        {
            ("https://speed.cloudflare.com/__down?bytes=10000000", "Cloudflare"),
            ("https://proof.ovh.net/files/1Mb.dat", "OVH"),
            ("https://ash-speed.hetzner.com/1GB.bin", "Hetzner"),
        };

        // First measure latency
        double latencyMs = 0;
        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync("1.1.1.1", 3000);
            if (reply.Status == IPStatus.Success)
                latencyMs = reply.RoundtripTime;
        }
        catch { }

        progress?.Report(new TaskProgress(Id, 10, "Connecting to speed test server..."));

        foreach (var (url, serverName) in testUrls)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
                httpClient.DefaultRequestHeaders.Add("User-Agent", "AuraCorePro/1.0 SpeedTest");

                var sw = Stopwatch.StartNew();
                long totalBytes = 0;

                using var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
                response.EnsureSuccessStatusCode();

                var contentLength = response.Content.Headers.ContentLength ?? 0;
                using var stream = await response.Content.ReadAsStreamAsync(ct);
                var buffer = new byte[65536]; // 64KB buffer
                int bytesRead;

                while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, ct)) > 0)
                {
                    totalBytes += bytesRead;
                    if (contentLength > 0)
                    {
                        var pct = (double)totalBytes / contentLength * 80 + 10; // 10-90%
                        var currentSpeed = totalBytes / (sw.Elapsed.TotalSeconds + 0.001) / 125000; // Mbps
                        progress?.Report(new TaskProgress(Id, pct,
                            $"Downloading... {currentSpeed:F1} Mbps ({totalBytes / (1024 * 1024.0):F1} MB)"));
                    }
                }

                sw.Stop();

                var durationSec = sw.Elapsed.TotalSeconds;
                var speedMbps = totalBytes * 8.0 / (durationSec * 1_000_000); // bits to Megabits

                progress?.Report(new TaskProgress(Id, 100,
                    $"Speed test complete — {speedMbps:F1} Mbps"));

                return new SpeedTestResult
                {
                    DownloadMbps = speedMbps,
                    LatencyMs = latencyMs,
                    BytesDownloaded = totalBytes,
                    DurationSeconds = durationSec,
                    Success = true,
                    ServerUsed = serverName
                };
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Try next URL
                continue;
            }
        }

        progress?.Report(new TaskProgress(Id, 100, "Speed test failed"));
        return new SpeedTestResult
        {
            Success = false,
            Error = "All test servers unreachable. Check your internet connection.",
            LatencyMs = latencyMs
        };
    }
}

public static class NetworkOptimizerRegistration
{
    public static IServiceCollection AddNetworkOptimizerModule(
        this IServiceCollection services)
        => services.AddSingleton<IOptimizationModule, NetworkOptimizerModule>();
}
