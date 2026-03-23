namespace AuraCore.API.Domain.Entities;

public sealed class License
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Tier { get; set; } = "free";
    public string Status { get; set; } = "active";
    public int MaxDevices { get; set; } = 1;
    public DateTimeOffset? ExpiresAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public User User { get; set; } = null!;
    public ICollection<Device> Devices { get; set; } = new List<Device>();
}
