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
    public Dictionary<string, TimeSpan> ProgramBreakdown { get; set; } = [];

    public AppUsage(string processName, string programName, TimeSpan duration)
    {
        ProcessName = processName ?? string.Empty;
        ProgramName = programName ?? string.Empty;
        Duration = duration;
    }

    public AppUsage() { }
}
