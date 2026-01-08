using System;
using DigitalWellbeing.Core.Helpers;

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
            return StringHelper.FormatDurationFull(TimeSpan.FromHours(hours));
        }

        /// <summary>
        /// Formats minutes as "Xh Ym" for consistent tooltip display.
        /// </summary>
        public static string FormatMinutes(double totalMinutes)
        {
            return StringHelper.FormatDurationCompact(TimeSpan.FromMinutes(totalMinutes));
        }
    }
}
