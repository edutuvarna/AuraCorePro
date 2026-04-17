using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Avalonia.Threading;
using AuraCore.UI.Avalonia.Helpers;

namespace AuraCore.UI.Avalonia.ViewModels.SpaceAnalyzer;

/// <summary>
/// ViewModel for a single node (file or directory) in the Space Analyzer
/// lazy-loading tree. Expanding a directory node triggers an asynchronous
/// scan of its direct children; results are marshalled to the UI thread via
/// <see cref="Dispatcher.UIThread"/>.
/// </summary>
public sealed class DirectoryNodeVM : INotifyPropertyChanged
{
    private bool _isExpanded;
    private bool _childrenLoaded;

    // -------------------------------------------------------------------------
    // Properties
    // -------------------------------------------------------------------------

    public string Path { get; }
    public string DisplayName { get; }
    public long SizeBytes { get; }
    public bool IsDirectory { get; }
    public bool HasChildren { get; }

    /// <summary>
    /// Live children collection. Contains a single placeholder entry while
    /// children have not yet been loaded (so that Avalonia's TreeView renders
    /// the expand chevron before we fetch real data).
    /// </summary>
    public ObservableCollection<DirectoryNodeVM> Children { get; } = new();

    /// <summary>
    /// Bound to the TreeViewItem's IsExpanded property. Setting it to
    /// <c>true</c> triggers a lazy child-load on the first expansion.
    /// </summary>
    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded == value) return;
            _isExpanded = value;
            RaisePropertyChanged();
            if (value && !_childrenLoaded && IsDirectory && HasChildren)
                _ = LoadChildrenAsync();
        }
    }

    // -------------------------------------------------------------------------
    // Construction
    // -------------------------------------------------------------------------

    public DirectoryNodeVM(DirectoryEntry entry)
    {
        Path        = entry.Path;
        DisplayName = System.IO.Path.GetFileName(
                          entry.Path.TrimEnd(System.IO.Path.DirectorySeparatorChar))
                      is { Length: > 0 } name ? name : entry.Path;
        SizeBytes   = entry.SizeBytes;
        IsDirectory = entry.IsDirectory;
        HasChildren = entry.HasChildren;

        // Add a placeholder child so the expand chevron appears before the real
        // scan completes. The placeholder is cleared in LoadChildrenAsync.
        if (IsDirectory && HasChildren)
            Children.Add(Placeholder());
    }

    // -------------------------------------------------------------------------
    // Public factory
    // -------------------------------------------------------------------------

    /// <summary>
    /// Scans <paramref name="path"/> on a background thread and returns one
    /// <see cref="DirectoryNodeVM"/> per direct child, sorted by size descending.
    /// </summary>
    public static async Task<IReadOnlyList<DirectoryNodeVM>> LoadRootAsync(string path)
    {
        var entries = await SpaceAnalyzerScanner.EnumerateChildrenAsync(path)
                                                .ConfigureAwait(false);
        var result = new List<DirectoryNodeVM>(entries.Count);
        foreach (var e in entries)
            result.Add(new DirectoryNodeVM(e));
        return result;
    }

    // -------------------------------------------------------------------------
    // Private
    // -------------------------------------------------------------------------

    private async Task LoadChildrenAsync()
    {
        _childrenLoaded = true;

        var entries = await SpaceAnalyzerScanner.EnumerateChildrenAsync(Path)
                                                .ConfigureAwait(false);

        Dispatcher.UIThread.Post(() =>
        {
            Children.Clear();
            foreach (var e in entries)
                Children.Add(new DirectoryNodeVM(e));
        });
    }

    /// <summary>Returns a lightweight placeholder node (no path, zero size).</summary>
    private static DirectoryNodeVM Placeholder()
        => new(new DirectoryEntry(string.Empty, 0, IsDirectory: false, HasChildren: false));

    // -------------------------------------------------------------------------
    // INPC
    // -------------------------------------------------------------------------

    public event PropertyChangedEventHandler? PropertyChanged;

    private void RaisePropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
