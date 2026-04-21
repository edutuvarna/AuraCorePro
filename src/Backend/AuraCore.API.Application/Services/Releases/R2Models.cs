namespace AuraCore.API.Application.Services.Releases;

public sealed record R2ObjectHead(long SizeBytes, DateTimeOffset LastModified, string? ContentType);

public sealed record PresignedPutResult(string UploadUrl, string ObjectKey, DateTimeOffset ExpiresAt);
