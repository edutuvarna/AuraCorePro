namespace AuraCore.Module.MacAppInstaller.Models;

public sealed record MacBundleApp
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required MacPackageSource Source { get; init; }
    public required string PackageName { get; init; }
    public bool IsInstalled { get; set; }  // mutable — set during scan
}
