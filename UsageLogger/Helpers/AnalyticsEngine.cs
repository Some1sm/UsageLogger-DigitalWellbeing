#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using UsageLogger.Core.Models;
using UsageLogger.Models;

namespace UsageLogger.Helpers
{
    public enum DayPart
    {
        Morning,
        Afternoon,
        Evening,
        Night
    }

    public enum FocusRating
    {
        DeepFocus,       // < 15 switches / hr
        BalancedFlow,    // 15 - 35 switches / hr
        FrequentSwitching, // 35 - 60 switches / hr
        HighFragmentation  // > 60 switches / hr
    }

    public record AppSwitchItem(string ProcessName, string DisplayName, int Count, double Percentage);

    public record DayPartSummary(
        TimeSpan MorningDuration, 
        TimeSpan AfternoonDuration, 
        TimeSpan EveningDuration, 
        TimeSpan NightDuration,
        double MorningPct,
        double AfternoonPct,
        double EveningPct,
        double NightPct,
        string MorningRangeText,
        string AfternoonRangeText,
        string EveningRangeText,
        string NightRangeText
    );

    public record ProductivitySummary(
        int Score,
        TimeSpan ProductiveDuration,
        TimeSpan NeutralDuration,
        TimeSpan LeisureDuration,
        int ProductivePct,
        int NeutralPct,
        int LeisurePct
    );

    public record ContextSwitchSummary(
        int TotalSwitches,
        double SwitchesPerHour,
        FocusRating Rating,
        string RatingLabel,
        List<AppSwitchItem> TopSwitchedApps
    );

    public static class AnalyticsEngine
    {
        private static readonly HashSet<string> AfkProcesses = new(StringComparer.OrdinalIgnoreCase) { "Away", "LogonUI" };

        public static bool IsAfk(AppSession s) => s.IsAfk || AfkProcesses.Contains(s.ProcessName);

        /// <summary>
        /// Analyzes day-part distribution based on user-configured hour boundaries.
        /// </summary>
        public static DayPartSummary ComputeDayParts(List<AppSession> sessions)
        {
            int mStart = UserPreferences.MorningStartHour;
            int aStart = UserPreferences.AfternoonStartHour;
            int eStart = UserPreferences.EveningStartHour;
            int nStart = UserPreferences.NightStartHour;

            double morningSec = 0;
            double afternoonSec = 0;
            double eveningSec = 0;
            double nightSec = 0;

            foreach (var s in sessions)
            {
                if (IsAfk(s) || AppUsageViewModel.IsProcessExcluded(s.ProcessName)) continue;
                if (s.Duration.TotalSeconds <= 0) continue;

                // Divide session into 1-minute or discrete slices across boundaries
                DateTime cur = s.StartTime;
                DateTime end = s.StartTime + s.Duration;

                while (cur < end)
                {
                    int hour = cur.Hour;
                    DayPart part;
                    if (mStart < aStart && hour >= mStart && hour < aStart) part = DayPart.Morning;
                    else if (aStart < eStart && hour >= aStart && hour < eStart) part = DayPart.Afternoon;
                    else if (eStart < nStart && hour >= eStart && hour < nStart) part = DayPart.Evening;
                    else part = DayPart.Night;

                    // Next boundary calculation
                    DateTime nextHour = new DateTime(cur.Year, cur.Month, cur.Day, cur.Hour, 0, 0).AddHours(1);
                    DateTime sliceEnd = (end < nextHour) ? end : nextHour;
                    double sliceSec = (sliceEnd - cur).TotalSeconds;

                    switch (part)
                    {
                        case DayPart.Morning: morningSec += sliceSec; break;
                        case DayPart.Afternoon: afternoonSec += sliceSec; break;
                        case DayPart.Evening: eveningSec += sliceSec; break;
                        case DayPart.Night: nightSec += sliceSec; break;
                    }

                    cur = sliceEnd;
                }
            }

            double totalSec = morningSec + afternoonSec + eveningSec + nightSec;
            double safeTotal = totalSec > 0 ? totalSec : 1.0;

            double mPct = Math.Round((morningSec / safeTotal) * 100.0, 1);
            double aPct = Math.Round((afternoonSec / safeTotal) * 100.0, 1);
            double ePct = Math.Round((eveningSec / safeTotal) * 100.0, 1);
            double nPct = Math.Max(0, 100.0 - mPct - aPct - ePct);

            string mRange = $"{mStart:D2}:00 - {aStart:D2}:00";
            string aRange = $"{aStart:D2}:00 - {eStart:D2}:00";
            string eRange = $"{eStart:D2}:00 - {nStart:D2}:00";
            string nRange = $"{nStart:D2}:00 - {mStart:D2}:00";

            return new DayPartSummary(
                TimeSpan.FromSeconds(morningSec),
                TimeSpan.FromSeconds(afternoonSec),
                TimeSpan.FromSeconds(eveningSec),
                TimeSpan.FromSeconds(nightSec),
                mPct, aPct, ePct, nPct,
                mRange, aRange, eRange, nRange
            );
        }

        /// <summary>
        /// Computes productivity score and category balance.
        /// </summary>
        public static ProductivitySummary ComputeProductivity(List<AppSession> sessions)
        {
            double productiveSec = 0;
            double neutralSec = 0;
            double leisureSec = 0;

            foreach (var s in sessions)
            {
                if (IsAfk(s) || AppUsageViewModel.IsProcessExcluded(s.ProcessName)) continue;

                AppTag tag = AppTagHelper.GetAppTag(s.ProcessName);
                if (!string.IsNullOrEmpty(s.ProgramName))
                {
                    AppTag titleTag = AppTagHelper.GetTitleTag(s.ProcessName, s.ProgramName);
                    if (titleTag != AppTag.Untagged) tag = titleTag;
                }

                ProductivityTier tier = UserPreferences.GetTagTier(tag);
                double sec = s.Duration.TotalSeconds;

                switch (tier)
                {
                    case ProductivityTier.Productive: productiveSec += sec; break;
                    case ProductivityTier.Neutral: neutralSec += sec; break;
                    case ProductivityTier.Leisure: leisureSec += sec; break;
                }
            }

            double totalSec = productiveSec + neutralSec + leisureSec;
            double safeTotal = totalSec > 0 ? totalSec : 1.0;

            int score = totalSec > 0 
                ? (int)Math.Clamp(Math.Round(((productiveSec + 0.5 * neutralSec) / totalSec) * 100.0), 0, 100)
                : 100;

            int prodPct = (int)Math.Round((productiveSec / safeTotal) * 100.0);
            int neutPct = (int)Math.Round((neutralSec / safeTotal) * 100.0);
            int leisPct = Math.Max(0, 100 - prodPct - neutPct);

            return new ProductivitySummary(
                score,
                TimeSpan.FromSeconds(productiveSec),
                TimeSpan.FromSeconds(neutralSec),
                TimeSpan.FromSeconds(leisureSec),
                prodPct, neutPct, leisPct
            );
        }

        /// <summary>
        /// Computes app switches per hour and per-app switch count breakdown.
        /// </summary>
        public static ContextSwitchSummary ComputeContextSwitches(List<AppSession> sessions)
        {
            var sorted = sessions.Where(s => !IsAfk(s) && !AppUsageViewModel.IsProcessExcluded(s.ProcessName))
                                 .OrderBy(s => s.StartTime)
                                 .ToList();

            int totalSwitches = 0;
            var switchCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            string? prevProcess = null;

            double totalActiveSec = 0;

            foreach (var s in sorted)
            {
                totalActiveSec += s.Duration.TotalSeconds;

                if (prevProcess == null || !string.Equals(prevProcess, s.ProcessName, StringComparison.OrdinalIgnoreCase))
                {
                    totalSwitches++;
                    switchCounts[s.ProcessName] = switchCounts.GetValueOrDefault(s.ProcessName, 0) + 1;
                    prevProcess = s.ProcessName;
                }
            }

            double totalActiveHours = totalActiveSec / 3600.0;
            double switchesPerHour = totalActiveHours > 0.05 
                ? Math.Round(totalSwitches / totalActiveHours, 1) 
                : 0.0;

            FocusRating rating;
            string ratingKey;
            if (switchesPerHour < 15)
            {
                rating = FocusRating.DeepFocus;
                ratingKey = "FocusRating_DeepFocus";
            }
            else if (switchesPerHour < 35)
            {
                rating = FocusRating.BalancedFlow;
                ratingKey = "FocusRating_BalancedFlow";
            }
            else if (switchesPerHour < 60)
            {
                rating = FocusRating.FrequentSwitching;
                ratingKey = "FocusRating_FrequentSwitching";
            }
            else
            {
                rating = FocusRating.HighFragmentation;
                ratingKey = "FocusRating_HighFragmentation";
            }

            string ratingLabel = LocalizationHelper.GetString(ratingKey);
            if (string.IsNullOrEmpty(ratingLabel) || ratingLabel == ratingKey)
            {
                ratingLabel = rating switch
                {
                    FocusRating.DeepFocus => "Deep Focus",
                    FocusRating.BalancedFlow => "Balanced Flow",
                    FocusRating.FrequentSwitching => "Frequent Switching",
                    _ => "High Fragmentation"
                };
            }

            double safeSwitches = totalSwitches > 0 ? totalSwitches : 1.0;
            var topApps = switchCounts
                .OrderByDescending(kvp => kvp.Value)
                .Take(15)
                .Select(kvp => new AppSwitchItem(
                    kvp.Key,
                    UserPreferences.GetDisplayName(kvp.Key),
                    kvp.Value,
                    Math.Round((kvp.Value / safeSwitches) * 100.0, 1)
                ))
                .ToList();

            return new ContextSwitchSummary(
                totalSwitches,
                switchesPerHour,
                rating,
                ratingLabel,
                topApps
            );
        }
    }
}
