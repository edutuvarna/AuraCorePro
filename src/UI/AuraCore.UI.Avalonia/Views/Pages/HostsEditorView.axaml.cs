using global::Avalonia.Controls;
using global::Avalonia.Interactivity;
using global::Avalonia.Media;
using AuraCore.Application;
using AuraCore.Application.Interfaces.Modules;
using AuraCore.Module.HostsEditor;
using AuraCore.Module.HostsEditor.Models;
using Microsoft.Extensions.DependencyInjection;

namespace AuraCore.UI.Avalonia.Views.Pages;

public record HostDisplayItem(
    string IpAddress, string Hostname, string Comment, string SourceLabel,
    bool IsEnabled, bool CanEdit, double RowOpacity,
    string ToggleTag, string DeleteTag, string DeleteLabel);

public partial class HostsEditorView : UserControl
{
    private readonly HostsEditorModule? _module;
    private bool _hasUnsaved;

    public HostsEditorView()
    {
        InitializeComponent();
        Loaded += (s, e) => ApplyLocalization();
        LocalizationService.LanguageChanged += () =>
            global::Avalonia.Threading.Dispatcher.UIThread.Post(ApplyLocalization);
        Unloaded += (s, e) =>
            LocalizationService.LanguageChanged -= () =>
                global::Avalonia.Threading.Dispatcher.UIThread.Post(ApplyLocalization);
        _module = App.Services.GetServices<IOptimizationModule>()
            .OfType<HostsEditorModule>().FirstOrDefault();
        Loaded += async (s, e) => await LoadHosts();
    }

    private async Task LoadHosts()
    {
        if (_module is null) return;
        ReloadLabel.Text = LocalizationService._("common.loading");

        try
        {
            await _module.ScanAsync(new ScanOptions());
            var report = _module.LastReport;
            if (report is null) return;

            FilePath.Text = report.FilePath;
            EntryCount.Text = report.Entries.Count.ToString();

            if (report.IsAdmin)
            {
                AdminStatus.Text = LocalizationService._("common.yes");
                AdminStatus.Foreground = new SolidColorBrush(Color.Parse("#22C55E"));
            }
            else
            {
                AdminStatus.Text = LocalizationService._("hosts.notAdmin");
                AdminStatus.Foreground = new SolidColorBrush(Color.Parse("#F59E0B"));
            }

            // Update subtitle with hosts path
            SubtitleText.Text = OperatingSystem.IsWindows()
                ? LocalizationService._("hosts.subtitleWindows")
                : LocalizationService._("hosts.subtitleLinux");

            RenderEntries(report);
        }
        catch { SubtitleText.Text = LocalizationService._("hosts.loadFailed"); }
        finally { ReloadLabel.Text = LocalizationService._("hosts.reload"); }
    }

    private void RenderEntries(HostsReport report)
    {
        var deleteLabel = LocalizationService._("hosts.delete");
        var items = report.Entries.Select(e => new HostDisplayItem(
            e.IpAddress, e.Hostname, e.Comment,
            e.Source switch
            {
                HostEntrySource.System => "System",
                HostEntrySource.Imported => "Imported",
                _ => "Manual"
            },
            e.IsEnabled,
            !e.IsReadOnly && report.IsAdmin,
            e.IsEnabled ? 1.0 : 0.5,
            $"{(e.IsEnabled ? "disable" : "enable")}:{e.IpAddress}:{e.Hostname}",
            $"delete:{e.IpAddress}:{e.Hostname}",
            deleteLabel
        )).ToList();

        HostsList.ItemsSource = items;
    }

    private void SetUnsaved(bool val)
    {
        _hasUnsaved = val;
        UnsavedBadge.IsVisible = val;
    }

    private void AddEntry_Click(object? sender, RoutedEventArgs e)
    {
        if (_module is null) return;
        var ip = NewIp.Text?.Trim();
        var host = NewHost.Text?.Trim();
        var comment = NewComment.Text?.Trim() ?? "";

        if (string.IsNullOrEmpty(ip) || string.IsNullOrEmpty(host)) return;

        _module.AddEntry(ip, host, comment);
        SetUnsaved(true);

        // Clear inputs
        NewIp.Text = "";
        NewHost.Text = "";
        NewComment.Text = "";

        // Re-render
        if (_module.LastReport is not null)
        {
            EntryCount.Text = _module.LastReport.Entries.Count.ToString();
            RenderEntries(_module.LastReport);
        }
    }

    private void Toggle_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox cb || _module is null) return;
        var tag = cb.Tag?.ToString();
        if (string.IsNullOrEmpty(tag)) return;

        var parts = tag.Split(':', 3);
        if (parts.Length < 3) return;
        var action = parts[0];
        var ip = parts[1];
        var hostname = parts[2];

        _module.SetEnabled(ip, hostname, action == "enable");
        SetUnsaved(true);

        if (_module.LastReport is not null)
            RenderEntries(_module.LastReport);
    }

    private void Delete_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || _module is null) return;
        var tag = btn.Tag?.ToString();
        if (string.IsNullOrEmpty(tag)) return;

        var parts = tag.Split(':', 3);
        if (parts.Length < 3) return;

        _module.DeleteEntry(parts[1], parts[2]);
        SetUnsaved(true);

        if (_module.LastReport is not null)
        {
            EntryCount.Text = _module.LastReport.Entries.Count.ToString();
            RenderEntries(_module.LastReport);
        }
    }

    private void Save_Click(object? sender, RoutedEventArgs e)
    {
        if (_module is null) return;
        try
        {
            _module.SaveHostsFile();
            SetUnsaved(false);
        }
        catch { SubtitleText.Text = LocalizationService._("hosts.saveFailed"); }
    }

    private void Backup_Click(object? sender, RoutedEventArgs e)
    {
        if (_module is null) return;
        try
        {
            var path = _module.CreateBackup();
            SubtitleText.Text = string.Format(LocalizationService._("hosts.backedUp"), path);
        }
        catch { SubtitleText.Text = LocalizationService._("hosts.backupFailed"); }
    }

    private async void Reload_Click(object? sender, RoutedEventArgs e)
    {
        SetUnsaved(false);
        await LoadHosts();
    }

    private async void Import_Click(object? sender, RoutedEventArgs e)
    {
        if (_module is null) return;
        var url = ImportUrl.Text?.Trim();
        if (string.IsNullOrEmpty(url)) return;

        SubtitleText.Text = LocalizationService._("hosts.importing");
        try
        {
            await _module.ImportBlockListAsync(url);
            SetUnsaved(true);
            if (_module.LastReport is not null)
            {
                EntryCount.Text = _module.LastReport.Entries.Count.ToString();
                RenderEntries(_module.LastReport);
            }
            SubtitleText.Text = LocalizationService._("hosts.importedUnsaved");
        }
        catch { SubtitleText.Text = LocalizationService._("hosts.importFailed"); }
}

    private void ApplyLocalization()
    {
        var L = LocalizationService._;
        PageTitle.Text          = L("nav.hostsEditor");
        ModuleHdr.Title         = L("hosts.title");
        ModuleHdr.Subtitle      = L("hosts.subtitle");
        BackupLabel.Text        = L("hosts.backup");
        ReloadLabel.Text        = L("hosts.reload");
        FileLabel.Text          = L("hosts.colFile");
        EntriesLabel.Text       = L("hosts.colEntries");
        AdminLabel.Text         = L("hosts.colAdmin");
        UnsavedLabel.Text       = L("hosts.unsaved");
        ColIpLabel.Text         = L("hosts.colIp");
        ColHostLabel.Text       = L("hosts.colHostname");
        ColCommentLabel.Text    = L("hosts.colComment");
        ColSourceLabel.Text     = L("hosts.colSource");
        ColActionsLabel.Text    = L("hosts.colActions");
        NewIp.Watermark         = L("hosts.ipWatermark");
        NewHost.Watermark       = L("hosts.hostnameWatermark");
        NewComment.Watermark    = L("hosts.commentWatermark");
        AddLabel.Text           = L("hosts.addBtn");
        ImportUrl.Watermark     = L("hosts.importUrlWatermark");
        ImportBlockListLabel.Text = L("hosts.importBtn");
        SaveBtn.Content         = L("hosts.save");
        // Re-render entries so DeleteLabel picks up new locale
        if (_module?.LastReport is { } report) RenderEntries(report);
    }
}