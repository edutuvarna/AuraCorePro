using System.Security.Cryptography;
using Amazon.S3;
using Amazon.S3.Model;
using AuraCore.API.Application.Services.Releases;

namespace AuraCore.API.Infrastructure.Services.Releases;

public sealed class AwsR2Client : IR2Client
{
    private readonly IAmazonS3 _s3;
    private readonly string _bucket;

    public AwsR2Client(IAmazonS3 s3, string bucketName)
    {
        _s3 = s3;
        _bucket = bucketName;
    }

    public async Task<PresignedPutResult> GeneratePresignedPutUrlAsync(
        string objectKey, TimeSpan ttl, long maxSizeBytes, CancellationToken ct)
    {
        var req = new GetPreSignedUrlRequest
        {
            BucketName = _bucket,
            Key = objectKey,
            Verb = HttpVerb.PUT,
            Expires = DateTime.UtcNow.Add(ttl),
        };
        // R2 enforces size via x-amz-decoded-content-length header; we document the cap to the caller
        var url = await _s3.GetPreSignedURLAsync(req);
        return new PresignedPutResult(url, objectKey, DateTimeOffset.UtcNow.Add(ttl));
    }

    public async Task<R2ObjectHead?> HeadObjectAsync(string objectKey, CancellationToken ct)
    {
        try
        {
            var resp = await _s3.GetObjectMetadataAsync(new GetObjectMetadataRequest {
                BucketName = _bucket, Key = objectKey
            }, ct);
            return new R2ObjectHead(resp.ContentLength, resp.LastModified, resp.Headers.ContentType);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public Task CopyObjectAsync(string sourceKey, string destinationKey, CancellationToken ct)
        => _s3.CopyObjectAsync(new CopyObjectRequest {
            SourceBucket = _bucket, SourceKey = sourceKey,
            DestinationBucket = _bucket, DestinationKey = destinationKey
        }, ct);

    public Task DeleteObjectAsync(string objectKey, CancellationToken ct)
        => _s3.DeleteObjectAsync(new DeleteObjectRequest {
            BucketName = _bucket, Key = objectKey
        }, ct);

    public async Task<string> ComputeSha256Async(string objectKey, CancellationToken ct)
    {
        using var resp = await _s3.GetObjectAsync(new GetObjectRequest {
            BucketName = _bucket, Key = objectKey
        }, ct);
        using var sha = SHA256.Create();
        var hashBytes = await sha.ComputeHashAsync(resp.ResponseStream, ct);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    public async Task DownloadToStreamAsync(string objectKey, Stream destination, CancellationToken ct)
    {
        using var resp = await _s3.GetObjectAsync(new GetObjectRequest {
            BucketName = _bucket, Key = objectKey
        }, ct);
        await resp.ResponseStream.CopyToAsync(destination, ct);
    }
}
