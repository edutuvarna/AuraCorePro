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
    long TotalReclaimableBytes,
    // Phase 4.3.3 additive: per-category reclaimable breakdown.
    // Lets the UI compute "Safe Cleanup" savings (images + containers + cache, excluding volumes)
    // without re-parsing the `docker system df` output. TotalReclaimableBytes stays as the sum
    // so external callers that only read that field continue to work.
    long ImagesReclaimableBytes = 0,
    long ContainersReclaimableBytes = 0,
    long VolumesReclaimableBytes = 0,
    long BuildCacheReclaimableBytes = 0)
{
    public static DockerReport None() => new(false, "", 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
}
