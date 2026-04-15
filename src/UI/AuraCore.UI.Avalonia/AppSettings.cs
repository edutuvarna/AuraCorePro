using System.Text.Json;

namespace AuraCore.UI.Avalonia;

/// <summary>
/// Application-level user preferences persisted to disk.
/// Phase 3 extensions: AI feature toggles, chat opt-in state, active model tracking, learning-day anchor.
/// </summary>
public class AppSettings
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AuraCorePro", "app_settings.json");

    // Phase 3 — AI feature toggles
    public bool InsightsEnabled { get; set; } = true;
    public bool RecommendationsEnabled { get; set; } = true;
    public bool ScheduleEnabled { get; set; } = true;
    public bool ChatEnabled { get; set; } = false; // opt-in

    // Phase 3 — Chat opt-in flow state
    public bool ChatOptInAcknowledged { get; set; } = false;
    public string? ActiveChatModelId { get; set; } = null;

    // Phase 3 — Learning-day anchor for CORTEX status chip
    public DateTime? AIFirstEnabledAt { get; set; } = null;

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var json = File.ReadAllText(FilePath);
                return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
        }
        catch
        {
            // Corruption → safe defaults
        }
        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(FilePath);
            if (dir != null) Directory.CreateDirectory(dir);
            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(FilePath, json);
        }
        catch
        {
            // Best-effort persistence; log in real impl
        }
    }
}
