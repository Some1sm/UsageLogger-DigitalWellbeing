using System;
using System.Collections.Generic;

namespace DigitalWellbeing.Core.Models
{
    public class UserPreferencesData
    {
        public bool Theme { get; set; } = true; // LEGACY: Kept for compat, but code might use ThemeMode
        public string ThemeMode { get; set; } = "System";
        public int DayAmount { get; set; } = 7;
        public int DetailedUsageDayCount { get; set; } = 1;
        public TimeSpan MinumumDuration { get; set; } = TimeSpan.FromMinutes(1);
        public int AppLimit { get; set; } = 5;
        public double WindowWidth { get; set; } = 1100;
        public double WindowHeight { get; set; } = 700;
        public int AppUsageSortType { get; set; } = 0; // 0=Duration, 1=Name
        public bool EnableAutoRefresh { get; set; } = true;
        public int RefreshIntervalSeconds { get; set; } = 60;
        public int DataFlushIntervalSeconds { get; set; } = 300;
        public int TimelineMergeThresholdSeconds { get; set; } = 30;
        public bool MinimizeOnExit { get; set; } = true;

        public Dictionary<string, int> TimeLimits { get; set; } = new(); // LEGACY name in DTO? Code uses AppTimeLimits
        public Dictionary<string, int> AppTimeLimits { get; set; } = new();

        public Dictionary<string, string> AppAlias { get; set; } = new();
        public List<CustomAppTag> CustomTags { get; set; } = new();
        public bool IncognitoMode { get; set; } = false;
        public bool ShowCombinedAudioView { get; set; } = false;
        public Dictionary<string, string> ProcessDisplayNames { get; set; } = new();
        public Dictionary<string, string> CustomIconPaths { get; set; } = new();
        public List<string> UserExcludedProcesses { get; set; } = new();

        // Title/Sub-app specific settings
        public Dictionary<string, string> TitleDisplayNames { get; set; } = new();
        public Dictionary<string, int> TitleTimeLimits { get; set; } = new();
        public List<string> ExcludedTitles { get; set; } = new();

        public string LanguageCode { get; set; } = "";
        public bool UseRamCache { get; set; } = true;
    }
}
