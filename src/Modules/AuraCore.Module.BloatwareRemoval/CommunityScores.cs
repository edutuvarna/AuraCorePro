namespace AuraCore.Module.BloatwareRemoval;

/// <summary>
/// Simulated community-sourced removal scores for common Windows apps.
/// Score: 0-100 (higher = more users recommend removal).
/// Votes: simulated community vote count.
/// In production, this would come from a backend API.
/// </summary>
internal static class CommunityScores
{
    internal sealed record Score(int RemovalScore, int Votes);

    internal static Score GetScore(string packageName)
    {
        if (string.IsNullOrEmpty(packageName)) return new(0, 0);

        // Match by partial name (package names are like "Microsoft.BingWeather")
        foreach (var (pattern, score) in Scores)
        {
            if (packageName.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                return score;
        }

        return new(0, 0); // Unknown app — no community data
    }

    private static readonly (string Pattern, Score Score)[] Scores =
    {
        // ── Strongly Recommended to Remove (85-98) ──
        ("BingFinance", new(95, 12840)),
        ("BingNews", new(94, 14200)),
        ("BingSports", new(96, 11300)),
        ("BingTranslator", new(92, 8700)),
        ("BingWeather", new(78, 15600)),   // some people like it
        ("BingFoodAndDrink", new(97, 6200)),
        ("BingHealthAndFitness", new(97, 5800)),
        ("BingTravel", new(96, 6900)),
        ("3DBuilder", new(93, 18400)),
        ("3DViewer", new(91, 16200)),
        ("MixedReality", new(95, 13700)),
        ("Microsoft.People", new(88, 11200)),
        ("Microsoft.Messaging", new(90, 9800)),
        ("Microsoft.GetHelp", new(87, 14100)),
        ("Microsoft.Getstarted", new(85, 12300)),     // Tips app
        ("Microsoft.MicrosoftOfficeHub", new(86, 13500)),
        ("Microsoft.MicrosoftSolitaireCollection", new(72, 19800)), // popular with casual users
        ("Microsoft.OneConnect", new(91, 7200)),
        ("Microsoft.Print3D", new(94, 10100)),
        ("Microsoft.SkypeApp", new(89, 17400)),
        ("Microsoft.Wallet", new(93, 5600)),
        ("Microsoft.WindowsMaps", new(65, 16800)),    // some use it
        ("Microsoft.ZuneMusic", new(58, 18200)),      // Groove — mixed
        ("Microsoft.ZuneVideo", new(62, 15900)),      // Movies & TV — mixed
        ("Clipchamp", new(78, 9200)),
        ("549981C3F5F10", new(88, 21000)),            // Cortana

        // ── Recommended to Remove (65-84) ──
        ("Microsoft.WindowsFeedbackHub", new(76, 13400)),
        ("Microsoft.WindowsAlarms", new(45, 14800)),   // many use alarms
        ("Microsoft.WindowsSoundRecorder", new(55, 8900)),
        ("Microsoft.WindowsCamera", new(32, 16200)),   // useful on laptops
        ("Microsoft.MSPaint", new(35, 17800)),         // Paint 3D — classic Paint is separate
        ("Microsoft.PowerAutomateDesktop", new(72, 7600)),
        ("Microsoft.Todos", new(40, 11300)),           // Microsoft To Do — useful
        ("Microsoft.YourPhone", new(68, 16700)),       // Phone Link

        // ── Mixed (40-64) ──
        ("Microsoft.MicrosoftStickyNotes", new(38, 15200)),  // useful for many
        ("Microsoft.Windows.Photos", new(25, 19400)),        // default photo viewer
        ("Microsoft.WindowsStore", new(5, 20100)),           // do NOT remove
        ("Microsoft.WindowsCalculator", new(8, 18900)),      // essential
        ("Microsoft.WindowsNotepad", new(6, 17200)),         // essential
        ("Microsoft.ScreenSketch", new(22, 14600)),          // Snipping Tool — useful

        // ── OEM Bloatware (typically high scores) ──
        ("Dell", new(88, 8400)),
        ("HP.", new(85, 7900)),
        ("Lenovo", new(86, 7200)),
        ("Acer", new(87, 6100)),
        ("ASUS", new(84, 5800)),
        ("Samsung", new(82, 5200)),
        ("McAfee", new(96, 22400)),
        ("Norton", new(94, 19800)),
        ("Avast", new(90, 14200)),
        ("WildTangent", new(98, 11700)),
        ("CyberLink", new(88, 7800)),
        ("Dolby", new(45, 6200)),        // some want Dolby audio
        ("Realtek", new(30, 5400)),      // audio driver — risky
        ("Disney", new(75, 8900)),
        ("Spotify", new(42, 16400)),     // pre-installed but many use it
        ("Netflix", new(55, 14200)),
        ("Amazon.com", new(78, 12100)),
        ("Facebook", new(85, 13600)),
        ("Twitter", new(83, 10200)),
        ("TikTok", new(87, 9800)),
        ("Instagram", new(84, 8900)),
    };
}
