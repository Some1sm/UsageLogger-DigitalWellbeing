using DigitalWellbeing.Core.Interfaces;
using DigitalWellbeing.Core.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace DigitalWellbeing.Core.Data;

public class AppUsageRepository : IAppUsageRepository
{
    private readonly string _logsFolderPath;

    public AppUsageRepository(string logsFolderPath)
    {
        _logsFolderPath = logsFolderPath;
    }

    private string GetFilePath(DateTime date) => Path.Combine(_logsFolderPath, $"{date:MM-dd-yyyy}.log");

    // Use FileShare.ReadWrite to allow both Service (Write) and App (Read) to access file simultaneously
    public async Task<List<AppUsage>> GetUsageForDateAsync(DateTime date)
    {
        // Consolidate: Fetch from Session Repository instead of summary log
        AppSessionRepository sessionRepo = new AppSessionRepository(_logsFolderPath);
        var sessions = await sessionRepo.GetSessionsForDateAsync(date);

        // Group by process and program name to reconstruct summary
        var usageDict = new Dictionary<string, AppUsage>();

        foreach (var session in sessions)
        {
            if (!usageDict.ContainsKey(session.ProcessName))
            {
                usageDict[session.ProcessName] = new AppUsage(session.ProcessName, session.ProgramName, TimeSpan.Zero);
            }
            usageDict[session.ProcessName].Duration = usageDict[session.ProcessName].Duration.Add(session.Duration);
            
            // Populate sub-app breakdown
            string subAppKey = string.IsNullOrEmpty(session.ProgramName) ? session.ProcessName : session.ProgramName;
            if (usageDict[session.ProcessName].ProgramBreakdown.ContainsKey(subAppKey))
            {
                usageDict[session.ProcessName].ProgramBreakdown[subAppKey] = usageDict[session.ProcessName].ProgramBreakdown[subAppKey].Add(session.Duration);
            }
            else
            {
                usageDict[session.ProcessName].ProgramBreakdown[subAppKey] = session.Duration;
            }
        }

        return usageDict.Values.ToList();
    }

    public Task UpdateUsageAsync(DateTime date, List<AppUsage> entries)
    {
        // NO-OP: We no longer write redundant summary logs.
        // Data is persisted via AppSessionRepository in ActivityLogger -> SessionManager.
        return Task.CompletedTask;
    }
}
