namespace AuraCore.Module.SwapOptimizer.Models;

public sealed record SwapDeviceInfo(
    string Path,
    string Type,           // "partition", "file", "zram"
    long SizeBytes,
    long UsedBytes,
    int Priority);
