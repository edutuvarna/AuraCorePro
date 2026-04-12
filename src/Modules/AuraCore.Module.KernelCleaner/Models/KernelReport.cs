namespace AuraCore.Module.KernelCleaner.Models;

public sealed record KernelReport(
    IReadOnlyList<KernelInfo> Kernels,
    string CurrentKernel,
    long TotalRemovableBytes,
    string PackageManager,
    bool IsAvailable)
{
    public static KernelReport None() => new(Array.Empty<KernelInfo>(), "", 0, "none", false);
}
