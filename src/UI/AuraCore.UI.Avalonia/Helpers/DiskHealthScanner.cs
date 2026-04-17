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
            return await Task.Run(() => ScanCore(), cancellationToken)
                             .ConfigureAwait(false);
        }
        catch
        {
            return Placeholder;
        }
    }

    // -------------------------------------------------------------------------
    // private
    // -------------------------------------------------------------------------

    private static DiskHealthScanResult ScanCore()
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

        // WorstTempText: disk temperature via SMART is platform-specific;
        // we return "—" for now since DriveInfo does not expose temperature.
        // A future phase can extend this via WMI/smartctl on the real SMART path.
        const string worstTempText = "—";

        return new DiskHealthScanResult(statusText, smartText, worstTempText);
    }
}
