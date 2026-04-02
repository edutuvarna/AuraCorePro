namespace AuraCore.API.Domain.Entities;

public sealed class IpWhitelist
{
    public Guid Id { get; set; }
    public string IpAddress { get; set; } = string.Empty;
    public string? Label { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
