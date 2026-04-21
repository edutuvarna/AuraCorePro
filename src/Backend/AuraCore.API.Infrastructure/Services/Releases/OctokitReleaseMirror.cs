using AuraCore.API.Application.Services.Releases;
using AuraCore.API.Domain.Entities;
using Octokit;

namespace AuraCore.API.Infrastructure.Services.Releases;

public sealed class OctokitReleaseMirror : IGitHubReleaseMirror
{
    private const string RepoOwner = "edutuvarna";
    private const string RepoName = "AuraCorePro";

    private readonly IR2Client _r2;
    private readonly Func<string, IGitHubClient> _clientFactory;

    private readonly object _clientCacheLock = new();
    private string? _cachedToken;
    private IGitHubClient? _cachedClient;

    public OctokitReleaseMirror(IR2Client r2)
        : this(r2, token => new GitHubClient(new ProductHeaderValue("AuraCorePro-Backend")) {
            Credentials = new Credentials(token)
        }) { }

    // Test-friendly ctor
    public OctokitReleaseMirror(IR2Client r2, Func<string, IGitHubClient> githubClientFactory)
    {
        _r2 = r2;
        _clientFactory = githubClientFactory;
    }

    private IGitHubClient GetClient(string token)
    {
        lock (_clientCacheLock)
        {
            if (_cachedClient is null || !string.Equals(_cachedToken, token, StringComparison.Ordinal))
            {
                _cachedClient = _clientFactory(token);
                _cachedToken = token;
            }
            return _cachedClient;
        }
    }

    public async Task<string?> MirrorAsync(AppUpdate update, string r2ObjectKey, CancellationToken ct)
    {
        var token = Environment.GetEnvironmentVariable("ASPNETCORE_GITHUB_TOKEN");
        if (string.IsNullOrWhiteSpace(token))
            return null;

        var gh = GetClient(token);
        var tag = $"v{update.Version}";

        // Find-or-create release for this tag
        Release release;
        try
        {
            release = await gh.Repository.Release.Get(RepoOwner, RepoName, tag);
        }
        catch (NotFoundException)
        {
            try
            {
                release = await gh.Repository.Release.Create(RepoOwner, RepoName, new NewRelease(tag)
                {
                    Name = $"AuraCore Pro v{update.Version}",
                    Body = update.ReleaseNotes ?? "See AuraCore Pro changelog.",
                    Draft = false,
                    Prerelease = update.Channel != "stable",
                });
            }
            catch (ApiValidationException)
            {
                // Race: another platform's publish created this tag between our Get and Create.
                // Fetch the now-existing release and continue with asset uploads.
                release = await gh.Repository.Release.Get(RepoOwner, RepoName, tag);
            }
        }

        // Upload the binary asset
        // TODO(perf): stream directly from R2 to Octokit without MemoryStream buffering — at 500MB binaries this allocates fully on managed heap per call
        await using var binaryStream = new MemoryStream();
        await _r2.DownloadToStreamAsync(r2ObjectKey, binaryStream, ct);
        binaryStream.Position = 0;
        var canonicalName = Path.GetFileName(r2ObjectKey);
        await gh.Repository.Release.UploadAsset(release, new ReleaseAssetUpload
        {
            FileName = canonicalName,
            ContentType = GuessContentType(canonicalName),
            RawData = binaryStream,
        }, ct);

        // Upload sha256sums-<platform>.txt
        var sha256Content = $"{update.SignatureHash}  {canonicalName}\n";
        await using var sumsStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(sha256Content));
        await gh.Repository.Release.UploadAsset(release, new ReleaseAssetUpload
        {
            FileName = $"sha256sums-{update.Platform.ToString().ToLowerInvariant()}.txt",
            ContentType = "text/plain",
            RawData = sumsStream,
        }, ct);

        return release.Id.ToString();
    }

    private static string GuessContentType(string filename)
    {
        var ext = Path.GetExtension(filename).ToLowerInvariant();
        return ext switch
        {
            ".exe" or ".msi" => "application/x-msdownload",
            ".deb" => "application/vnd.debian.binary-package",
            ".rpm" => "application/x-rpm",
            ".dmg" => "application/x-apple-diskimage",
            ".pkg" => "application/x-newton-compatible-pkg",
            _ => "application/octet-stream"
        };
    }
}
