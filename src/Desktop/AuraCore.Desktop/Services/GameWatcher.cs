using System.Diagnostics;
using System.Text.Json;
using AuraCore.Application;
using AuraCore.Application.Interfaces.Modules;
using AuraCore.Module.GamingMode;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;

namespace AuraCore.Desktop.Services;

/// <summary>
/// Background service that monitors for known game processes and
/// automatically activates/deactivates Gaming Mode.
/// </summary>
public sealed class GameWatcher : IDisposable
{
    private readonly IServiceProvider _services;
    private readonly DispatcherQueue _dispatcher;
    private Timer? _timer;
    private bool _isEnabled;
    private bool _gameDetected;
    private string? _detectedGameName;

    private static readonly string ConfigPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AuraCorePro", "gamewatcher.json");

    /// <summary>Well-known game executable names (lowercase, no .exe)</summary>
    private static readonly HashSet<string> KnownGames = new(StringComparer.OrdinalIgnoreCase)
    {
        // Launchers
        "steam", "steamservice",
        // Valve
        "csgo", "cs2", "dota2", "hl2", "left4dead2",
        // Epic / Fortnite
        "fortniteClient-win64-shipping", "fortniteclient-win64-shipping",
        "rocketleague",
        // Riot
        "league of legends", "leagueclient", "valorant", "valorant-win64-shipping",
        "riotclientservices",
        // Blizzard
        "wow", "overwatch", "diablo iv", "hearthstone",
        "battle.net",
        // EA
        "fifa", "fc24", "fc25", "apexlegends", "battlefield",
        "needforspeed", "thesims4",
        // Ubisoft
        "assassinscreed", "farcry", "r6-siege",
        // Indie / Popular
        "minecraft", "javaw", // Minecraft Java
        "gta5", "gtav", "rdr2", "cyberpunk2077",
        "eldenring", "darksouls", "sekiro",
        "baldursgate3", "bg3",
        "hogwartslegacy",
        "terraria", "stardewvalley",
        "witcher3",
        "halo", "haloinfinite",
        "cod", "moderwarfare", "blackops",
        "pubg", "pubgtslgame",
        "rust", "ark",
        "satisfactory", "factorio",
        "totalwar", "civilization", "civ6",
        "flightsimulator",
        "godofwar", "horizonzerodawn",
        "spiderman", "spidermanremastered",
        "ghostoftsushima",
        // General patterns
        "unrealengine", "unitycrashandler",
    };

    /// <summary>User-added custom game names</summary>
    private HashSet<string> _customGames = new(StringComparer.OrdinalIgnoreCase);

    public bool IsEnabled => _isEnabled;
    public bool IsGameDetected => _gameDetected;
    public string? DetectedGameName => _detectedGameName;

    public event Action<string>? GameDetected;
    public event Action? GameExited;

    public GameWatcher(IServiceProvider services, DispatcherQueue dispatcher)
    {
        _services = services;
        _dispatcher = dispatcher;
        LoadConfig();
    }

    public void Start()
    {
        if (_timer is not null) return;
        _isEnabled = true;
        // Check every 5 seconds
        _timer = new Timer(CheckForGames, null, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5));
        SaveConfig();
    }

    public void Stop()
    {
        _timer?.Dispose();
        _timer = null;
        _isEnabled = false;

        // Deactivate if a game was detected
        if (_gameDetected)
        {
            _ = DeactivateGamingModeAsync();
            _gameDetected = false;
            _detectedGameName = null;
        }
        SaveConfig();
    }

    public void AddCustomGame(string processName)
    {
        _customGames.Add(processName.ToLowerInvariant());
        SaveConfig();
    }

    public void RemoveCustomGame(string processName)
    {
        _customGames.Remove(processName.ToLowerInvariant());
        SaveConfig();
    }

    public IReadOnlySet<string> GetCustomGames() => _customGames;

    private void CheckForGames(object? state)
    {
        try
        {
            var allGames = new HashSet<string>(KnownGames, StringComparer.OrdinalIgnoreCase);
            foreach (var cg in _customGames) allGames.Add(cg);

            string? foundGame = null;
            foreach (var proc in Process.GetProcesses())
            {
                try
                {
                    var name = proc.ProcessName;
                    if (allGames.Contains(name))
                    {
                        foundGame = name;
                        break;
                    }
                }
                catch { }
                finally { try { proc.Dispose(); } catch { } }
            }

            if (foundGame is not null && !_gameDetected)
            {
                // Game just launched
                _gameDetected = true;
                _detectedGameName = foundGame;
                _dispatcher.TryEnqueue(async () =>
                {
                    await ActivateGamingModeAsync();
                    GameDetected?.Invoke(foundGame);
                    NotificationService.Instance.Post(
                        "Gaming Mode",
                        $"Game detected: {foundGame} — Gaming Mode activated",
                        NotificationType.Success, "gaming-mode");
                });
            }
            else if (foundGame is null && _gameDetected)
            {
                // Game exited
                _gameDetected = false;
                var lastGame = _detectedGameName;
                _detectedGameName = null;
                _dispatcher.TryEnqueue(async () =>
                {
                    await DeactivateGamingModeAsync();
                    GameExited?.Invoke();
                    NotificationService.Instance.Post(
                        "Gaming Mode",
                        $"{lastGame} closed — Gaming Mode deactivated",
                        NotificationType.Info, "gaming-mode");
                });
            }
        }
        catch { }
    }

    private async Task ActivateGamingModeAsync()
    {
        try
        {
            var modules = _services.GetServices<IOptimizationModule>();
            var module = modules.FirstOrDefault(m => m.Id == "gaming-mode") as GamingModeModule;
            if (module is null || module.IsActive) return;

            // Check for a game-specific profile
            var ids = new List<string> { "activate" };
            var profile = _detectedGameName is not null
                ? GameProfileStore.FindForGame(_detectedGameName) : null;

            if (profile is not null)
            {
                // Apply profile-specific settings
                if (profile.SwitchPowerPlan) ids.Add("power-plan");
                if (profile.DisableNotifications) ids.Add("notifications");
                if (profile.SuspendBackground) ids.Add("suspend-bg");
                if (profile.CleanRam) ids.Add("clean-ram");
                if (profile.BoostPriority) ids.Add("boost-priority");
            }
            else
            {
                // Default: enable everything
                ids.AddRange(new[] { "power-plan", "notifications", "suspend-bg", "clean-ram" });
            }

            var plan = new OptimizationPlan("gaming-mode", ids);
            await module.OptimizeAsync(plan);
        }
        catch { }
    }

    private async Task DeactivateGamingModeAsync()
    {
        try
        {
            var modules = _services.GetServices<IOptimizationModule>();
            var module = modules.FirstOrDefault(m => m.Id == "gaming-mode") as GamingModeModule;
            if (module is null || !module.IsActive) return;

            var plan = new OptimizationPlan("gaming-mode", new List<string> { "deactivate" });
            await module.OptimizeAsync(plan);
        }
        catch { }
    }

    private void SaveConfig()
    {
        try
        {
            var dir = Path.GetDirectoryName(ConfigPath)!;
            Directory.CreateDirectory(dir);
            var config = new GameWatcherConfig
            {
                Enabled = _isEnabled,
                CustomGames = _customGames.ToList()
            };
            File.WriteAllText(ConfigPath, JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }

    private void LoadConfig()
    {
        try
        {
            if (!File.Exists(ConfigPath)) return;
            var json = File.ReadAllText(ConfigPath);
            var config = JsonSerializer.Deserialize<GameWatcherConfig>(json);
            if (config is null) return;
            _customGames = new HashSet<string>(config.CustomGames, StringComparer.OrdinalIgnoreCase);
            if (config.Enabled) Start();
        }
        catch { }
    }

    public void Dispose() => _timer?.Dispose();

    private sealed record GameWatcherConfig
    {
        public bool Enabled { get; init; }
        public List<string> CustomGames { get; init; } = new();
    }
}
