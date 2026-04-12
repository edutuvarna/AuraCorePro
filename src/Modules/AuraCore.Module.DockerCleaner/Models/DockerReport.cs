namespace AuraCore.Module.DockerCleaner.Models;

public sealed record DockerReport(
    bool DockerAvailable,
    string DockerVersion,
    int TotalContainers,
    int StoppedContainers,
    int DanglingImages,
    int UnusedVolumes,
    long ImagesTotalBytes,
    long VolumesTotalBytes,
    long BuildCacheBytes,
    long TotalReclaimableBytes)
{
    public static DockerReport None() => new(false, "", 0, 0, 0, 0, 0, 0, 0, 0);
}
