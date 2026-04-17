using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AuraCore.UI.Avalonia.Helpers;
using AuraCore.UI.Avalonia.ViewModels.SpaceAnalyzer;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.Pages;

/// <summary>
/// Tests for the Space Analyzer tree scanner helper and DirectoryNodeVM
/// factory (Phase 5.5.4 — lazy tree drill-down).
/// These are pure-logic tests: no Avalonia headless window required.
/// </summary>
public class SpaceAnalyzerTreeTests
{
    // -------------------------------------------------------------------------
    // SpaceAnalyzerScanner
    // -------------------------------------------------------------------------

    [Fact]
    public async Task EnumerateChildrenAsync_lists_direct_files_and_dirs_by_size()
    {
        var tmp = Directory.CreateTempSubdirectory("sa-tree-test-");
        try
        {
            await File.WriteAllTextAsync(Path.Combine(tmp.FullName, "a.txt"), "AAAA");
            await File.WriteAllTextAsync(Path.Combine(tmp.FullName, "b.txt"), "BB");
            Directory.CreateDirectory(Path.Combine(tmp.FullName, "sub"));
            await File.WriteAllTextAsync(Path.Combine(tmp.FullName, "sub", "c.txt"), "CCCCCCCC");

            var children = await SpaceAnalyzerScanner.EnumerateChildrenAsync(tmp.FullName);

            // 1 directory ("sub") + 2 files ("a.txt", "b.txt")
            Assert.Equal(3, children.Count);
            Assert.Contains(children, c => c.IsDirectory && c.Path.EndsWith("sub"));
            Assert.Equal(2, children.Count(c => !c.IsDirectory));
        }
        finally { tmp.Delete(recursive: true); }
    }

    [Fact]
    public async Task EnumerateChildrenAsync_on_empty_dir_returns_empty()
    {
        var tmp = Directory.CreateTempSubdirectory("sa-tree-empty-");
        try
        {
            var children = await SpaceAnalyzerScanner.EnumerateChildrenAsync(tmp.FullName);
            Assert.Empty(children);
        }
        finally { tmp.Delete(recursive: true); }
    }

    [Fact]
    public async Task EnumerateChildrenAsync_nonexistent_path_returns_empty()
    {
        var path = @"C:\__does_not_exist__" + System.Guid.NewGuid();
        var children = await SpaceAnalyzerScanner.EnumerateChildrenAsync(path);
        Assert.Empty(children);
    }

    [Fact]
    public async Task EnumerateChildrenAsync_results_are_sorted_descending_by_size()
    {
        var tmp = Directory.CreateTempSubdirectory("sa-tree-sort-");
        try
        {
            // small file first so default directory order would be wrong
            await File.WriteAllTextAsync(Path.Combine(tmp.FullName, "small.txt"), "S");
            await File.WriteAllTextAsync(Path.Combine(tmp.FullName, "large.txt"), new string('X', 512));

            var children = await SpaceAnalyzerScanner.EnumerateChildrenAsync(tmp.FullName);

            Assert.True(children.Count >= 2);
            // First entry must be >= second in size
            Assert.True(children[0].SizeBytes >= children[1].SizeBytes);
        }
        finally { tmp.Delete(recursive: true); }
    }

    [Fact]
    public async Task EnumerateChildrenAsync_directory_entry_has_correct_flags()
    {
        var tmp = Directory.CreateTempSubdirectory("sa-tree-flags-");
        try
        {
            var sub = Directory.CreateDirectory(Path.Combine(tmp.FullName, "childDir"));
            await File.WriteAllTextAsync(Path.Combine(sub.FullName, "f.txt"), "data");

            var children = await SpaceAnalyzerScanner.EnumerateChildrenAsync(tmp.FullName);

            var dirEntry = children.Single(c => c.IsDirectory);
            Assert.True(dirEntry.HasChildren);
            Assert.Equal(sub.FullName, dirEntry.Path);
        }
        finally { tmp.Delete(recursive: true); }
    }

    // -------------------------------------------------------------------------
    // DirectoryNodeVM
    // -------------------------------------------------------------------------

    [Fact]
    public async Task LoadRootAsync_produces_DirectoryNodeVM_per_entry()
    {
        var tmp = Directory.CreateTempSubdirectory("sa-tree-root-");
        try
        {
            Directory.CreateDirectory(Path.Combine(tmp.FullName, "alpha"));
            Directory.CreateDirectory(Path.Combine(tmp.FullName, "beta"));
            await File.WriteAllTextAsync(Path.Combine(tmp.FullName, "f.txt"), "1");

            var roots = await DirectoryNodeVM.LoadRootAsync(tmp.FullName);

            Assert.Equal(3, roots.Count);
            Assert.Contains(roots, r => r.IsDirectory && r.DisplayName == "alpha");
            Assert.Contains(roots, r => r.IsDirectory && r.DisplayName == "beta");
        }
        finally { tmp.Delete(recursive: true); }
    }

    [Fact]
    public async Task LoadRootAsync_on_nonexistent_path_returns_empty()
    {
        var roots = await DirectoryNodeVM.LoadRootAsync(@"Z:\__missing_" + System.Guid.NewGuid());
        Assert.Empty(roots);
    }

    [Fact]
    public async Task DirectoryNodeVM_DisplayName_is_last_segment_of_path()
    {
        var tmp = Directory.CreateTempSubdirectory("sa-tree-dn-");
        try
        {
            var sub = Directory.CreateDirectory(Path.Combine(tmp.FullName, "MyFolder"));

            var roots = await DirectoryNodeVM.LoadRootAsync(tmp.FullName);
            var node  = roots.Single(r => r.IsDirectory);

            Assert.Equal("MyFolder", node.DisplayName);
        }
        finally { tmp.Delete(recursive: true); }
    }

    [Fact]
    public async Task DirectoryNodeVM_with_children_has_placeholder_before_expansion()
    {
        var tmp = Directory.CreateTempSubdirectory("sa-tree-ph-");
        try
        {
            var sub = Directory.CreateDirectory(Path.Combine(tmp.FullName, "hasChildren"));
            await File.WriteAllTextAsync(Path.Combine(sub.FullName, "inner.txt"), "content");

            var roots = await DirectoryNodeVM.LoadRootAsync(tmp.FullName);
            var dirNode = roots.Single(r => r.IsDirectory);

            // HasChildren should be true and a placeholder child should exist
            Assert.True(dirNode.HasChildren);
            Assert.Single(dirNode.Children); // placeholder before expansion
        }
        finally { tmp.Delete(recursive: true); }
    }
}
