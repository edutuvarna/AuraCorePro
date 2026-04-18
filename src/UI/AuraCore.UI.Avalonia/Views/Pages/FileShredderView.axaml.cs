using System.Security.Cryptography;
using global::Avalonia.Controls;
using global::Avalonia.Interactivity;
using global::Avalonia.Media;
using global::Avalonia.Threading;

namespace AuraCore.UI.Avalonia.Views.Pages;

public partial class FileShredderView : UserControl
{
    private readonly List<string> _files = new();

    public FileShredderView()
    {
        InitializeComponent();
        Loaded += (s, e) => ApplyLocalization();
        LocalizationService.LanguageChanged += () =>
            Dispatcher.UIThread.Post(ApplyLocalization);
        Unloaded += (s, e) =>
            LocalizationService.LanguageChanged -= () =>
                Dispatcher.UIThread.Post(ApplyLocalization);
    }

    private async void AddFiles_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;
            var files = await topLevel.StorageProvider.OpenFilePickerAsync(
                new global::Avalonia.Platform.Storage.FilePickerOpenOptions
                {
                    Title = LocalizationService._("shredder.picker.title"),
                    AllowMultiple = true
                });
            foreach (var f in files)
            {
                var path = f.Path.LocalPath;
                if (!_files.Contains(path))
                {
                    _files.Add(path);
                    FileList.Items.Add(new Border
                    {
                        CornerRadius = new global::Avalonia.CornerRadius(6),
                        Background = new SolidColorBrush(Color.Parse("#12FFFFFF")),
                        Padding = new global::Avalonia.Thickness(12, 8),
                        Margin = new global::Avalonia.Thickness(0, 0, 0, 4),
                        Child = new StackPanel
                        {
                            Children =
                            {
                                new TextBlock { Text = System.IO.Path.GetFileName(path), FontSize = 12,
                                    FontWeight = FontWeight.SemiBold, Foreground = new SolidColorBrush(Color.Parse("#E8E8F0")) },
                                new TextBlock { Text = path, FontSize = 10,
                                    Foreground = new SolidColorBrush(Color.Parse("#8888A0")) }
                            }
                        }
                    });
                }
            }
            FileCount.Text = string.Format(LocalizationService._("shredder.file.selectedCount"), _files.Count);
        }
        catch (Exception ex) { FileCount.Text = $"Error selecting files: {ex.Message}"; }
    }

    private void ClearFiles_Click(object? sender, RoutedEventArgs e)
    {
        _files.Clear();
        FileList.Items.Clear();
        FileCount.Text = LocalizationService._("shredder.file.noFiles");
    }

    private async void Shred_Click(object? sender, RoutedEventArgs e)
    {
        if (_files.Count == 0) { FileCount.Text = LocalizationService._("shredder.file.noFiles"); return; }

        var passes = Method7Pass.IsChecked == true ? 7 : Method3Pass.IsChecked == true ? 3 : 1;
        ShredBtn.IsEnabled = false;
        ProgressPanel.IsVisible = true;
        ProgressBar.Value = 0;

        var total = _files.Count;
        var completed = 0;

        await Task.Run(() =>
        {
            foreach (var filePath in _files.ToList())
            {
                try
                {
                    if (!File.Exists(filePath)) { completed++; continue; }
                    var fileSize = new FileInfo(filePath).Length;

                    Dispatcher.UIThread.Post(() =>
                    {
                        ProgressLabel.Text = string.Format(LocalizationService._("shredder.progress.shredding"), System.IO.Path.GetFileName(filePath));
                        ProgressDetail.Text = string.Format(LocalizationService._("shredder.progress.detail"), completed + 1, total, passes);
                    });

                    // Multi-pass overwrite
                    using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Write, FileShare.None))
                    {
                        var buffer = new byte[8192];
                        for (int pass = 0; pass < passes; pass++)
                        {
                            fs.Position = 0;
                            long written = 0;
                            while (written < fileSize)
                            {
                                // Alternate patterns: zeros, ones, random
                                if (pass % 3 == 0)
                                    Array.Clear(buffer);
                                else if (pass % 3 == 1)
                                    Array.Fill(buffer, (byte)0xFF);
                                else
                                    RandomNumberGenerator.Fill(buffer);

                                var toWrite = (int)Math.Min(buffer.Length, fileSize - written);
                                fs.Write(buffer, 0, toWrite);
                                written += toWrite;
                            }
                            fs.Flush();
                        }
                    }

                    // Rename to random name before delete (obscure original filename)
                    var dir = System.IO.Path.GetDirectoryName(filePath) ?? Environment.CurrentDirectory;
                    var randomName = System.IO.Path.Combine(dir, Guid.NewGuid().ToString("N")[..12] + ".tmp");
                    File.Move(filePath, randomName);
                    File.Delete(randomName);

                    completed++;
                    var pct = (double)completed / total * 100;
                    Dispatcher.UIThread.Post(() => ProgressBar.Value = pct);
                }
                catch (Exception ex)
                {
                    Dispatcher.UIThread.Post(() =>
                        ProgressDetail.Text = $"Error: {System.IO.Path.GetFileName(filePath)} - {ex.Message}");
                    completed++;
                }
            }
        });

        ProgressBar.Value = 100;
        ProgressLabel.Text = string.Format(LocalizationService._("shredder.progress.complete"), completed);
        ProgressDetail.Text = string.Format(LocalizationService._("shredder.progress.completedDetail"), passes);
        ShredBtn.IsEnabled = true;

        _files.Clear();
        FileList.Items.Clear();
        FileCount.Text = LocalizationService._("shredder.file.noFiles");

        NotificationService.Instance.Post(LocalizationService._("nav.fileShredder"),
            string.Format(LocalizationService._("shredder.notification.shredded"), completed, passes), NotificationType.Success);
    }

    private void ApplyLocalization()
    {
        PageTitle.Text = LocalizationService._("nav.fileShredder");
        var L = LocalizationService._;
        if (this.FindControl<global::AuraCore.UI.Avalonia.Views.Controls.ModuleHeader>("Header") is { } h)
        {
            h.Title = L("shredder.title");
            h.Subtitle = L("shredder.subtitle");
        }
        MethodLabel.Text = L("shredder.method.label");
        Method1Pass.Content = L("shredder.method.quick");
        Method3Pass.Content = L("shredder.method.standard");
        Method7Pass.Content = L("shredder.method.secure");
        FilesToShredLabel.Text = L("shredder.files.label");
        AddFilesBtn.Content = L("shredder.action.addFiles");
        ClearAllBtn.Content = L("shredder.action.clearAll");
        if (string.IsNullOrEmpty(FileCount.Text))
            FileCount.Text = L("shredder.file.noFiles");
        ShredBtn.Content = L("shredder.action.shred");
        WarningText.Text = L("shredder.warning");
    }
}
