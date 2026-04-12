namespace AuraCore.Module.MacAppInstaller.Models;

public sealed record MacAppBundle
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string Icon { get; init; }
    public required List<MacBundleApp> Apps { get; init; }
}
