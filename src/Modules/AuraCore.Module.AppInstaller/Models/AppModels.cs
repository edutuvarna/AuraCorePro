namespace AuraCore.Module.AppInstaller.Models;

public sealed record WinGetApp
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Version { get; init; } = "";
    public string Publisher { get; init; } = "";
    public string Description { get; init; } = "";
    public bool IsInstalled { get; init; }
}

public sealed record AppBundle
{
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public string Icon { get; init; } = "";
    public List<BundleApp> Apps { get; init; } = new();
}

public sealed record BundleApp
{
    public string WinGetId { get; init; } = "";
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public bool IsInstalled { get; set; }
}

public sealed record InstalledApp
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Version { get; init; } = "";
    public string Publisher { get; init; } = "";
}

public sealed record AppInstallerReport
{
    public List<InstalledApp> InstalledApps { get; init; } = new();
    public List<AppBundle> Bundles { get; init; } = new();
    public bool WinGetAvailable { get; init; }
}

public sealed record OutdatedApp
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string CurrentVersion { get; init; } = "";
    public string AvailableVersion { get; init; } = "";
}

public sealed record AppListExport
{
    public string ExportDate { get; init; } = "";
    public string MachineName { get; init; } = "";
    public int AppCount { get; init; }
    public List<AppListExportEntry> Apps { get; init; } = new();
}

public sealed record AppListExportEntry
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Version { get; init; } = "";
    public string Publisher { get; init; } = "";
}
