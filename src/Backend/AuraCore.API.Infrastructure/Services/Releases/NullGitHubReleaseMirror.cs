using AuraCore.API.Application.Services.Releases;
using AuraCore.API.Domain.Entities;

namespace AuraCore.API.Infrastructure.Services.Releases;

/// <summary>
/// No-op implementation used until OctokitReleaseMirror is wired in sub-phase 6.6.D.
/// Returns null (no release ID) so the AppUpdate row is saved with GitHubReleaseId = null.
/// </summary>
public sealed class NullGitHubReleaseMirror : IGitHubReleaseMirror
{
    public Task<string?> MirrorAsync(AppUpdate update, string r2ObjectKey, CancellationToken ct)
        => Task.FromResult<string?>(null);
}
