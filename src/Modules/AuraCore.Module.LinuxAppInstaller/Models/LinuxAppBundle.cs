namespace AuraCore.Module.LinuxAppInstaller.Models;

public sealed record LinuxAppBundle
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string Icon { get; init; }
    public required List<LinuxBundleApp> Apps { get; init; }
}
