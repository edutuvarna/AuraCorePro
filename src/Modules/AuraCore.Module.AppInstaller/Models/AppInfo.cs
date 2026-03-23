namespace AuraCore.Module.AppInstaller.Models;

public sealed record AppInfo
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Version { get; init; } = "";
    public string AvailableVersion { get; init; } = "";
    public string Source { get; init; } = "";
    public bool HasUpdate => !string.IsNullOrEmpty(AvailableVersion) && AvailableVersion != Version;
}

public sealed record AppSearchResult
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Version { get; init; } = "";
    public string Source { get; init; } = "";
    public string Match { get; init; } = "";
}

public sealed record InstalledAppsReport
{
    public List<AppInfo> Apps { get; init; } = new();
    public int TotalInstalled => Apps.Count;
    public int UpdatesAvailable => Apps.Count(a => a.HasUpdate);
}
