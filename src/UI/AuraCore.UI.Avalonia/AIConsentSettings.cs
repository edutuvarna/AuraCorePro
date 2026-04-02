using System.Text.Json;

namespace AuraCore.UI.Avalonia;

/// <summary>
/// Manages AI telemetry consent persistence.
/// Reads/writes consent state to %APPDATA%/AuraCorePro/ai_consent.json
/// </summary>
public static class AIConsentSettings
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "AuraCorePro", "ai_consent.json");

    private sealed class ConsentData
    {
        public bool Consent { get; set; }
        public string Timestamp { get; set; } = "";
        public string Version { get; set; } = "1.0";
    }

    /// <summary>Returns true if the consent dialog has already been shown (file exists).</summary>
    public static bool HasBeenShown()
    {
        try { return File.Exists(FilePath); }
        catch { return false; }
    }

    /// <summary>Returns true if user gave consent for anonymous AI telemetry.</summary>
    public static bool IsConsentGiven()
    {
        try
        {
            if (!File.Exists(FilePath)) return false;
            var json = File.ReadAllText(FilePath);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("consent", out var prop))
                return prop.GetBoolean();
            return false;
        }
        catch { return false; }
    }

    /// <summary>Saves consent choice and marks dialog as shown.</summary>
    public static void Save(bool consent)
    {
        try
        {
            var dir = Path.GetDirectoryName(FilePath)!;
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            var data = new
            {
                consent,
                timestamp = DateTime.UtcNow.ToString("o"),
                version = "1.0"
            };
            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(FilePath, json);
        }
        catch { /* Fail silently — non-critical setting */ }
    }
}
