namespace AuraCore.Module.KernelCleaner.Models;

public sealed record KernelInfo(
    string Version,
    string PackageName,
    bool IsCurrent,
    bool IsLatest,
    long SizeBytes,
    DateTime? InstalledDate);
