using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using DigitalWellbeing.Core.Models;

namespace DigitalWellbeingWinUI3.Helpers
{
    public static class UserPreferences
    {
        private static string SettingsPath => Path.Combine(DigitalWellbeing.Core.ApplicationPath.APP_LOCATION, "user_preferences.json");

        // properties need to be loaded
        public static int DayAmount { get; set; } = 7;
        public static int DetailedUsageDayCount { get; set; } = 1;
        public static TimeSpan MinumumDuration { get; set; } = TimeSpan.FromSeconds(0);
        public static bool EnableAutoRefresh { get; set; } = true;
        public static int RefreshIntervalSeconds { get; set; } = 60;
        public static int DataFlushIntervalSeconds { get; set; } = 300; // Service: how often RAM data is flushed to disk (5 min default)
        public static int TimelineMergeThresholdSeconds { get; set; } = 30; // Default 30s
        public static List<string> UserExcludedProcesses { get; set; } = new List<string>();
        public static string ThemeMode { get; set; } = "System"; // System, Light, Dark
        public static bool MinimizeOnExit { get; set; } = true;
        public static Dictionary<string, int> AppTimeLimits { get; set; } = new Dictionary<string, int>();
        public static List<CustomAppTag> CustomTags { get; set; } = new List<CustomAppTag>();
        public static bool IncognitoMode { get; set; } = false;
        public static bool ShowCombinedAudioView { get; set; } = false;
        public static Dictionary<string, string> ProcessDisplayNames { get; set; } = new Dictionary<string, string>();
        public static Dictionary<string, string> CustomIconPaths { get; set; } = new Dictionary<string, string>();
        
        // Title/Sub-app specific settings (key format: "ProcessName|Title")
        public static Dictionary<string, string> TitleDisplayNames { get; set; } = new Dictionary<string, string>();
        public static Dictionary<string, int> TitleTimeLimits { get; set; } = new Dictionary<string, int>();
        public static List<string> ExcludedTitles { get; set; } = new List<string>();
        public static string LanguageCode { get; set; } = "";
        public static bool UseRamCache { get; set; } = true; // True = RAM buffer + 5 min flush; False = Direct disk writes

        static UserPreferences()
        {
            Load();
        }

        public static void Save()
        {
            try
            {
                var data = new UserPreferencesData
                {
                    DayAmount = DayAmount,
                    DetailedUsageDayCount = DetailedUsageDayCount,
                    MinumumDuration = MinumumDuration,
                    EnableAutoRefresh = EnableAutoRefresh,
                    RefreshIntervalSeconds = RefreshIntervalSeconds,
                    DataFlushIntervalSeconds = DataFlushIntervalSeconds,
                    TimelineMergeThresholdSeconds = TimelineMergeThresholdSeconds,
                    UserExcludedProcesses = UserExcludedProcesses,
                    ThemeMode = ThemeMode,
                    MinimizeOnExit = MinimizeOnExit,
                    AppTimeLimits = AppTimeLimits,
                    CustomTags = CustomTags,
                    IncognitoMode = IncognitoMode,
                    ShowCombinedAudioView = ShowCombinedAudioView,
                    ProcessDisplayNames = ProcessDisplayNames,
                    CustomIconPaths = CustomIconPaths,
                    TitleDisplayNames = TitleDisplayNames,
                    TitleTimeLimits = TitleTimeLimits,
                    ExcludedTitles = ExcludedTitles,
                    LanguageCode = LanguageCode,
                    UseRamCache = UseRamCache
                };

                // Use Source Generated context - Fully AOT Safe
                string json = JsonSerializer.Serialize(data, DigitalWellbeing.Core.Contexts.AppJsonContext.Default.UserPreferencesData);
                
                Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath));
                File.WriteAllText(SettingsPath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UserPreferences Save Error: {ex}");
            }
        }

        public static void Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    string json = File.ReadAllText(SettingsPath);
                    // Use Source Generated context
                    var data = JsonSerializer.Deserialize(json, DigitalWellbeing.Core.Contexts.AppJsonContext.Default.UserPreferencesData);

                    if (data != null)
                    {
                        DayAmount = data.DayAmount;
                        DetailedUsageDayCount = data.DetailedUsageDayCount;
                        MinumumDuration = data.MinumumDuration;
                        EnableAutoRefresh = data.EnableAutoRefresh;
                        RefreshIntervalSeconds = data.RefreshIntervalSeconds;
                        DataFlushIntervalSeconds = data.DataFlushIntervalSeconds;
                        TimelineMergeThresholdSeconds = data.TimelineMergeThresholdSeconds;
                        UserExcludedProcesses = data.UserExcludedProcesses ?? new List<string>();
                        ThemeMode = data.ThemeMode;
                        MinimizeOnExit = data.MinimizeOnExit;
                        AppTimeLimits = data.AppTimeLimits ?? new Dictionary<string, int>();
                        CustomTags = data.CustomTags ?? new List<CustomAppTag>();
                        IncognitoMode = data.IncognitoMode;
                        ShowCombinedAudioView = data.ShowCombinedAudioView;
                        ProcessDisplayNames = data.ProcessDisplayNames ?? new Dictionary<string, string>();
                        CustomIconPaths = data.CustomIconPaths ?? new Dictionary<string, string>();
                        TitleDisplayNames = data.TitleDisplayNames ?? new Dictionary<string, string>();
                        TitleTimeLimits = data.TitleTimeLimits ?? new Dictionary<string, int>();
                        ExcludedTitles = data.ExcludedTitles ?? new List<string>();
                        LanguageCode = data.LanguageCode ?? "";
                        UseRamCache = data.UseRamCache;
                    }
                }
                
                // Default Initialization
                if (CustomTags == null || CustomTags.Count == 0)
                {
                    CustomTags = new List<CustomAppTag>
                    {
                        // Untagged (0) is implicit usually, but good to have explicit if we want a color
                        new CustomAppTag(0, "Untagged", "#808080"), 
                        new CustomAppTag(1, "Work", "#1E90FF"),
                        new CustomAppTag(2, "Education", "#FFA500"),
                        new CustomAppTag(3, "Entertainment", "#9370DB"),
                        new CustomAppTag(4, "Social", "#FF1493"),
                        new CustomAppTag(5, "Utility", "#00FF3A"),
                        new CustomAppTag(6, "Game", "#DC143C")
                    };
                    Save();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UserPreferences Load Error: {ex}");
            }
        }

        public static void UpdateAppTimeLimit(string processName, TimeSpan timeLimit)
        {
            int totalMins = (int)timeLimit.TotalMinutes;

            if (totalMins <= 0)
            {
                if (AppTimeLimits.ContainsKey(processName))
                {
                    AppTimeLimits.Remove(processName);
                }
            }
            else
            {
                if (AppTimeLimits.ContainsKey(processName))
                {
                    AppTimeLimits[processName] = totalMins;
                }
                else
                {
                    AppTimeLimits.Add(processName, totalMins);
                }
            }
            Save();
        }

        /// <summary>
        /// Gets the display name for a process. Returns the custom display name if set, otherwise returns the original process name.
        /// </summary>
        public static string GetDisplayName(string processName)
        {
            if (string.IsNullOrWhiteSpace(processName))
                return processName;

            if (ProcessDisplayNames.ContainsKey(processName))
            {
                var displayName = ProcessDisplayNames[processName];
                if (!string.IsNullOrWhiteSpace(displayName))
                    return displayName;
            }

            return processName;
        }

        /// <summary>
        /// Sets a custom display name for a process. If displayName is null or empty, the mapping is removed.
        /// </summary>
        public static void SetDisplayName(string processName, string displayName)
        {
            if (string.IsNullOrWhiteSpace(processName))
                return;

            if (string.IsNullOrWhiteSpace(displayName))
            {
                RemoveDisplayName(processName);
                return;
            }

            ProcessDisplayNames[processName] = displayName;
            Save();
        }

        /// <summary>
        /// Removes the custom display name for a process.
        /// </summary>
        public static void RemoveDisplayName(string processName)
        {
            if (string.IsNullOrWhiteSpace(processName))
                return;

            if (ProcessDisplayNames.ContainsKey(processName))
            {
                ProcessDisplayNames.Remove(processName);
                Save();
            }
        }

        /// <summary>
        /// Gets the custom icon path for a process. Returns null if no custom icon is set.
        /// </summary>
        public static string GetCustomIconPath(string processName)
        {
            if (string.IsNullOrWhiteSpace(processName))
                return null;

            if (CustomIconPaths.ContainsKey(processName))
            {
                return CustomIconPaths[processName];
            }

            return null;
        }

        /// <summary>
        /// Sets a custom icon path for a process.
        /// </summary>
        public static void SetCustomIconPath(string processName, string iconPath)
        {
            if (string.IsNullOrWhiteSpace(processName))
                return;

            if (string.IsNullOrWhiteSpace(iconPath))
            {
                RemoveCustomIconPath(processName);
                return;
            }

            CustomIconPaths[processName] = iconPath;
            Save();
        }

        /// <summary>
        /// Removes the custom icon path for a process.
        /// </summary>
        public static void RemoveCustomIconPath(string processName)
        {
            if (string.IsNullOrWhiteSpace(processName))
                return;

            if (CustomIconPaths.ContainsKey(processName))
            {
                CustomIconPaths.Remove(processName);
                Save();
            }
        }

        #region Title/Sub-App Settings

        /// <summary>
        /// Gets the display name for a title. Returns custom name if set, otherwise original title.
        /// Key format: "ProcessName|Title"
        /// </summary>
        public static string GetTitleDisplayName(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return key;

            if (TitleDisplayNames.TryGetValue(key, out string displayName))
                return displayName;

            // Return the title portion (after the |)
            int pipeIndex = key.IndexOf('|');
            return pipeIndex >= 0 ? key.Substring(pipeIndex + 1) : key;
        }

        /// <summary>
        /// Sets a custom display name for a title.
        /// </summary>
        public static void SetTitleDisplayName(string key, string displayName)
        {
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(displayName))
                return;

            TitleDisplayNames[key] = displayName;
            Save();
        }

        /// <summary>
        /// Removes the custom display name for a title.
        /// </summary>
        public static void RemoveTitleDisplayName(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return;

            if (TitleDisplayNames.ContainsKey(key))
            {
                TitleDisplayNames.Remove(key);
                Save();
            }
        }

        /// <summary>
        /// Updates the time limit for a title.
        /// </summary>
        public static void UpdateTitleTimeLimit(string key, TimeSpan timeLimit)
        {
            int totalMins = (int)timeLimit.TotalMinutes;

            if (totalMins <= 0)
            {
                if (TitleTimeLimits.ContainsKey(key))
                {
                    TitleTimeLimits.Remove(key);
                    Save();
                }
            }
            else
            {
                TitleTimeLimits[key] = totalMins;
                Save();
            }
        }

        #endregion
    }
}
