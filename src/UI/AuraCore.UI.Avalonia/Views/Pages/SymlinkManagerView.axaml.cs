using System.Diagnostics;
using global::Avalonia.Controls;
using global::Avalonia.Interactivity;
using global::Avalonia.Media;
using AuraCore.Module.SymlinkManager;
using Microsoft.Extensions.DependencyInjection;

namespace AuraCore.UI.Avalonia.Views.Pages;

public partial class SymlinkManagerView : UserControl
{
    public SymlinkManagerView()
    {
        InitializeComponent();
        Loaded += (s, e) => ApplyLocalization();
        LocalizationService.LanguageChanged += () =>
            global::Avalonia.Threading.Dispatcher.UIThread.Post(ApplyLocalization);
    }

    private static SymlinkManagerModule? GetModule()
    {
        try { return App.Services.GetService<SymlinkManagerModule>(); }
        catch { return null; }
    }

    private async void CreateLink_Click(object? sender, RoutedEventArgs e)
    {
        var linkPath = LinkPathBox.Text?.Trim();
        var targetPath = TargetPathBox.Text?.Trim();
        if (string.IsNullOrEmpty(linkPath) || string.IsNullOrEmpty(targetPath))
        {
            StatusText.Text = LocalizationService._("symlink.pathRequired");
            return;
        }

        var type = (LinkTypeCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "file";

        try
        {
            StatusText.Text = LocalizationService._("symlink.creatingLink");

            // On Linux: route through the privileged module for proper polkit elevation
            if (OperatingSystem.IsLinux() && (type == "file" || type == "dir"))
            {
                var module = GetModule();
                if (module is not null)
                {
                    // source = link name (linkPath), target = what it points to (targetPath)
                    var outcome = await module.CreateSymlinkAsync(linkPath, targetPath);
                    StatusText.Text = outcome.Success
                        ? $"OK: Created symlink {linkPath} -> {targetPath}"
                        : $"Error: {outcome.Error}";
                    if (outcome.Success)
                        NotificationService.Instance.Post("Symlink Manager",
                            $"Created {linkPath} -> {targetPath}", NotificationType.Success);
                    return;
                }
            }

            var result = await Task.Run(() =>
            {
                try
                {
                    if (OperatingSystem.IsWindows())
                    {
                        var cmd = type switch
                        {
                            "file" => $"mklink \"{linkPath}\" \"{targetPath}\"",
                            "dir" => $"mklink /D \"{linkPath}\" \"{targetPath}\"",
                            "junction" => $"mklink /J \"{linkPath}\" \"{targetPath}\"",
                            "hard" => $"mklink /H \"{linkPath}\" \"{targetPath}\"",
                            _ => ""
                        };
                        var psi = new ProcessStartInfo("cmd.exe", $"/c {cmd}")
                        {
                            RedirectStandardOutput = true, RedirectStandardError = true,
                            UseShellExecute = false, CreateNoWindow = true
                        };
                        using var proc = Process.Start(psi);
                        proc?.WaitForExit(10000);
                        var output = proc?.StandardOutput.ReadToEnd() ?? "";
                        var error = proc?.StandardError.ReadToEnd() ?? "";
                        return string.IsNullOrEmpty(error) ? $"OK: {output.Trim()}" : $"Error: {error.Trim()}";
                    }
                    else
                    {
                        // Linux/macOS fallback (no privilege helper): ln -s
                        File.CreateSymbolicLink(linkPath, targetPath);
                        return $"OK: Created symlink {linkPath} -> {targetPath}";
                    }
                }
                catch (Exception ex) { return $"Error: {ex.Message}"; }
            });
            StatusText.Text = result;
            if (result.StartsWith("OK"))
                NotificationService.Instance.Post("Symlink Manager", result, NotificationType.Success);
        }
        catch (Exception ex) { StatusText.Text = $"Error: {ex.Message}"; }
    }

    private async void Scan_Click(object? sender, RoutedEventArgs e)
    {
        var scanPath = ScanPathBox.Text?.Trim();
        if (string.IsNullOrEmpty(scanPath) || !Directory.Exists(scanPath))
        {
            ScanStatus.Text = LocalizationService._("symlink.validDirRequired");
            return;
        }

        ScanStatus.Text = LocalizationService._("symlink.scanning");
        var links = await Task.Run(() =>
        {
            var found = new List<(string Path, string Target, string Type)>();
            try
            {
                foreach (var entry in new DirectoryInfo(scanPath).EnumerateFileSystemInfos("*", SearchOption.TopDirectoryOnly))
                {
                    if (entry.LinkTarget != null)
                    {
                        var kind = entry is DirectoryInfo ? "Dir Symlink" : "File Symlink";
                        if (entry.Attributes.HasFlag(FileAttributes.ReparsePoint) && entry is DirectoryInfo)
                            kind = "Junction/Symlink";
                        found.Add((entry.FullName, entry.LinkTarget, kind));
                    }
                }
            }
            catch { }
            return found;
        });

        ScanStatus.Text = string.Format(LocalizationService._("symlink.foundLinks"), links.Count);
        LinkList.ItemsSource = links.Select(l => new Border
        {
            CornerRadius = new global::Avalonia.CornerRadius(6),
            Background = new SolidColorBrush(Color.Parse("#12FFFFFF")),
            Padding = new global::Avalonia.Thickness(12, 8),
            Margin = new global::Avalonia.Thickness(0, 0, 0, 4),
            Child = new StackPanel
            {
                Children =
                {
                    new StackPanel { Orientation = global::Avalonia.Layout.Orientation.Horizontal, Spacing = 8, Children = {
                        new TextBlock { Text = System.IO.Path.GetFileName(l.Path), FontSize = 12, FontWeight = FontWeight.SemiBold,
                            Foreground = new SolidColorBrush(Color.Parse("#00D4AA")) },
                        new Border { CornerRadius = new global::Avalonia.CornerRadius(4), Padding = new global::Avalonia.Thickness(6,2),
                            Background = new SolidColorBrush(Color.Parse("#200080FF")),
                            Child = new TextBlock { Text = l.Type, FontSize = 9, Foreground = new SolidColorBrush(Color.Parse("#0080FF")) }}
                    }},
                    new TextBlock { Text = $"-> {l.Target}", FontSize = 11,
                        Foreground = new SolidColorBrush(Color.Parse("#8888A0")), TextWrapping = global::Avalonia.Media.TextWrapping.Wrap }
                }
            }
        }).ToList();
    }

    private void ApplyLocalization()
    {
        PageTitle.Text = LocalizationService._("nav.symlinkManager");
        ModuleHdr.Title = LocalizationService._("nav.symlinkManager");
        ModuleHdr.Subtitle = LocalizationService._("symlink.subtitle");
        IntroText.Text = LocalizationService._("symlink.intro");
        CreateHeader.Text = LocalizationService._("symlink.createHeader");
        LinkTypeFile.Content = LocalizationService._("symlink.typeFile");
        LinkTypeDir.Content = LocalizationService._("symlink.typeDir");
        LinkTypeJunction.Content = LocalizationService._("symlink.typeJunction");
        LinkTypeHard.Content = LocalizationService._("symlink.typeHard");
        LinkPathBox.Watermark = LocalizationService._("symlink.linkPathWatermark");
        TargetPathBox.Watermark = LocalizationService._("symlink.targetPathWatermark");
        CreateLinkBtn.Content = LocalizationService._("symlink.createBtn");
        ExistingHeader.Text = LocalizationService._("symlink.existingHeader");
        ScanDirBtn.Content = LocalizationService._("symlink.scanDirBtn");
        ScanPathBox.Watermark = LocalizationService._("symlink.scanPathWatermark");
        if (string.IsNullOrEmpty(ScanStatus.Text))
            ScanStatus.Text = LocalizationService._("symlink.scanHint");
        AdminWarningText.Text = LocalizationService._("symlink.adminWarning");
    }
}
