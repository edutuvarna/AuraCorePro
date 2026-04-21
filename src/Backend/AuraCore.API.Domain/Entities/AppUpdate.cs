namespace AuraCore.API.Domain.Entities;

public sealed class AppUpdate
{
    public Guid Id { get; set; }
    public string Version { get; set; } = string.Empty;
    public string Channel { get; set; } = "stable";
    public AppUpdatePlatform Platform { get; set; } = AppUpdatePlatform.Windows;
    public string? ReleaseNotes { get; set; }
    public string BinaryUrl { get; set; } = string.Empty;
    public string SignatureHash { get; set; } = string.Empty;
    public bool IsMandatory { get; set; }
    public DateTimeOffset PublishedAt { get; set; }
    public string? GitHubReleaseId { get; set; }  // null until mirror completes
}
