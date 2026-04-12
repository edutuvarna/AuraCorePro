namespace AuraCore.Module.SwapOptimizer.Models;

public sealed record SwapReport(
    int CurrentSwappiness,
    int RecommendedSwappiness,
    IReadOnlyList<SwapDeviceInfo> Devices,
    long TotalSwapBytes,
    long UsedSwapBytes,
    bool ZramAvailable,
    bool ZramEnabled,
    long RamBytes,
    bool IsAvailable)
{
    public static SwapReport None() => new(0, 0, Array.Empty<SwapDeviceInfo>(), 0, 0, false, false, 0, false);
}
