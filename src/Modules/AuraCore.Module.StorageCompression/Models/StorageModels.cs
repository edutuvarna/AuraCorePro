namespace AuraCore.Module.StorageCompression.Models;

public sealed record StorageCompressionReport
{
    public List<CompressibleFolder> Folders { get; init; } = new();
    public long TotalCurrentBytes { get; init; }
    public long TotalSavingsEstimate { get; init; }
    public bool CompactOsEnabled { get; init; }
    public string CompactOsState { get; init; } = "";
    public StorageDriveType SystemDriveType { get; init; } = StorageDriveType.Unknown;
    public string DriveTypeWarning => SystemDriveType == StorageDriveType.HDD
        ? "Your system drive is an HDD. Compression may slightly slow down file access. SSD users see no performance impact."
        : "";

    public string CurrentSizeDisplay => FormatBytes(TotalCurrentBytes);
    public string SavingsDisplay => FormatBytes(TotalSavingsEstimate);

    private static string FormatBytes(long b) => b switch
    {
        < 1024 * 1024 => $"{b / 1024.0:F0} KB",
        < 1024L * 1024 * 1024 => $"{b / (1024.0 * 1024):F1} MB",
        _ => $"{b / (1024.0 * 1024 * 1024):F2} GB"
    };
}

public enum StorageDriveType
{
    Unknown,
    SSD,
    HDD
}

public sealed record CompressibleFolder
{
    public string Path { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public string Description { get; init; } = "";
    public long SizeBytes { get; init; }
    public long EstimatedSavings { get; init; }
    public string Risk { get; init; } = "Safe";
    public bool IsAlreadyCompressed { get; init; }
    public CompressionType RecommendedAlgorithm { get; init; } = CompressionType.XPRESS4K;

    public string SizeDisplay => SizeBytes switch
    {
        < 1024 * 1024 => $"{SizeBytes / 1024.0:F0} KB",
        < 1024L * 1024 * 1024 => $"{SizeBytes / (1024.0 * 1024):F1} MB",
        _ => $"{SizeBytes / (1024.0 * 1024 * 1024):F2} GB"
    };

    public string SavingsDisplay => EstimatedSavings switch
    {
        < 1024 * 1024 => $"{EstimatedSavings / 1024.0:F0} KB",
        < 1024L * 1024 * 1024 => $"{EstimatedSavings / (1024.0 * 1024):F1} MB",
        _ => $"{EstimatedSavings / (1024.0 * 1024 * 1024):F2} GB"
    };
}

public enum CompressionType
{
    XPRESS4K,       // Fastest, least compression (~15-25% savings)
    XPRESS8K,       // Good balance
    XPRESS16K,      // Better compression, slightly slower
    LZX             // Best compression (~40-60%), slowest read
}
