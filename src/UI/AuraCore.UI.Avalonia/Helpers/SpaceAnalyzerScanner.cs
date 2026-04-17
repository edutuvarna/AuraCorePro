using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace AuraCore.UI.Avalonia.Helpers;

/// <summary>
/// A single filesystem entry (file or directory) returned by
/// <see cref="SpaceAnalyzerScanner.EnumerateChildrenAsync"/>.
/// </summary>
public sealed record DirectoryEntry(
    string Path,
    long SizeBytes,
    bool IsDirectory,
    bool HasChildren);

/// <summary>
/// Cross-platform, exception-safe filesystem enumeration helper for the
/// Space Analyzer tree. All public methods run on background threads and
/// tolerate access-denied / file-not-found errors at the per-entry level.
/// </summary>
public static class SpaceAnalyzerScanner
{
    /// <summary>
    /// Enumerates the direct children (files + sub-directories) of
    /// <paramref name="path"/>, sorted by descending size.
    /// Never throws; returns an empty list on any top-level error.
    /// </summary>
    public static Task<IReadOnlyList<DirectoryEntry>> EnumerateChildrenAsync(string path)
    {
        return Task.Run<IReadOnlyList<DirectoryEntry>>(() =>
        {
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
                return Array.Empty<DirectoryEntry>();

            var list = new List<DirectoryEntry>();

            // --- Directories ---
            IEnumerable<string> dirs;
            try { dirs = Directory.EnumerateDirectories(path); }
            catch { dirs = Array.Empty<string>(); }

            foreach (var dir in dirs)
            {
                long size = 0;
                bool hasChildren = false;
                try
                {
                    hasChildren = Directory.EnumerateFileSystemEntries(dir).Any();
                    size = ComputeDirectorySizeSafe(dir);
                }
                catch { /* unauthorized / deleted mid-scan — leave 0/false */ }
                list.Add(new DirectoryEntry(dir, size, IsDirectory: true, HasChildren: hasChildren));
            }

            // --- Files ---
            IEnumerable<string> files;
            try { files = Directory.EnumerateFiles(path); }
            catch { files = Array.Empty<string>(); }

            foreach (var file in files)
            {
                long size = 0;
                try { size = new FileInfo(file).Length; } catch { }
                list.Add(new DirectoryEntry(file, size, IsDirectory: false, HasChildren: false));
            }

            return list.OrderByDescending(e => e.SizeBytes).ToList();
        });
    }

    // -------------------------------------------------------------------------
    // private
    // -------------------------------------------------------------------------

    private static long ComputeDirectorySizeSafe(string dir)
    {
        long total = 0;
        try
        {
            foreach (var f in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
            {
                try { total += new FileInfo(f).Length; } catch { }
            }
        }
        catch { }
        return total;
    }
}
