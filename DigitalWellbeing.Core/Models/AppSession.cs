#nullable enable
using System;
using System.Collections.Generic;

namespace DigitalWellbeing.Core.Models;

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

    public TimeSpan Duration => EndTime - StartTime;

    public AppSession(string processName, string programName, DateTime startTime, DateTime endTime, bool isAfk = false, List<string>? audioSources = null)
    {
        ProcessName = processName ?? string.Empty;
        ProgramName = programName ?? string.Empty;
        StartTime = startTime;
        EndTime = endTime;
        IsAfk = isAfk;
        if (audioSources is not null)
        {
            AudioSources = audioSources;
        }
    }

    public AppSession() { }
}
