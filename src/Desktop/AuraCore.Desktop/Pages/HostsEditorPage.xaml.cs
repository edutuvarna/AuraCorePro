using AuraCore.Application;
using AuraCore.Desktop.Services;
using AuraCore.Module.HostsEditor;
using AuraCore.Module.HostsEditor.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace AuraCore.Desktop.Pages;

public sealed partial class HostsEditorPage : Page
{
    private HostsEditorModule? _module;

    public HostsEditorPage()
    {
        InitializeComponent();
        ApplyLocalization();
        S.LanguageChanged += () => DispatcherQueue?.TryEnqueue(ApplyLocalization);
        Loaded += Page_Loaded;
    }

    private async void Page_Loaded(object sender, RoutedEventArgs e)
    {
        _module = App.Current.Services.GetServices<AuraCore.Application.Interfaces.Modules.IOptimizationModule>()
            .FirstOrDefault(m => m.Id == "hosts-editor") as HostsEditorModule;

        await RunScan();

        if (_module?.LastReport?.IsAdmin == false)
        {
            AdminWarning.Visibility = Visibility.Visible;
            AdminWarnText.Text = S._("hosts.adminWarning");
            SaveBtn.IsEnabled = false;
        }
    }

    private async Task RunScan()
    {
        if (_module is null) return;
        ScanProgress.IsActive = true;
        await _module.ScanAsync(new ScanOptions());
        RenderEntries();
        StatusText.Text = string.Format(S._("hosts.loaded"),
            _module.LastReport?.Entries.Count ?? 0,
            _module.LastReport?.EnabledCount ?? 0);
        ScanProgress.IsActive = false;
    }

    private void RenderEntries()
    {
        EntryList.Children.Clear();
        var entries = _module?.LastReport?.Entries ?? new();

        foreach (var entry in entries)
        {
            var row = new Grid
            {
                ColumnSpacing = 8, Padding = new Thickness(4, 6, 4, 6),
                Opacity = entry.IsReadOnly ? 0.5 : 1.0,
                BorderBrush = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["CardStrokeColorDefaultBrush"],
                BorderThickness = new Thickness(0, 0, 0, 0.5)
            };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });

            // Toggle
            var toggle = new ToggleSwitch
            {
                IsOn = entry.IsEnabled, IsEnabled = !entry.IsReadOnly,
                OnContent = "", OffContent = "",
                Margin = new Thickness(0, -4, 0, -4), VerticalAlignment = VerticalAlignment.Center,
                Tag = entry
            };
            toggle.Toggled += (s, e) =>
            {
                entry.IsEnabled = toggle.IsOn;
                StatusText.Text = S._("hosts.unsaved");
            };
            Grid.SetColumn(toggle, 0); row.Children.Add(toggle);

            // IP
            var ipText = new TextBlock { Text = entry.IpAddress, FontSize = 12, FontFamily = new FontFamily("Cascadia Code, Consolas, monospace"), VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(ipText, 1); row.Children.Add(ipText);

            // Hostname
            var hostText = new TextBlock { Text = entry.Hostname, FontSize = 12, FontFamily = new FontFamily("Cascadia Code, Consolas, monospace"), VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(hostText, 2); row.Children.Add(hostText);

            // Comment
            var commentText = new TextBlock { Text = entry.Comment, FontSize = 11, Opacity = 0.5, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(commentText, 3); row.Children.Add(commentText);

            // Source badge
            var srcColor = entry.Source switch
            {
                HostEntrySource.System => "#607D8B",
                HostEntrySource.Imported => "#9C27B0",
                _ => "#1565C0"
            };
            var srcBadge = new Border
            {
                Background = new SolidColorBrush(Windows.UI.Color.FromArgb(30,
                    Convert.ToByte(srcColor[1..3], 16), Convert.ToByte(srcColor[3..5], 16), Convert.ToByte(srcColor[5..7], 16))),
                CornerRadius = new CornerRadius(4), Padding = new Thickness(6, 2, 6, 2),
                VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Left
            };
            srcBadge.Child = new TextBlock { Text = entry.Source.ToString(), FontSize = 9,
                Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255,
                    Convert.ToByte(srcColor[1..3], 16), Convert.ToByte(srcColor[3..5], 16), Convert.ToByte(srcColor[5..7], 16))) };
            Grid.SetColumn(srcBadge, 4); row.Children.Add(srcBadge);

            // Delete button (not for system entries)
            if (!entry.IsReadOnly)
            {
                var capturedEntry = entry;
                var delBtn = new Button { Content = S._("common.remove"), Padding = new Thickness(8, 2, 8, 2), FontSize = 11 };
                delBtn.Click += (s, ev) =>
                {
                    _module?.DeleteEntry(capturedEntry.IpAddress, capturedEntry.Hostname);
                    RenderEntries();
                    StatusText.Text = S._("hosts.unsaved");
                };
                Grid.SetColumn(delBtn, 5); row.Children.Add(delBtn);
            }

            EntryList.Children.Add(row);
        }
    }

    private async void Add_Click(object sender, RoutedEventArgs e)
    {
        var ipBox   = new TextBox { PlaceholderText = "0.0.0.0", Width = 160 };
        var hostBox = new TextBox { PlaceholderText = "example.com", Width = 280 };
        var cBox    = new TextBox { PlaceholderText = "Comment (optional)", Width = 280 };

        var dlg = new ContentDialog
        {
            Title = S._("hosts.addTitle"),
            Content = new StackPanel { Spacing = 10, Children =
            {
                new TextBlock { Text = "IP Address:" }, ipBox,
                new TextBlock { Text = "Hostname:" }, hostBox,
                new TextBlock { Text = "Comment:" }, cBox
            }},
            PrimaryButtonText = S._("hosts.addBtn"),
            CloseButtonText = S._("common.cancel"),
            XamlRoot = this.XamlRoot, DefaultButton = ContentDialogButton.Primary
        };

        if (await dlg.ShowAsync() != ContentDialogResult.Primary) return;
        if (string.IsNullOrWhiteSpace(ipBox.Text) || string.IsNullOrWhiteSpace(hostBox.Text)) return;

        _module?.AddEntry(ipBox.Text.Trim(), hostBox.Text.Trim(), cBox.Text.Trim());
        RenderEntries();
        StatusText.Text = S._("hosts.unsaved");
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        try { _module?.SaveHostsFile(); StatusText.Text = S._("hosts.saved"); }
        catch (Exception ex) { StatusText.Text = S._("common.errorPrefix") + ex.Message; }
    }

    private void Backup_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var path = _module?.CreateBackup() ?? "";
            StatusText.Text = string.Format(S._("hosts.backedUp"), path);
        }
        catch (Exception ex) { StatusText.Text = S._("common.errorPrefix") + ex.Message; }
    }

    private async void Import_Click(object sender, RoutedEventArgs e)
    {
        var urlBox = new TextBox
        {
            PlaceholderText = "https://raw.githubusercontent.com/StevenBlack/hosts/master/hosts",
            Width = 420
        };
        var dlg = new ContentDialog
        {
            Title = S._("hosts.importTitle"),
            Content = new StackPanel { Spacing = 8, Children =
            {
                new TextBlock { Text = S._("hosts.importDesc"), TextWrapping = TextWrapping.Wrap, FontSize = 12, Opacity = 0.7 },
                urlBox
            }},
            PrimaryButtonText = S._("hosts.importBtn"),
            CloseButtonText = S._("common.cancel"),
            XamlRoot = this.XamlRoot
        };
        if (await dlg.ShowAsync() != ContentDialogResult.Primary) return;

        ScanProgress.IsActive = true;
        StatusText.Text = S._("hosts.importing");
        try
        {
            await _module!.ImportBlockListAsync(urlBox.Text.Trim());
            RenderEntries();
            StatusText.Text = S._("hosts.importedUnsaved");
        }
        catch (Exception ex) { StatusText.Text = S._("common.errorPrefix") + ex.Message; }
        finally { ScanProgress.IsActive = false; }
    }

    private void ApplyLocalization()
    {
        PageTitle.Text    = S._("hosts.title");
        PageSubtitle.Text = S._("hosts.subtitle");
        AddBtnText.Text   = S._("hosts.addBtn");
        SaveBtnText.Text  = S._("hosts.save");
        BackupBtnText.Text = S._("hosts.backup");
        ImportBtnText.Text = S._("hosts.import");
    }
}
