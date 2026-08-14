#nullable enable
using System;
using System.Collections.Generic;

namespace UsageLogger.Core.Models;

/// <summary>
/// Represents aggregated usage of an application.
/// </summary>
public class AppUsage
{
    public string ProgramName { get; set; } = string.Empty;
    public string ProcessName { get; set; } = string.Empty;
    public TimeSpan Duration { get; set; }
    public double EnergyWattHours { get; set; }
    public string PowerImpact { get; set; } = string.Empty;
    public Dictionary<string, TimeSpan> ProgramBreakdown { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, Dictionary<string, TimeSpan>> DetailedBreakdown { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public AppUsage(string processName, string programName, TimeSpan duration, double energyWattHours = 0.0, string powerImpact = "")
    {
        ProcessName = processName ?? string.Empty;
        ProgramName = programName ?? string.Empty;
        Duration = duration;
        EnergyWattHours = energyWattHours;
        PowerImpact = powerImpact;
    }

    public AppUsage() { }
}
