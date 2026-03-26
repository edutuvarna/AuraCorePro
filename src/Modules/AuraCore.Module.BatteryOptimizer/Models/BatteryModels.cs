namespace AuraCore.Module.BatteryOptimizer.Models;

public sealed class BatteryStatus
{
    public bool HasBattery { get; set; }
    public bool IsCharging { get; set; }
    public bool IsOnAC { get; set; }
    public int ChargePercent { get; set; }
    public string ChargeStatus { get; set; } = "Unknown";

    // Capacity
    public int DesignCapacityMWh { get; set; }
    public int FullChargeCapacityMWh { get; set; }
    public int CurrentChargeMWh { get; set; }

    /// <summary>Battery health percentage (full charge vs design)</summary>
    public int HealthPercent => DesignCapacityMWh > 0
        ? (int)Math.Round(FullChargeCapacityMWh * 100.0 / DesignCapacityMWh)
        : 0;

    /// <summary>Wear level percentage</summary>
    public int WearPercent => 100 - HealthPercent;

    public string HealthGrade => HealthPercent switch
    {
        >= 90 => "Excellent",
        >= 75 => "Good",
        >= 50 => "Fair",
        >= 25 => "Poor",
        _ => "Critical"
    };

    public string HealthColor => HealthGrade switch
    {
        "Excellent" => "Green",
        "Good" => "Blue",
        "Fair" => "Amber",
        "Poor" or "Critical" => "Red",
        _ => "Blue"
    };

    // Estimates
    public TimeSpan EstimatedRemaining { get; set; }
    public int CycleCount { get; set; }
    public string Chemistry { get; set; } = "";
    public string BatteryName { get; set; } = "";
    public string Manufacturer { get; set; } = "";
    public string SerialNumber { get; set; } = "";

    public string EstRemainingDisplay => EstimatedRemaining.TotalMinutes > 0
        ? $"{(int)EstimatedRemaining.TotalHours}h {EstimatedRemaining.Minutes}m"
        : IsOnAC ? "Plugged in" : "Calculating...";

    public string DesignCapacityDisplay => DesignCapacityMWh > 0
        ? $"{DesignCapacityMWh / 1000.0:F1} Wh" : "Unknown";
    public string FullChargeCapacityDisplay => FullChargeCapacityMWh > 0
        ? $"{FullChargeCapacityMWh / 1000.0:F1} Wh" : "Unknown";

    public string? Error { get; set; }
}

public sealed class PowerPlanInfo
{
    public Guid PlanId { get; set; }
    public string Name { get; set; } = "";
    public bool IsActive { get; set; }
    public string Description { get; set; } = "";
}

public sealed class BatteryReportResult
{
    public string ReportPath { get; set; } = "";
    public bool Success { get; set; }
    public string? Error { get; set; }
}

public sealed class PowerDrainApp
{
    public string Name { get; set; } = "";
    public double CpuPercent { get; set; }
    public long WorkingSetMB { get; set; }
    public string Impact { get; set; } = "Low";

    public string ImpactColor => Impact switch
    {
        "High" => "Red",
        "Medium" => "Amber",
        _ => "Green"
    };
}
