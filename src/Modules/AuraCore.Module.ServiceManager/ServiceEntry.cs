namespace AuraCore.Module.ServiceManager;

public sealed record ServiceEntry(
    string Name,
    string DisplayName,
    string Status,       // "Running", "Stopped", "Paused"
    string StartType);   // "Automatic", "Manual", "Disabled"
