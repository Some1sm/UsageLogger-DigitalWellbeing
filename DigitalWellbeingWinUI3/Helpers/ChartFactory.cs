using System;

namespace DigitalWellbeingWinUI3.Helpers
{
    /// <summary>
    /// Factory class for creating chart series and color palettes.
    /// Centralizes all visualization logic.
    /// </summary>
    public static class ChartFactory
    {
        /// <summary>
        /// Formats hours as "Xh Ym Zs" for consistent tooltip display.
        /// </summary>
        public static string FormatHours(double hours)
        {
            TimeSpan t = TimeSpan.FromHours(hours);
            return $"{(int)t.TotalHours}h {t.Minutes}m {t.Seconds}s";
        }

        /// <summary>
        /// Formats minutes as "Xh Ym" for consistent tooltip display.
        /// </summary>
        public static string FormatMinutes(double totalMinutes)
        {
            TimeSpan t = TimeSpan.FromMinutes(totalMinutes);
            if (t.TotalHours >= 1)
                return $"{(int)t.TotalHours}h {t.Minutes}m";
            else if (t.TotalMinutes >= 1)
                return $"{t.Minutes}m {t.Seconds}s";
            else
                return $"{t.Seconds}s";
        }
    }
}
