using System.Collections.Generic;

namespace AuraCore.PrivilegedService.Ops;

public static class ActionWhitelist
{
    public static class Windows
    {
        private static readonly HashSet<string> Allowed = new(System.StringComparer.Ordinal)
        {
            // Driver Updater
            "driver.scan",
            "driver.export",

            // Defender Manager
            "defender.update-signatures",
            "defender.scan-quick",
            "defender.scan-full",
            "defender.set-realtime",
            "defender.add-exclusion",
            "defender.remove-exclusion",
            "defender.remove-threat",

            // Service Manager
            "service.start",
            "service.stop",
            "service.restart",
            "service.set-startup"
        };

        public static bool IsAllowed(string actionId) => Allowed.Contains(actionId);

        public static IReadOnlyCollection<string> All => Allowed;
    }
}
