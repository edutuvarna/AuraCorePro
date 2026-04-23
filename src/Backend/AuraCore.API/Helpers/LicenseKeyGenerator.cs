namespace AuraCore.API.Helpers;

public static class LicenseKeyGenerator
{
    /// <summary>
    /// Generates a license key in the AC-XXXX-XXXX-XXXX-XXXX format
    /// (16 hex chars, dash-separated 4-block, prefixed with AC-).
    /// Phase 6.10 T3.6 — replaces the legacy 32-char raw hex format.
    /// Backend validation accepts both new and legacy formats during
    /// the transition; existing keys keep their format (no migration).
    /// </summary>
    public static string Generate()
    {
        var raw = Guid.NewGuid().ToString("N").ToUpperInvariant().Substring(0, 16);
        return $"AC-{raw.Substring(0, 4)}-{raw.Substring(4, 4)}-{raw.Substring(8, 4)}-{raw.Substring(12, 4)}";
    }
}
