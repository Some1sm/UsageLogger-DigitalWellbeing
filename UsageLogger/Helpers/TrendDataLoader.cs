using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using UsageLogger.Core.Models;
using UsageLogger.Helpers;
using UsageLogger.ViewModels;

namespace UsageLogger.Helpers;

/// <summary>
/// Loads 7-day trend comparison data for the dashboard.
/// Extracted from AppUsageViewModel to separate data loading from presentation.
/// </summary>
public static class TrendDataLoader
{
    /// <summary>
    /// Compares today's usage with the same day last week and updates trend properties.
    /// </summary>
    public static async Task LoadAsync(
        DateTime currentDate,
        TimeSpan currentTotal,
        Func<AppUsage, bool> appUsageFilter,
        DispatcherQueue dispatcherQueue,
        Action<string, string, bool> setTrend)
    {
        await Task.Run(async () =>
        {
            DateTime pastDate = currentDate.AddDays(-7.0);
            List<AppUsage> list = (await AppUsageViewModel.GetData(pastDate)).Where(appUsageFilter).ToList();
            TimeSpan pastTotal = TimeSpan.Zero;
            foreach (AppUsage item in list)
            {
                pastTotal = pastTotal.Add(item.Duration);
            }

            double todayMinutes = currentTotal.TotalMinutes;
            double pastMinutes = pastTotal.TotalMinutes;
            string percentage = "";
            string desc = LocalizationHelper.GetString("FromLast") + " " + pastDate.ToString("dddd");
            bool isGood = true;

            if (pastMinutes == 0.0)
            {
                percentage = (todayMinutes > 0.0) ? "100%" : "0%";
                isGood = todayMinutes == 0.0;
            }
            else
            {
                double change = (todayMinutes - pastMinutes) / pastMinutes * 100.0;
                if (change > 0.0)
                {
                    percentage = $"↑ {Math.Abs((int)change)}%";
                    isGood = true;
                }
                else
                {
                    percentage = $"↓ {Math.Abs((int)change)}%";
                    isGood = false;
                }
            }

            dispatcherQueue.TryEnqueue(() => setTrend(percentage, desc, isGood));
        });
    }
}
