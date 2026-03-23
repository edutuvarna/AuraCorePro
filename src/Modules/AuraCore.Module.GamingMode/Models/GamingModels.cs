namespace AuraCore.Module.GamingMode.Models;

public sealed record GamingModeState
{
    public bool IsActive { get; init; }
    public string CurrentPowerPlan { get; init; } = "";
    public string CurrentPowerPlanGuid { get; init; } = "";
    public bool FocusAssistEnabled { get; init; }
    public int RunningBackgroundApps { get; init; }
    public List<SuspendableProcess> BackgroundProcesses { get; init; } = new();
    public List<GamingToggle> Toggles { get; init; } = new();
}

public sealed record SuspendableProcess
{
    public int Pid { get; init; }
    public string Name { get; init; } = "";
    public long MemoryMb { get; init; }
    public string Category { get; init; } = "";
    public bool SuggestSuspend { get; init; }
}

public sealed record GamingToggle
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public string Risk { get; init; } = "Safe";
    public bool IsEnabled { get; init; }
    public bool CurrentState { get; init; }
}

public sealed record GamingProfile
{
    public string Name { get; init; } = "Default";
    public bool SwitchPowerPlan { get; init; } = true;
    public bool DisableNotifications { get; init; } = true;
    public bool SuspendBackground { get; init; } = true;
    public bool BoostPriority { get; init; } = true;
    public bool CleanRam { get; init; } = true;
    public List<string> ProcessesToSuspend { get; init; } = new();
    public List<string> ProcessesToNeverSuspend { get; init; } = new();
}
