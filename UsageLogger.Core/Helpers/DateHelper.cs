#nullable enable
using System;

namespace UsageLogger.Core.Helpers;

/// <summary>
/// Provides logical date calculations based on a configurable day start time.
/// When DayStartMinutes = 240 (4:00 AM), activity from midnight–3:59 AM counts as the previous day.
/// </summary>
public static class DateHelper
{
    /// <summary>
    /// Total minutes past midnight when a new "logical day" begins (0–1439).
    /// Default 0 = midnight (normal behavior).
    /// </summary>
    public static int DayStartMinutes { get; set; } = 0;

    /// <summary>
    /// Convenience: gets or sets just the hour component (0–23).
    /// </summary>
    public static int DayStartHour
    {
        get => DayStartMinutes / 60;
        set => DayStartMinutes = (value * 60) + (DayStartMinutes % 60);
    }

    /// <summary>
    /// Convenience: gets or sets just the minute component (0–59).
    /// </summary>
    public static int DayStartMinute
    {
        get => DayStartMinutes % 60;
        set => DayStartMinutes = (DayStartHour * 60) + Math.Clamp(value, 0, 59);
    }

    /// <summary>
    /// Returns the logical date for a given timestamp.
    /// If DayStartMinutes = 240 (4:00 AM) and timestamp is Wed 2:00 AM, returns Tuesday's date.
    /// </summary>
    public static DateTime GetLogicalDate(DateTime timestamp)
    {
        if (DayStartMinutes <= 0) return timestamp.Date;
        return timestamp.AddMinutes(-DayStartMinutes).Date;
    }

    /// <summary>
    /// Returns today's logical date.
    /// </summary>
    public static DateTime GetLogicalToday() => GetLogicalDate(DateTime.Now);

    /// <summary>
    /// Returns the real calendar time window for a logical date.
    /// E.g. DayStartMinutes=240, logicalDate=Tuesday → (Tue 04:00, Wed 04:00)
    /// </summary>
    public static (DateTime Start, DateTime End) GetCalendarWindow(DateTime logicalDate)
    {
        DateTime dayStart = logicalDate.Date.AddMinutes(DayStartMinutes);
        DateTime dayEnd = dayStart.AddDays(1);
        return (dayStart, dayEnd);
    }
}
