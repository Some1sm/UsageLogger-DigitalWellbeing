using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UsageLogger.Core;
using UsageLogger.Core.Data;
using UsageLogger.Core.Helpers;
using UsageLogger.Core.Models;
using UsageLogger.Helpers;

namespace UsageLogger.Helpers;

/// <summary>
/// Loads and aggregates AppSessions for a date range.
/// Extracted from HistoryViewModel for potential reuse.
/// </summary>
public static class SessionAggregator
{
    /// <summary>
    /// Loads all AppSessions for a date range, applying retroactive custom title rules.
    /// </summary>
    public static async Task<List<AppSession>> LoadSessionsForDateRangeAsync(DateTime start, DateTime end)
    {
        return await Task.Run(async () =>
        {
            List<AppSession> total = new List<AppSession>();
            string folder = ApplicationPath.UsageLogsFolder;
            var repo = new AppSessionRepository(folder);

            for (DateTime date = start; date <= end; date = date.AddDays(1))
            {
                var sessions = await repo.GetSessionsForDateAsync(date);
                total.AddRange(sessions);
            }

            // RETROACTIVE RULE APPLICATION:
            if (UserPreferences.CustomTitleRules != null && UserPreferences.CustomTitleRules.Count > 0)
            {
                foreach (var s in total)
                {
                    s.ProgramName = WindowTitleParser.Parse(
                        s.ProcessName,
                        s.ProgramName,
                        UserPreferences.CustomTitleRules
                    );
                }
            }

            return total;
        });
    }

    /// <summary>
    /// Aggregates flat AppSession list into AppUsage objects with ProgramBreakdown.
    /// Applies retroactive sub-app hiding.
    /// </summary>
    public static List<AppUsage> AggregateSessions(List<AppSession> sessions)
    {
        var usageMap = new Dictionary<string, AppUsage>();

        foreach (var session in sessions)
        {
            if (!usageMap.ContainsKey(session.ProcessName))
            {
                usageMap[session.ProcessName] = new AppUsage(session.ProcessName, session.ProgramName, TimeSpan.Zero);
            }

            var appUsage = usageMap[session.ProcessName];
            appUsage.Duration = appUsage.Duration.Add(session.Duration);

            if (string.IsNullOrEmpty(appUsage.ProgramName) && !string.IsNullOrEmpty(session.ProgramName))
            {
                appUsage.ProgramName = session.ProgramName;
            }

            // Aggregate Sub-App / Title breakdown with retroactive hide filter
            string effectiveProgramName = session.ProgramName;
            if (UserPreferences.ShouldHideSubApp(session.ProgramName))
            {
                effectiveProgramName = session.ProcessName; // Merge into parent
            }
            string title = !string.IsNullOrEmpty(effectiveProgramName) ? effectiveProgramName : session.ProcessName;
            if (appUsage.ProgramBreakdown.ContainsKey(title))
            {
                appUsage.ProgramBreakdown[title] = appUsage.ProgramBreakdown[title].Add(session.Duration);
            }
            else
            {
                appUsage.ProgramBreakdown[title] = session.Duration;
            }
        }
        return usageMap.Values.ToList();
    }
}
