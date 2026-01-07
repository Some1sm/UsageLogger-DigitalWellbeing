using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using DigitalWellbeingWinUI3.Models;

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
                var data = new
                {
                    DayAmount,
                    DetailedUsageDayCount,
                    MinumumDuration,
                    EnableAutoRefresh,
                    RefreshIntervalSeconds,

                    DataFlushIntervalSeconds,
                    UserExcludedProcesses,
                    ThemeMode,
                    MinimizeOnExit,
                    AppTimeLimits,
                    CustomTags,
                    IncognitoMode,
                    ShowCombinedAudioView,
                    ProcessDisplayNames,
                    CustomIconPaths,
                    TitleDisplayNames,
                    TitleTimeLimits,
                    ExcludedTitles,
                    LanguageCode,
                    UseRamCache
                };

                string json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
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
                    var data = JsonSerializer.Deserialize<JsonElement>(json);

                    if (data.TryGetProperty(nameof(DayAmount), out var prop)) DayAmount = prop.GetInt32();
                    if (data.TryGetProperty(nameof(DetailedUsageDayCount), out prop)) DetailedUsageDayCount = prop.GetInt32();
                    if (data.TryGetProperty(nameof(MinumumDuration), out prop)) MinumumDuration = JsonSerializer.Deserialize<TimeSpan>(prop.GetRawText());
                    if (data.TryGetProperty(nameof(EnableAutoRefresh), out prop)) EnableAutoRefresh = prop.GetBoolean();
                    if (data.TryGetProperty(nameof(RefreshIntervalSeconds), out prop)) RefreshIntervalSeconds = prop.GetInt32();
                    if (data.TryGetProperty(nameof(DataFlushIntervalSeconds), out prop)) DataFlushIntervalSeconds = prop.GetInt32();
                    if (data.TryGetProperty(nameof(UserExcludedProcesses), out prop)) UserExcludedProcesses = JsonSerializer.Deserialize<List<string>>(prop.GetRawText()) ?? new List<string>();
                    if (data.TryGetProperty(nameof(ThemeMode), out prop)) ThemeMode = prop.GetString();
                    if (data.TryGetProperty(nameof(MinimizeOnExit), out prop)) MinimizeOnExit = prop.GetBoolean();
                    if (data.TryGetProperty(nameof(AppTimeLimits), out prop)) AppTimeLimits = JsonSerializer.Deserialize<Dictionary<string, int>>(prop.GetRawText()) ?? new Dictionary<string, int>();
                    if (data.TryGetProperty(nameof(CustomTags), out prop)) CustomTags = JsonSerializer.Deserialize<List<CustomAppTag>>(prop.GetRawText()) ?? new List<CustomAppTag>();
                    if (data.TryGetProperty(nameof(IncognitoMode), out prop)) IncognitoMode = prop.GetBoolean();
                    if (data.TryGetProperty(nameof(ShowCombinedAudioView), out prop)) ShowCombinedAudioView = prop.GetBoolean();
                    if (data.TryGetProperty(nameof(ProcessDisplayNames), out prop)) ProcessDisplayNames = JsonSerializer.Deserialize<Dictionary<string, string>>(prop.GetRawText()) ?? new Dictionary<string, string>();
                    if (data.TryGetProperty(nameof(CustomIconPaths), out prop)) CustomIconPaths = JsonSerializer.Deserialize<Dictionary<string, string>>(prop.GetRawText()) ?? new Dictionary<string, string>();
                    if (data.TryGetProperty(nameof(TitleDisplayNames), out prop)) TitleDisplayNames = JsonSerializer.Deserialize<Dictionary<string, string>>(prop.GetRawText()) ?? new Dictionary<string, string>();
                    if (data.TryGetProperty(nameof(TitleTimeLimits), out prop)) TitleTimeLimits = JsonSerializer.Deserialize<Dictionary<string, int>>(prop.GetRawText()) ?? new Dictionary<string, int>();
                    if (data.TryGetProperty(nameof(ExcludedTitles), out prop)) ExcludedTitles = JsonSerializer.Deserialize<List<string>>(prop.GetRawText()) ?? new List<string>();
                    if (data.TryGetProperty(nameof(LanguageCode), out prop)) LanguageCode = prop.GetString() ?? "";
                    if (data.TryGetProperty(nameof(UseRamCache), out prop)) UseRamCache = prop.GetBoolean();
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
            catch { }
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
