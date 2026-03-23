using System.Text.Json;
using AuraCore.Module.GamingMode.Models;

namespace AuraCore.Desktop.Services;

/// <summary>
/// Persists per-game profiles to %LocalAppData%\AuraCorePro\game_profiles.json.
/// Each profile maps a game process name to specific optimization settings.
/// </summary>
public static class GameProfileStore
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AuraCorePro", "game_profiles.json");

    private static List<GameProfileEntry> _profiles = new();

    public sealed record GameProfileEntry
    {
        public string Id { get; init; } = Guid.NewGuid().ToString()[..8];
        public string ProfileName { get; init; } = "Default";
        public List<string> GameProcesses { get; init; } = new(); // e.g. ["cs2", "csgo"]
        public bool SwitchPowerPlan { get; init; } = true;
        public bool DisableNotifications { get; init; } = true;
        public bool SuspendBackground { get; init; } = true;
        public bool BoostPriority { get; init; } = true;
        public bool CleanRam { get; init; } = true;
        public List<string> NeverSuspend { get; init; } = new(); // keep these running
    }

    public static List<GameProfileEntry> GetAll()
    {
        if (_profiles.Count == 0) Load();
        return _profiles.ToList();
    }

    /// <summary>Find the best matching profile for a game process name.</summary>
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
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            var json = JsonSerializer.Serialize(_profiles, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(FilePath, json);
        }
        catch { }
    }

    public static void Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return;
            var json = File.ReadAllText(FilePath);
            _profiles = JsonSerializer.Deserialize<List<GameProfileEntry>>(json) ?? new();
        }
        catch { _profiles = new(); }
    }
}
