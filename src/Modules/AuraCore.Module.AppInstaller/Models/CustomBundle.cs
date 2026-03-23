using System.Text.Json;

namespace AuraCore.Module.AppInstaller.Models;

/// <summary>
/// User-created custom app bundles. Stored as JSON in LocalAppData.
/// </summary>
public sealed record CustomBundle
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N")[..8];
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public string Icon { get; init; } = "E74C"; // default: package icon
    public List<CustomBundleApp> Apps { get; init; } = new();
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record CustomBundleApp
{
    public string WinGetId { get; init; } = "";
    public string Name { get; init; } = "";
}

public static class CustomBundleStore
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AuraCorePro", "custom_bundles.json");

    public static List<CustomBundle> Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<List<CustomBundle>>(File.ReadAllText(FilePath)) ?? new();
        }
        catch { }
        return new();
    }

    public static void Save(List<CustomBundle> bundles)
    {
        try
        {
            var dir = Path.GetDirectoryName(FilePath)!;
            Directory.CreateDirectory(dir);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(bundles, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }

    public static void Add(CustomBundle bundle)
    {
        var list = Load();
        list.Add(bundle);
        Save(list);
    }

    public static void Remove(string bundleId)
    {
        var list = Load();
        list.RemoveAll(b => b.Id == bundleId);
        Save(list);
    }
}
