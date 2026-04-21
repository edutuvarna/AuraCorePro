using AuraCore.API.Domain.Entities;

namespace AuraCore.API.Application.Services.Releases;

public interface IGitHubReleaseMirror
{
    Task<string?> MirrorAsync(AppUpdate update, string r2ObjectKey, CancellationToken ct);
}
