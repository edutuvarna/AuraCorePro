namespace AuraCore.Module.JunkCleaner.Models;

public sealed record JunkItem(
    string FullPath,
    long SizeBytes,
    string Category,
    DateTimeOffset LastModified)
{
    public string SizeDisplay => SizeBytes switch
    {
        < 1024 => $"{SizeBytes} B",
        < 1024 * 1024 => $"{SizeBytes / 1024.0:F1} KB",
        < 1024 * 1024 * 1024 => $"{SizeBytes / (1024.0 * 1024):F1} MB",
        _ => $"{SizeBytes / (1024.0 * 1024 * 1024):F2} GB"
    };
}

public sealed record JunkScanReport
{
    public List<JunkCategory> Categories { get; init; } = new();
    public int TotalFiles => Categories.Sum(c => c.Files.Count);
    public long TotalBytes => Categories.Sum(c => c.TotalBytes);

    public string TotalSizeDisplay => TotalBytes switch
    {
        < 1024 => $"{TotalBytes} B",
        < 1024 * 1024 => $"{TotalBytes / 1024.0:F1} KB",
        < 1024 * 1024 * 1024 => $"{TotalBytes / (1024.0 * 1024):F1} MB",
        _ => $"{TotalBytes / (1024.0 * 1024 * 1024):F2} GB"
    };
}

public sealed record JunkCategory
{
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public List<JunkItem> Files { get; init; } = new();
    public long TotalBytes => Files.Sum(f => f.SizeBytes);
    public int FileCount => Files.Count;

    public string TotalSizeDisplay => TotalBytes switch
    {
        < 1024 => $"{TotalBytes} B",
        < 1024 * 1024 => $"{TotalBytes / 1024.0:F1} KB",
        < 1024 * 1024 * 1024 => $"{TotalBytes / (1024.0 * 1024):F1} MB",
        _ => $"{TotalBytes / (1024.0 * 1024 * 1024):F2} GB"
    };
}
