namespace AuraCore.Module.HostsEditor.Models;

public sealed class HostEntry
{
    public int    LineIndex  { get; set; }
    public string IpAddress  { get; set; } = "";
    public string Hostname   { get; set; } = "";
    public string Comment    { get; set; } = "";
    public bool   IsEnabled  { get; set; } = true;
    public bool   IsReadOnly { get; set; } = false; // localhost/::1
    public HostEntrySource Source { get; set; } = HostEntrySource.Manual;
}

public enum HostEntrySource { Manual, Imported, System }

public sealed class HostsReport
{
    public List<HostEntry> Entries { get; set; } = new();
    public string FilePath         { get; set; } = "";
    public bool   IsAdmin          { get; set; }
    public int    EnabledCount     => Entries.Count(e => e.IsEnabled && !e.IsReadOnly);
    public int    DisabledCount    => Entries.Count(e => !e.IsEnabled);
}
