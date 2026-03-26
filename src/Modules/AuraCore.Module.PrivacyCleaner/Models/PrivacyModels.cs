namespace AuraCore.Module.PrivacyCleaner.Models;

public sealed record PrivacyItem(
    string FullPath,
    long SizeBytes,
    string Category,
    DateTimeOffset LastModified)
{
    public string SizeDisplay => SizeBytes switch
    {
        < 1024 => $"{SizeBytes} B",
        < 1024 * 1024 => $"{SizeBytes / 1024.0:F1} KB",
        < 1024L * 1024 * 1024 => $"{SizeBytes / (1024.0 * 1024):F1} MB",
        _ => $"{SizeBytes / (1024.0 * 1024 * 1024):F2} GB"
    };
}

public sealed record PrivacyScanReport
{
    public List<PrivacyCategory> Categories { get; init; } = new();
    public int TotalItems => Categories.Sum(c => c.Items.Count);
    public long TotalBytes => Categories.Sum(c => c.TotalBytes);

    public string TotalSizeDisplay => TotalBytes switch
    {
        < 1024 => $"{TotalBytes} B",
        < 1024 * 1024 => $"{TotalBytes / 1024.0:F1} KB",
        < 1024L * 1024 * 1024 => $"{TotalBytes / (1024.0 * 1024):F1} MB",
        _ => $"{TotalBytes / (1024.0 * 1024 * 1024):F2} GB"
    };
}

public sealed record PrivacyCategory
{
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public string Icon { get; init; } = "";
    public string RiskLevel { get; init; } = "Safe";
    public List<PrivacyItem> Items { get; init; } = new();
    public long TotalBytes => Items.Sum(f => f.SizeBytes);
    public int ItemCount => Items.Count;

    public string TotalSizeDisplay => TotalBytes switch
    {
        < 1024 => $"{TotalBytes} B",
        < 1024 * 1024 => $"{TotalBytes / 1024.0:F1} KB",
        < 1024L * 1024 * 1024 => $"{TotalBytes / (1024.0 * 1024):F1} MB",
        _ => $"{TotalBytes / (1024.0 * 1024 * 1024):F2} GB"
    };
}
