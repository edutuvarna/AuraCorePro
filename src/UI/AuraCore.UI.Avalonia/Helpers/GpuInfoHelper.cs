using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace AuraCore.UI.Avalonia.Helpers;

public sealed record GpuInfo(string Name, double UsagePercent, double? TemperatureC);

/// <summary>
/// Cross-platform GPU detection and current-usage helper.
/// All public methods are exception-safe: return null or sensible defaults on failure.
/// </summary>
public static class GpuInfoHelper
{
    public static GpuInfo? Detect()
    {
        try
        {
            if (OperatingSystem.IsWindows()) return DetectWindows();
            if (OperatingSystem.IsLinux()) return DetectLinux();
            if (OperatingSystem.IsMacOS()) return DetectMacOS();
        }
        catch { }
        return null;
    }

    public static double GetCurrentUsage()
    {
        try
        {
            if (OperatingSystem.IsWindows()) return GetUsageWindows();
            if (OperatingSystem.IsLinux()) return GetUsageLinux();
        }
        catch { }
        return 0.0;
    }

    /// <summary>
    /// Rank a GPU name to prefer discrete over integrated. Higher = preferred.
    /// 30+ : clearly discrete (GeForce RTX/GTX/Quadro, Radeon RX/Pro, Arc)
    /// 10  : ambiguous (just "NVIDIA" or "AMD" with no series)
    /// 0   : clearly integrated ("Graphics", "HD Graphics", "Iris", "UHD")
    /// </summary>
    private static int RankGpuName(string name)
    {
        var n = name.ToLowerInvariant();

        // Strong discrete signals
        if (n.Contains("rtx") || n.Contains("gtx") || n.Contains("quadro") || n.Contains("tesla")) return 40;
        if (n.Contains("radeon rx") || n.Contains("radeon pro") || n.Contains("radeon vii")) return 35;
        if (n.Contains("geforce")) return 30;
        if (n.Contains("arc a") && !n.Contains("graphics")) return 25; // Intel Arc discrete

        // Strong integrated signals
        if (n.Contains("radeon(tm) graphics") || n.Contains("radeon graphics") ||
            n.Contains("hd graphics") || n.Contains("iris") || n.Contains("uhd graphics") ||
            n.Contains("vega 3") || n.Contains("vega 7") || n.Contains("vega 8") || n.Contains("vega 11"))
            return 0;

        // Generic fallback
        if (n.Contains("nvidia") || n.Contains("amd")) return 10;
        return 5;
    }

    [global::System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static GpuInfo? DetectWindows()
    {
        // Primary path: WMI via System.Management (works on Windows 11 26200 where wmic is removed).
        // Prefer DISCRETE GPU over integrated: rank by explicit name keywords first,
        // then by AdapterRAM (uint32 field — may underflow for >=4GB discrete cards;
        // both integrated AMD Graphics and Intel iGPU typically report low/shared values).
        try
        {
            using var searcher = new global::System.Management.ManagementObjectSearcher(
                "SELECT Name, AdapterRAM FROM Win32_VideoController");
            var candidates = new System.Collections.Generic.List<(string Name, long Ram, int Priority)>();

            foreach (var obj in searcher.Get())
            {
                try
                {
                    var name = obj["Name"]?.ToString()?.Trim();
                    if (string.IsNullOrWhiteSpace(name)) continue;
                    long ram = 0;
                    try
                    {
                        if (obj["AdapterRAM"] is not null)
                            long.TryParse(obj["AdapterRAM"].ToString(), out ram);
                    }
                    catch { }

                    // Priority: higher = preferred (discrete GPU)
                    var priority = RankGpuName(name);
                    candidates.Add((name, ram, priority));
                }
                finally { obj.Dispose(); }
            }

            if (candidates.Count > 0)
            {
                // Pick highest priority; tiebreak by AdapterRAM; tiebreak by first encountered
                var best = candidates
                    .OrderByDescending(c => c.Priority)
                    .ThenByDescending(c => c.Ram)
                    .First();
                return new GpuInfo(best.Name, GetUsageWindows(), null);
            }
        }
        catch { }

        // Fallback: legacy wmic (still present on older Windows builds)
        try
        {
            using var p = Process.Start(new ProcessStartInfo
            {
                FileName = "wmic",
                Arguments = "path win32_VideoController get name",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            if (p is null) return null;
            var output = p.StandardOutput.ReadToEnd();
            p.WaitForExit(2000);

            foreach (var line in output.Split('\n'))
            {
                var name = line.Trim();
                if (!string.IsNullOrEmpty(name) && !name.Equals("Name", StringComparison.OrdinalIgnoreCase))
                    return new GpuInfo(name, GetUsageWindows(), null);
            }
        }
        catch { }
        return null;
    }

    private static double GetUsageWindows()
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo
            {
                FileName = "typeperf",
                Arguments = "\"\\GPU Engine(*)\\Utilization Percentage\" -sc 1",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            if (p is null) return 0.0;
            var output = p.StandardOutput.ReadToEnd();
            p.WaitForExit(2000);

            double total = 0;
            foreach (var line in output.Split('\n'))
            {
                var parts = line.Split(',');
                foreach (var part in parts)
                {
                    var cleaned = part.Trim('"', ' ', '\r');
                    if (double.TryParse(cleaned, System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out var v) && v >= 0 && v <= 100)
                        total += v;
                }
            }
            return Math.Min(total, 100.0);
        }
        catch { return 0.0; }
    }

    private static GpuInfo? DetectLinux()
    {
        try
        {
            var cardPath = "/sys/class/drm/card0/device";
            if (!Directory.Exists(cardPath)) return null;
            var name = TryReadLinuxGpuName() ?? "GPU";
            return new GpuInfo(name, GetUsageLinux(), null);
        }
        catch { return null; }
    }

    private static string? TryReadLinuxGpuName()
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo
            {
                FileName = "lspci",
                Arguments = "",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            if (p is null) return null;
            var output = p.StandardOutput.ReadToEnd();
            p.WaitForExit(2000);
            foreach (var line in output.Split('\n'))
            {
                if (line.Contains("VGA compatible controller", StringComparison.OrdinalIgnoreCase) ||
                    line.Contains("3D controller", StringComparison.OrdinalIgnoreCase))
                {
                    var idx = line.IndexOf(':', line.IndexOf(':') + 1);
                    if (idx > 0) return line[(idx + 1)..].Trim();
                }
            }
        }
        catch { }
        return null;
    }

    private static double GetUsageLinux()
    {
        try
        {
            var path = "/sys/class/drm/card0/device/gpu_busy_percent";
            if (File.Exists(path))
            {
                var text = File.ReadAllText(path).Trim();
                if (double.TryParse(text, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var v))
                    return Math.Clamp(v, 0, 100);
            }
        }
        catch { }
        return 0.0;
    }

    private static GpuInfo? DetectMacOS()
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo
            {
                FileName = "system_profiler",
                Arguments = "SPDisplaysDataType",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            if (p is null) return null;
            var output = p.StandardOutput.ReadToEnd();
            p.WaitForExit(2000);
            foreach (var line in output.Split('\n'))
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("Chipset Model:", StringComparison.OrdinalIgnoreCase))
                {
                    var name = trimmed["Chipset Model:".Length..].Trim();
                    if (!string.IsNullOrEmpty(name))
                        return new GpuInfo(name, 0.0, null);
                }
            }
        }
        catch { }
        return null;
    }
}
