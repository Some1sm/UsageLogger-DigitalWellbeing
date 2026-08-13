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
    /// Aggregates flat AppSession list into AppUsage objects with ProgramBreakdown and DetailedBreakdown.
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

            string specificTitle = !string.IsNullOrEmpty(session.ProgramName) ? session.ProgramName : session.ProcessName;
            string groupedTitle = specificTitle;

            if (UserPreferences.CustomTitleRules != null && UserPreferences.CustomTitleRules.Count > 0)
            {
                groupedTitle = WindowTitleParser.Parse(session.ProcessName, specificTitle, UserPreferences.CustomTitleRules);
            }
            else
            {
                groupedTitle = WindowTitleParser.Parse(session.ProcessName, specificTitle);
            }

            // Apply retroactive hide filter: use ProcessName if sub-app should be hidden
            if (UserPreferences.ShouldHideSubApp(groupedTitle) || UserPreferences.ShouldHideSubApp(specificTitle))
            {
                groupedTitle = session.ProcessName;
                specificTitle = session.ProcessName;
            }

            if (string.IsNullOrEmpty(appUsage.ProgramName))
            {
                appUsage.ProgramName = groupedTitle;
            }

            // Aggregate into ProgramBreakdown (Grouped umbrella title)
            if (appUsage.ProgramBreakdown.ContainsKey(groupedTitle))
            {
                appUsage.ProgramBreakdown[groupedTitle] = appUsage.ProgramBreakdown[groupedTitle].Add(session.Duration);
            }
            else
            {
                appUsage.ProgramBreakdown[groupedTitle] = session.Duration;
            }

            // Aggregate into DetailedBreakdown (Specific titles under Grouped title)
            if (!appUsage.DetailedBreakdown.ContainsKey(groupedTitle))
            {
                appUsage.DetailedBreakdown[groupedTitle] = new Dictionary<string, TimeSpan>();
            }

            if (appUsage.DetailedBreakdown[groupedTitle].ContainsKey(specificTitle))
            {
                appUsage.DetailedBreakdown[groupedTitle][specificTitle] = appUsage.DetailedBreakdown[groupedTitle][specificTitle].Add(session.Duration);
            }
            else
            {
                appUsage.DetailedBreakdown[groupedTitle][specificTitle] = session.Duration;
            }
        }
        return usageMap.Values.ToList();
    }
}
