using Amazon.S3;
using Amazon.S3.Model;
using AuraCore.API.Application.Services.Releases;
using AuraCore.API.Infrastructure.Services.Releases;
using Moq;
using Xunit;

namespace AuraCore.Tests.API.Releases;

public class R2ClientTests
{
    private static AwsR2Client BuildClient(Mock<IAmazonS3> s3)
        => new AwsR2Client(s3.Object, bucketName: "auracore-releases");

    [Fact]
    public async Task GeneratePresignedPutUrlAsync_returns_url_and_object_key()
    {
        var s3 = new Mock<IAmazonS3>();
        s3.Setup(x => x.GetPreSignedURLAsync(It.IsAny<GetPreSignedUrlRequest>()))
          .ReturnsAsync("https://auracore-releases.r2.cloudflarestorage.com/pending/abc-installer.exe?X-Amz-Signature=xyz");

        var client = BuildClient(s3);
        var result = await client.GeneratePresignedPutUrlAsync(
            "pending/abc-installer.exe", TimeSpan.FromMinutes(10), 500_000_000, CancellationToken.None);

        Assert.StartsWith("https://", result.UploadUrl);
        Assert.Equal("pending/abc-installer.exe", result.ObjectKey);
        Assert.True(result.ExpiresAt > DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task HeadObjectAsync_returns_null_when_object_missing()
    {
        var s3 = new Mock<IAmazonS3>();
        s3.Setup(x => x.GetObjectMetadataAsync(It.IsAny<GetObjectMetadataRequest>(), It.IsAny<CancellationToken>()))
          .ThrowsAsync(new AmazonS3Exception("Not Found") { StatusCode = System.Net.HttpStatusCode.NotFound });

        var client = BuildClient(s3);
        var head = await client.HeadObjectAsync("pending/missing.exe", CancellationToken.None);
        Assert.Null(head);
    }

    [Fact]
    public async Task HeadObjectAsync_returns_size_and_timestamp_when_exists()
    {
        var lm = new DateTime(2026, 4, 20, 12, 0, 0, DateTimeKind.Utc);
        var s3 = new Mock<IAmazonS3>();
        s3.Setup(x => x.GetObjectMetadataAsync(It.IsAny<GetObjectMetadataRequest>(), It.IsAny<CancellationToken>()))
          .ReturnsAsync(new GetObjectMetadataResponse {
              ContentLength = 52_428_800,  // 50 MB
              LastModified = lm,
              Headers = { ContentType = "application/octet-stream" }
          });

        var client = BuildClient(s3);
        var head = await client.HeadObjectAsync("releases/v1.7.0/AuraCorePro-Windows-v1.7.0.exe", CancellationToken.None);
        Assert.NotNull(head);
        Assert.Equal(52_428_800, head!.SizeBytes);
        Assert.Equal("application/octet-stream", head.ContentType);
    }

    [Fact]
    public async Task ComputeSha256Async_returns_64_char_lowercase_hex()
    {
        var payload = System.Text.Encoding.UTF8.GetBytes("test-installer-content");
        var expectedHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(payload)).ToLowerInvariant();

        var s3 = new Mock<IAmazonS3>();
        s3.Setup(x => x.GetObjectAsync(It.IsAny<GetObjectRequest>(), It.IsAny<CancellationToken>()))
          .ReturnsAsync(new GetObjectResponse { ResponseStream = new MemoryStream(payload) });

        var client = BuildClient(s3);
        var hash = await client.ComputeSha256Async("releases/v1.7.0/x.exe", CancellationToken.None);
        Assert.Equal(expectedHash, hash);
        Assert.Equal(64, hash.Length);
    }

    [Fact]
    public async Task CopyObjectAsync_maps_source_and_destination_keys()
    {
        var s3 = new Mock<IAmazonS3>();
        CopyObjectRequest? captured = null;
        s3.Setup(x => x.CopyObjectAsync(It.IsAny<CopyObjectRequest>(), It.IsAny<CancellationToken>()))
          .Callback<CopyObjectRequest, CancellationToken>((r, _) => captured = r)
          .ReturnsAsync(new CopyObjectResponse());

        var client = BuildClient(s3);
        await client.CopyObjectAsync("pending/abc.exe", "releases/v1.7.0/x.exe", CancellationToken.None);

        Assert.NotNull(captured);
        Assert.Equal("auracore-releases", captured!.SourceBucket);
        Assert.Equal("pending/abc.exe", captured.SourceKey);
        Assert.Equal("auracore-releases", captured.DestinationBucket);
        Assert.Equal("releases/v1.7.0/x.exe", captured.DestinationKey);
    }
}
