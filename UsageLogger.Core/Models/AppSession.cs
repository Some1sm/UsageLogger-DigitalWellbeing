#nullable enable
using System;
using System.Collections.Generic;

namespace UsageLogger.Core.Models;

/// <summary>
/// Represents a single usage session of an application.
/// </summary>
public class AppSession
{
    public string ProcessName { get; set; } = string.Empty;
    public string ProgramName { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public bool IsAfk { get; set; }
    public List<string> AudioSources { get; set; } = [];
    public double EnergyWattHours { get; set; }
    public string PowerImpact { get; set; } = string.Empty;

    public TimeSpan Duration => EndTime - StartTime;

    public AppSession(string processName, string programName, DateTime startTime, DateTime endTime, bool isAfk = false, List<string>? audioSources = null, double energyWattHours = 0.0, string powerImpact = "")
    {
        ProcessName = processName ?? string.Empty;
        ProgramName = programName ?? string.Empty;
        StartTime = startTime;
        EndTime = endTime;
        IsAfk = isAfk;
        EnergyWattHours = energyWattHours;
        PowerImpact = powerImpact;
        if (audioSources is not null)
        {
            AudioSources = audioSources;
        }
    }

    public AppSession() { }
}
