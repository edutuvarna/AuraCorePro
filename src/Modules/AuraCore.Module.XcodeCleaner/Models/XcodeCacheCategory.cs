namespace AuraCore.Module.XcodeCleaner.Models;

public sealed record XcodeCacheCategory(
    string Id,
    string Name,
    string Path,
    long SizeBytes,
    int ItemCount,
    DateTime? OldestItem,
    bool Exists);
