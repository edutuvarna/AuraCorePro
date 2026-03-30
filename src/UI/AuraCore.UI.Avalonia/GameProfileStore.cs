using System.Text.Json;

namespace AuraCore.UI.Avalonia;

/// <summary>
/// Persists per-game profiles. Each profile maps game process names to optimization settings.
/// </summary>
public static class GameProfileStore
{
    private static readonly string FilePath = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AuraCorePro", "game_profiles.json");

    private static List<GameProfileEntry> _profiles = new();

    public sealed record GameProfileEntry
    {
        public string Id { get; init; } = Guid.NewGuid().ToString()[..8];
        public string ProfileName { get; init; } = "Default";
        public List<string> GameProcesses { get; init; } = new();
        public bool SwitchPowerPlan { get; init; } = true;
        public bool DisableNotifications { get; init; } = true;
        public bool SuspendBackground { get; init; } = true;
        public bool BoostPriority { get; init; } = true;
        public bool CleanRam { get; init; } = true;
        public List<string> NeverSuspend { get; init; } = new();
    }

    public static List<GameProfileEntry> GetAll()
    {
        if (_profiles.Count == 0) Load();
        return _profiles.ToList();
    }

    public static GameProfileEntry? FindForGame(string processName)
    {
        if (_profiles.Count == 0) Load();
        return _profiles.FirstOrDefault(p =>
            p.GameProcesses.Any(g => g.Equals(processName, StringComparison.OrdinalIgnoreCase)));
    }

    public static void Add(GameProfileEntry profile)
    {
        _profiles.Add(profile);
        Save();
    }

    public static void Update(string id, GameProfileEntry updated)
    {
        var idx = _profiles.FindIndex(p => p.Id == id);
        if (idx >= 0) _profiles[idx] = updated;
        Save();
    }

    public static void Delete(string id)
    {
        _profiles.RemoveAll(p => p.Id == id);
        Save();
    }

    public static void Save()
    {
        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(_profiles, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }

    public static void Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return;
            _profiles = JsonSerializer.Deserialize<List<GameProfileEntry>>(File.ReadAllText(FilePath)) ?? new();
        }
        catch { _profiles = new(); }
    }
}
