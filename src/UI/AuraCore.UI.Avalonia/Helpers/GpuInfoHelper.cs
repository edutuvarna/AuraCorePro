using System;
using System.Diagnostics;
using System.IO;
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

    private static GpuInfo? DetectWindows()
    {
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
