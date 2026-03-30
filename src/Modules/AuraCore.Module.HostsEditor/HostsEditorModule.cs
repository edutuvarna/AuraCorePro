using Microsoft.Extensions.DependencyInjection;
using AuraCore.Application;
using AuraCore.Application.Interfaces.Modules;
using AuraCore.Domain.Enums;
using AuraCore.Module.HostsEditor.Models;

namespace AuraCore.Module.HostsEditor;

public sealed class HostsEditorModule : IOptimizationModule
{
    public string Id          => "hosts-editor";
    public string DisplayName => "Hosts File Editor";
    public OptimizationCategory Category => OptimizationCategory.NetworkTools;
    public RiskLevel Risk     => RiskLevel.High;
    public SupportedPlatform Platform => SupportedPlatform.All;

    public static readonly string HostsPath =
        OperatingSystem.IsWindows()
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"drivers\etc\hosts")
            : "/etc/hosts";

    private static readonly HashSet<string> _readOnlyHostnames =
        new(StringComparer.OrdinalIgnoreCase) { "localhost", "localhost.localdomain" };

    public HostsReport? LastReport { get; private set; }

    public async Task<ScanResult> ScanAsync(ScanOptions options, CancellationToken ct = default)
    {
        var entries = new List<HostEntry>();
        bool isAdmin = false;

        await Task.Run(() =>
        {
            isAdmin = IsRunningAsAdmin();
            if (!File.Exists(HostsPath)) return;

            var lines = File.ReadAllLines(HostsPath);
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (string.IsNullOrEmpty(line)) continue;

                var enabled  = !line.StartsWith('#');
                var content  = enabled ? line : line.TrimStart('#').TrimStart();
                var comment  = "";

                // Extract inline comment
                var commentIdx = content.IndexOf('#');
                if (commentIdx >= 0)
                {
                    comment = content[(commentIdx + 1)..].Trim();
                    content = content[..commentIdx].Trim();
                }

                var parts = content.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2) continue;

                var ip   = parts[0];
                var host = parts[1];

                // Skip lines where first token isn't a valid IP address
                if (!System.Net.IPAddress.TryParse(ip, out _)) continue;

                var isSystem = _readOnlyHostnames.Contains(host) ||
                               ip == "127.0.0.1" || ip == "::1" || ip == "0.0.0.0" && host == "0.0.0.0";

                entries.Add(new HostEntry
                {
                    LineIndex  = i,
                    IpAddress  = ip,
                    Hostname   = host,
                    Comment    = comment,
                    IsEnabled  = enabled,
                    IsReadOnly = isSystem,
                    Source     = HostEntrySource.Manual
                });
            }
        }, ct);

        LastReport = new HostsReport { Entries = entries, FilePath = HostsPath, IsAdmin = isAdmin };
        return new ScanResult(Id, true, entries.Count, 0);
    }

    public async Task<OptimizationResult> OptimizeAsync(
        OptimizationPlan plan, IProgress<TaskProgress>? progress = null, CancellationToken ct = default)
    {
        // plan.SelectedItemIds: "add:ip:hostname:comment", "delete:ip:hostname",
        //                       "enable:ip:hostname", "disable:ip:hostname", "save" (flush current report)
        int done = 0;
        var start = DateTime.UtcNow;
        var ids = plan.SelectedItemIds ?? new List<string>();

        await Task.Run(() =>
        {
            foreach (var id in ids)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var parts  = id.Split(':');
                    var action = parts[0].ToLower();

                    switch (action)
                    {
                        case "add" when parts.Length >= 3:
                            AddEntry(parts[1], parts[2], parts.Length > 3 ? parts[3] : "");
                            break;
                        case "delete" when parts.Length >= 3:
                            DeleteEntry(parts[1], parts[2]);
                            break;
                        case "enable" when parts.Length >= 3:
                            SetEnabled(parts[1], parts[2], true);
                            break;
                        case "disable" when parts.Length >= 3:
                            SetEnabled(parts[1], parts[2], false);
                            break;
                        case "save":
                            SaveHostsFile();
                            break;
                    }
                    done++;
                }
                catch { }
            }

            // Always flush to disk after operations
            SaveHostsFile();
        }, ct);

        return new OptimizationResult(Id, Guid.NewGuid().ToString(), true, done, 0, DateTime.UtcNow - start);
    }

    public Task<bool> CanRollbackAsync(string opId, CancellationToken ct = default) => Task.FromResult(true);

    public Task RollbackAsync(string opId, CancellationToken ct = default)
    {
        // opId contains backup path
        if (File.Exists(opId))
            File.Copy(opId, HostsPath, overwrite: true);
        return Task.CompletedTask;
    }

    // ── Public API for UI ─────────────────────────────────────

    public string CreateBackup()
    {
        var bak = Path.Combine(Path.GetTempPath(), $"hosts_backup_{DateTime.Now:yyyyMMdd_HHmmss}.bak");
        File.Copy(HostsPath, bak, overwrite: true);
        return bak;
    }

    public void AddEntry(string ip, string hostname, string comment = "")
    {
        LastReport?.Entries.Add(new HostEntry
        {
            IpAddress = ip, Hostname = hostname, Comment = comment,
            IsEnabled = true, Source = HostEntrySource.Manual
        });
    }

    public void DeleteEntry(string ip, string hostname)
    {
        var entry = LastReport?.Entries.FirstOrDefault(
            e => e.IpAddress == ip && e.Hostname.Equals(hostname, StringComparison.OrdinalIgnoreCase));
        if (entry != null && !entry.IsReadOnly)
            LastReport?.Entries.Remove(entry);
    }

    public void SetEnabled(string ip, string hostname, bool enabled)
    {
        var entry = LastReport?.Entries.FirstOrDefault(
            e => e.IpAddress == ip && e.Hostname.Equals(hostname, StringComparison.OrdinalIgnoreCase));
        if (entry != null && !entry.IsReadOnly)
            entry.IsEnabled = enabled;
    }

    public void SaveHostsFile()
    {
        if (LastReport is null) return;
        var lines = new List<string>
        {
            "# Managed by AuraCore Pro - " + DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
            "# DO NOT EDIT MANUALLY WHILE AURACORE IS OPEN",
            ""
        };

        foreach (var e in LastReport.Entries)
        {
            var comment = string.IsNullOrEmpty(e.Comment) ? "" : $" # {e.Comment}";
            var line    = $"{e.IpAddress}\t{e.Hostname}{comment}";
            lines.Add(e.IsEnabled ? line : $"# {line}");
        }

        File.WriteAllLines(HostsPath, lines);
    }

    public Task ImportBlockListAsync(string url, CancellationToken ct = default)
        => Task.Run(async () =>
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            var text = await http.GetStringAsync(url, ct);
            int added = 0;
            foreach (var line in text.Split('\n'))
            {
                var l = line.Trim();
                if (l.StartsWith('#') || string.IsNullOrEmpty(l)) continue;
                var parts = l.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2) continue;
                var ip = parts[0]; var host = parts[1];
                if (LastReport?.Entries.Any(e => e.Hostname.Equals(host, StringComparison.OrdinalIgnoreCase)) == true) continue;
                AddEntry(ip, host, "Imported from block list");
                added++;
                if (added >= 5000) break; // safety cap
            }
        }, ct);

    private static bool IsRunningAsAdmin()
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
                var principal = new System.Security.Principal.WindowsPrincipal(identity);
                return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
            }
            else
            {
                // Linux/macOS: root = uid 0
                return Environment.UserName == "root" || getuid() == 0;
            }
        }
        catch { return false; }
    }

    // P/Invoke for Unix uid check
    [System.Runtime.InteropServices.DllImport("libc")]
    private static extern uint getuid();
}

public static class HostsEditorRegistration
{
    public static IServiceCollection AddHostsEditorModule(this IServiceCollection services)
        => services.AddSingleton<IOptimizationModule, HostsEditorModule>();
}
