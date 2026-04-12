namespace AuraCore.Module.JournalCleaner.Models;

public sealed record JournalReport(
    long CurrentBytes,
    DateTime? OldestEntry,
    int JournalFileCount,
    long RecommendedLimit,
    bool IsAvailable)
{
    public static JournalReport None() => new(0, null, 0, 0, false);
}
