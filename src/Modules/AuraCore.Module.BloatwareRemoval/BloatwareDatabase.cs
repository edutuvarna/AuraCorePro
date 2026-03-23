namespace AuraCore.Module.BloatwareRemoval;

/// <summary>
/// Curated database of known Windows bloatware, categorized by risk.
/// This is what makes the module "AI-powered" — it knows what's safe to remove.
/// </summary>
internal static class BloatwareDatabase
{
    // Microsoft apps safe to remove — ship with Windows, not needed for function
    internal static readonly HashSet<string> MicrosoftBloat = new(StringComparer.OrdinalIgnoreCase)
    {
        "Microsoft.3DBuilder",
        "Microsoft.549981C3F5F10",           // Cortana
        "Microsoft.BingFinance",
        "Microsoft.BingNews",
        "Microsoft.BingSports",
        "Microsoft.BingTranslator",
        "Microsoft.BingWeather",
        "Microsoft.BingFoodAndDrink",
        "Microsoft.BingHealthAndFitness",
        "Microsoft.BingTravel",
        "Microsoft.GetHelp",
        "Microsoft.Getstarted",              // Tips
        "Microsoft.Messaging",
        "Microsoft.Microsoft3DViewer",
        "Microsoft.MicrosoftOfficeHub",
        "Microsoft.MicrosoftSolitaireCollection",
        "Microsoft.MicrosoftStickyNotes",
        "Microsoft.MixedReality.Portal",
        "Microsoft.MSPaint",                  // Paint 3D (old Paint is separate)
        "Microsoft.Office.OneNote",
        "Microsoft.OneConnect",
        "Microsoft.People",
        "Microsoft.Print3D",
        "Microsoft.SkypeApp",
        "Microsoft.Todos",
        "Microsoft.Wallet",
        "Microsoft.Windows.Photos",
        "Microsoft.WindowsAlarms",
        "Microsoft.WindowsCamera",
        "Microsoft.WindowsCommunicationsApps", // Mail & Calendar
        "Microsoft.WindowsFeedbackHub",
        "Microsoft.WindowsMaps",
        "Microsoft.WindowsSoundRecorder",
        "Microsoft.YourPhone",                // Phone Link
        "Microsoft.ZuneMusic",                // Groove Music
        "Microsoft.ZuneVideo",                // Movies & TV
        "Clipchamp.Clipchamp",
        "Microsoft.GamingApp",
        "Microsoft.PowerAutomateDesktop",
        "MicrosoftCorporationII.QuickAssist",
        "Microsoft.OutlookForWindows",
        "Microsoft.Windows.DevHome",
        "Microsoft.WindowsTerminal",          // Some users want this — Caution
        "Microsoft.Teams.Free",
        "MSTeams",
    };

    // Apps that are Caution — many users want these but they are optional
    internal static readonly HashSet<string> CautionApps = new(StringComparer.OrdinalIgnoreCase)
    {
        "Microsoft.WindowsCalculator",
        "Microsoft.WindowsNotepad",
        "Microsoft.Paint",
        "Microsoft.ScreenSketch",             // Snipping Tool
        "Microsoft.WindowsTerminal",
        "Microsoft.WindowsStore",
        "Microsoft.StorePurchaseApp",
        "Microsoft.Xbox.TCUI",
        "Microsoft.XboxGameOverlay",
        "Microsoft.XboxGamingOverlay",
        "Microsoft.XboxIdentityProvider",
        "Microsoft.XboxSpeechToTextOverlay",
        "Microsoft.GamingApp",
    };

    // System-required — removing these will break Windows functionality
    internal static readonly HashSet<string> SystemRequired = new(StringComparer.OrdinalIgnoreCase)
    {
        "Microsoft.WindowsStore",
        "Microsoft.StorePurchaseApp",
        "Microsoft.DesktopAppInstaller",
        "Microsoft.VCLibs",
        "Microsoft.NET",
        "Microsoft.UI.Xaml",
        "Microsoft.Services.Store.Engagement",
        "Microsoft.Windows.ShellExperienceHost",
        "Microsoft.Windows.StartMenuExperienceHost",
        "Microsoft.Windows.Search",
        "Microsoft.Windows.ContentDeliveryManager",
        "Microsoft.Windows.SecHealthUI",
        "Microsoft.SecHealthUI",
        "Microsoft.AAD.BrokerPlugin",
        "Microsoft.AccountsControl",
        "Microsoft.Windows.CloudExperienceHost",
        "Microsoft.Windows.OOBENetworkCaptivePortal",
        "Microsoft.Windows.OOBENetworkConnectionFlow",
        "Microsoft.Windows.PeopleExperienceHost",
        "Microsoft.Windows.XGpuEjectDialog",
        "Microsoft.XboxIdentityProvider",     // Needed for Xbox Game Pass
        "windows.immersivecontrolpanel",
        "Windows.PrintDialog",
        "NcsiUwpApp",
    };

    // Known OEM bloatware patterns
    internal static readonly string[] OemBloatPatterns =
    {
        "Dell", "HP", "Lenovo", "ASUS", "Acer", "Samsung", "Toshiba",
        "CyberLink", "WildTangent", "Candy", "Royal", "Bubble",
        "CookingFever", "Disney", "Dolby", "Drawboard", "Duolingo",
        "EclipseManager", "Fitbit", "Flipboard", "iHeartRadio",
        "king.com", "Marchetti", "McAfee", "Norton", "Plex",
        "PolarrPhotoEditor", "Shazam", "Spotify", "Twitter",
        "Wunderlist", "XING", "ActiproSoftware", "AdobeSystemsIncorporated",
        "Amazon", "Facebook", "Netflix", "TikTok", "Instagram",
    };
}
