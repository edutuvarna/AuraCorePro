namespace AuraCore.Module.LinuxAppInstaller.Models;

public sealed record LinuxBundleApp
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required LinuxPackageSource Source { get; init; }
    public required string PackageName { get; init; }
    public bool IsInstalled { get; set; }  // mutable — set during scan
}
