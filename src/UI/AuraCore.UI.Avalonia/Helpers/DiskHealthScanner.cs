using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AuraCore.UI.Avalonia.Helpers;

/// <summary>
/// Lightweight summary produced by <see cref="DiskHealthScanner.ScanAsync"/>.
/// All properties are non-null; fields are "—" when data is unavailable.
/// </summary>
public sealed record DiskHealthScanResult(
    string StatusText,
    string SmartText,
    string WorstTempText);

/// <summary>
/// Cross-platform disk health scan helper used by both
/// <see cref="AuraCore.UI.Avalonia.Views.Pages.DiskHealthView"/> and the
/// Dashboard's DiskHealthSummaryCard (Phase 5.5.2.2.1).
///
/// Runs entirely on background threads; returns a plain record so callers can
/// marshal the result to the UI thread themselves.
///
/// Exception-safe: returns a placeholder result on any failure.
/// </summary>
public static class DiskHealthScanner
{
    // =========================================================================
    // Phase 5 debt-A3 — WMI SMART augmentation
    // =========================================================================

    /// <summary>
    /// A single temperature reading from one disk.
    /// </summary>
    public readonly record struct SmartSample(string DeviceId, int TempCelsius);

    /// <summary>
    /// Abstraction over the SMART temperature data source.
    /// Allows unit tests to inject a fake without WMI.
    /// </summary>
    public interface ISmartProbe
    {
        IReadOnlyList<SmartSample> Sample();
    }

    /// <summary>
    /// Returns the highest temperature across all samples, or null when the
    /// list is null/empty (caller should display placeholder).
    /// </summary>
    public static int? PickWorstTempCelsius(IReadOnlyList<SmartSample>? samples)
    {
        if (samples is null || samples.Count == 0) return null;
        int worst = int.MinValue;
        foreach (var s in samples)
        {
            if (s.TempCelsius > worst) worst = s.TempCelsius;
        }
        return worst == int.MinValue ? null : worst;
    }

    /// <summary>
    /// Formats a nullable temperature for display.
    /// Null → "—"; value → "45°C".
    /// </summary>
    public static string FormatWorstTemp(int? celsius)
        => celsius.HasValue ? $"{celsius.Value}°C" : "—";

    // =========================================================================
    // Public API
    // =========================================================================

    /// <summary>
    /// The result that is shown while no scan has completed yet.
    /// </summary>
    public static readonly DiskHealthScanResult Placeholder =
        new("—", "—", "—");

    /// <summary>
    /// Perform a drive scan on a background thread and return a summary.
    /// Never throws; returns <see cref="Placeholder"/> on any error.
    /// </summary>
    public static async Task<DiskHealthScanResult> ScanAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await Task.Run(() => ScanCore(new WmiSmartProbe()), cancellationToken)
                             .ConfigureAwait(false);
        }
        catch
        {
            return Placeholder;
        }
    }

    /// <summary>
    /// Synchronous, testable entry point.  Drive-enumeration result is
    /// produced by <see cref="ScanDrivesCore"/> then augmented with the
    /// worst temperature returned by <paramref name="probe"/>.
    /// </summary>
    public static DiskHealthScanResult ScanCore(ISmartProbe probe)
    {
        var driveResult = ScanDrivesCore();

        int? worst = null;
        try { worst = PickWorstTempCelsius(probe.Sample()); }
        catch { /* probe may throw — swallow and keep placeholder */ }

        return driveResult with { WorstTempText = FormatWorstTemp(worst) };
    }

    // =========================================================================
    // Private
    // =========================================================================

    /// <summary>
    /// Drive-enumeration logic (previously the body of ScanCore).
    /// Returns WorstTempText = "—"; <see cref="ScanCore(ISmartProbe)"/> overwrites it.
    /// </summary>
    private static DiskHealthScanResult ScanDrivesCore()
    {
        var results = new List<(string Name, string Health, double UsedPct)>();

        foreach (var d in DriveInfo.GetDrives())
        {
            try
            {
                if (!d.IsReady) continue;
                var totalGb = d.TotalSize / (1024.0 * 1024 * 1024);
                var freeGb  = d.AvailableFreeSpace / (1024.0 * 1024 * 1024);
                var usedPct = totalGb > 0 ? (totalGb - freeGb) / totalGb * 100.0 : 0.0;
                var health  = usedPct > 95 ? "Critical"
                            : usedPct > 85 ? "Warning"
                            : "Healthy";
                results.Add((d.Name, health, usedPct));
            }
            catch { /* skip individual drive read failures */ }
        }

        if (results.Count == 0)
            return new DiskHealthScanResult("No drives found", "—", "—");

        // StatusText: any critical/warning drives stand out; otherwise "All drives healthy"
        var criticalCount = results.Count(r => r.Health == "Critical");
        var warningCount  = results.Count(r => r.Health == "Warning");

        string statusText;
        string smartText;
        if (criticalCount > 0)
        {
            statusText = criticalCount == 1
                ? "1 drive critically full"
                : $"{criticalCount} drives critically full";
            smartText = "Fail";
        }
        else if (warningCount > 0)
        {
            statusText = warningCount == 1
                ? "1 drive nearly full"
                : $"{warningCount} drives nearly full";
            smartText = "Warning";
        }
        else
        {
            statusText = results.Count == 1
                ? "Drive is healthy"
                : "All drives healthy";
            smartText = "OK";
        }

        // WorstTempText will be set by ScanCore(ISmartProbe) after drive enumeration.
        return new DiskHealthScanResult(statusText, smartText, "—");
    }

    /// <summary>
    /// Production SMART probe: queries ROOT\WMI\MSStorageDriver_ATAPISmartData
    /// for attribute 0xC2 (temperature) or 0xBE (airflow temp).
    /// Returns an empty list on any failure so the caller gracefully falls back
    /// to the placeholder dash.
    /// </summary>
    private sealed class WmiSmartProbe : ISmartProbe
    {
        public IReadOnlyList<SmartSample> Sample()
        {
            var list = new List<SmartSample>();
            if (!System.OperatingSystem.IsWindows()) return list;

            try
            {
                using var searcher = new System.Management.ManagementObjectSearcher(
                    "root\\WMI",
                    "SELECT InstanceName, VendorSpecific FROM MSStorageDriver_ATAPISmartData");
                foreach (System.Management.ManagementObject o in searcher.Get())
                {
                    try
                    {
                        var instanceName = o["InstanceName"]?.ToString() ?? "";
                        var bytes = o["VendorSpecific"] as byte[];
                        if (bytes is null || bytes.Length < 14) continue;

                        // SMART data layout: 2-byte header followed by 12-byte attribute records.
                        // Each record: [AttrId(1), Flags(2), Current(1), Worst(1), RawBytes(6), Reserved(1)]
                        for (int i = 2; i + 12 <= bytes.Length; i += 12)
                        {
                            var attrId = bytes[i];
                            if (attrId == 0xC2 /* temperature */ || attrId == 0xBE /* airflow temp */)
                            {
                                // Raw bytes start at offset i+5 (after attrId + flags(2) + current + worst).
                                var rawLow = bytes[i + 5];
                                if (rawLow is > 0 and < 120)
                                {
                                    list.Add(new SmartSample(instanceName, rawLow));
                                    break; // one temperature reading per disk is enough
                                }
                            }
                        }
                    }
                    catch { /* one bad drive shouldn't abort the whole scan */ }
                }
            }
            catch
            {
                // WMI denied / namespace missing / not supported — return empty list.
            }
            return list;
        }
    }
}
