using System.Diagnostics;
using System.Text.Json;
using AuraCore.Application.Interfaces.Modules;
using global::Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;

namespace AuraCore.UI.Avalonia;

/// <summary>
/// Background service that monitors for known game processes and
/// automatically activates/deactivates Gaming Mode.
/// Cross-platform port from WinUI3 — uses Avalonia Dispatcher.
/// </summary>
public sealed class GameWatcher : IDisposable
{
    private Timer? _timer;
    private bool _isEnabled;
    private bool _gameDetected;
    private string? _detectedGameName;

    private static readonly string ConfigPath = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AuraCorePro", "gamewatcher.json");

    private static readonly HashSet<string> KnownGames = new(StringComparer.OrdinalIgnoreCase)
    {
        "steam", "csgo", "cs2", "dota2", "hl2", "left4dead2",
        "fortniteClient-win64-shipping", "rocketleague",
        "league of legends", "leagueclient", "valorant", "valorant-win64-shipping",
        "wow", "overwatch", "diablo iv", "hearthstone",
        "fifa", "fc24", "fc25", "apexlegends", "battlefield",
        "minecraft", "javaw",
        "gta5", "gtav", "rdr2", "cyberpunk2077",
        "eldenring", "darksouls", "sekiro",
        "baldursgate3", "bg3", "hogwartslegacy",
        "terraria", "stardewvalley", "witcher3",
        "halo", "haloinfinite",
        "cod", "moderwarfare", "blackops",
        "pubg", "rust", "ark",
        "satisfactory", "factorio",
        "totalwar", "civilization", "civ6",
        "flightsimulator",
        "godofwar", "horizonzerodawn",
        "spiderman", "ghostoftsushima",
    };

    private HashSet<string> _customGames = new(StringComparer.OrdinalIgnoreCase);

    public bool IsEnabled => _isEnabled;
    public bool IsGameDetected => _gameDetected;
    public string? DetectedGameName => _detectedGameName;

    public event Action<string>? GameDetected;
    public event Action? GameExited;

    public GameWatcher()
    {
        LoadConfig();
    }

    public void Start()
    {
        if (_timer is not null) return;
        _isEnabled = true;
        _timer = new Timer(CheckForGames, null, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5));
        SaveConfig();
    }

    public void Stop()
    {
        _timer?.Dispose();
        _timer = null;
        _isEnabled = false;

        if (_gameDetected)
        {
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
                    if (allGames.Contains(proc.ProcessName))
                    {
                        foundGame = proc.ProcessName;
                        break;
                    }
                }
                catch { }
                finally { try { proc.Dispose(); } catch { } }
            }

            if (foundGame is not null && !_gameDetected)
            {
                _gameDetected = true;
                _detectedGameName = foundGame;
                Dispatcher.UIThread.Post(() =>
                {
                    GameDetected?.Invoke(foundGame);
                    NotificationService.Instance.Post(
                        "Gaming Mode",
                        $"Game detected: {foundGame} - Gaming Mode activated",
                        NotificationType.Success, "gaming-mode");
                });
            }
            else if (foundGame is null && _gameDetected)
            {
                var lastGame = _detectedGameName;
                _gameDetected = false;
                _detectedGameName = null;
                Dispatcher.UIThread.Post(() =>
                {
                    GameExited?.Invoke();
                    NotificationService.Instance.Post(
                        "Gaming Mode",
                        $"{lastGame} closed - Gaming Mode deactivated",
                        NotificationType.Info, "gaming-mode");
                });
            }
        }
        catch { }
    }

    private void SaveConfig()
    {
        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(ConfigPath)!);
            var config = new GameWatcherConfig { Enabled = _isEnabled, CustomGames = _customGames.ToList() };
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
