#nullable enable
using System;

namespace UsageLogger.Core.Models;

public enum PowerSourceType
{
    Battery,
    AC,
    Unknown
}

public enum PowerImpactLevel
{
    VeryLow,
    Low,
    Moderate,
    High,
    VeryHigh
}

public class PowerSnapshot
{
    public PowerSourceType PowerSource { get; set; } = PowerSourceType.Unknown;
    public bool IsBatteryPresent { get; set; }
    public bool IsCharging { get; set; }
    public int BatteryPercentage { get; set; } = -1; // 0-100%
    public TimeSpan? EstimatedTimeRemaining { get; set; }
    public double InstantDrawWatts { get; set; } // Current power draw in Watts
    public bool IsSimulatedDraw { get; set; } // True if estimated (e.g. on AC or Desktop)
    public double BatteryHealthPercentage { get; set; } = 100.0;
    public string PowerStatusText { get; set; } = string.Empty;
    public string PowerDetailTooltip { get; set; } = string.Empty;
}
