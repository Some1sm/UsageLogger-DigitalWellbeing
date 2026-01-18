#nullable enable
using System;
using System.Globalization;
using System.Linq;

namespace DigitalWellbeing.Core.Helpers
{
    public static class StringHelper
    {
        public static string NEWLINE = Environment.NewLine;

        public static string TimeSpanToString(TimeSpan duration)
        {
            string durationStr = (int)duration.Hours > 0 ? $"{duration.Hours}h " : "";
            durationStr += (int)duration.TotalMinutes > 0 ? $"{duration.Minutes}m " : "";
            durationStr += (int)duration.TotalSeconds > 0 ? $"{duration.Seconds}s " : "";

            return durationStr.Trim();
        }

        private static readonly TextInfo txtInfo = new CultureInfo("en-US", false).TextInfo;
        public static string TitleCaseWhenLower(string processName)
        {
            return processName.Any(char.IsUpper) ? processName : txtInfo.ToTitleCase(processName);
        }

        public static string TimeSpanToShortString(TimeSpan duration)
        {
            return $"{duration.Hours}h {duration.Minutes}m";
        }

        /// <summary>
        /// Formats duration as "Xh Ym Zs" - full format for tooltips.
        /// </summary>
        public static string FormatDurationFull(TimeSpan t)
        {
            return $"{(int)t.TotalHours}h {t.Minutes}m {t.Seconds}s";
        }

        /// <summary>
        /// Formats duration as "Xh Ym" or "Ym Zs" - compact format for UI labels.
        /// </summary>
        public static string FormatDurationCompact(TimeSpan t)
        {
            if (t.TotalHours >= 1)
                return $"{(int)t.TotalHours}h {t.Minutes}m";
            else if (t.TotalMinutes >= 1)
                return $"{t.Minutes}m {t.Seconds}s";
            else
                return $"{t.Seconds}s";
        }

        public static string ShortenBytes(ulong bytes)
        {
            ulong MB = bytes / 1048576;
            ulong GB = MB / 1024;

            if (GB > 0)
            {
                return $"{GB} GB";
            }
            else
            {
                return $"{MB} MB";
            }
        }

    }
}
