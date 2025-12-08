using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using DigitalWellbeingWinUI3.Models;

namespace DigitalWellbeingWinUI3.Helpers
{
    public static class UserPreferences
    {
        private static string SettingsPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DigitalWellbeing", "user_preferences.json");

        // properties need to be loaded
        public static int DayAmount { get; set; } = 7;
        public static TimeSpan MinumumDuration { get; set; } = TimeSpan.FromSeconds(0);
        public static bool EnableAutoRefresh { get; set; } = true;
        public static int RefreshIntervalSeconds { get; set; } = 60;
        public static List<string> UserExcludedProcesses { get; set; } = new List<string>();
        public static string ThemeMode { get; set; } = "System"; // System, Light, Dark
        public static bool MinimizeOnExit { get; set; } = true;
        public static Dictionary<string, int> AppTimeLimits { get; set; } = new Dictionary<string, int>();
        public static List<CustomAppTag> CustomTags { get; set; } = new List<CustomAppTag>();

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
                    MinumumDuration,
                    EnableAutoRefresh,
                    RefreshIntervalSeconds,
                    UserExcludedProcesses,
                    ThemeMode,
                    MinimizeOnExit,
                    AppTimeLimits,
                    CustomTags
                };

                string json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
                Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath));
                File.WriteAllText(SettingsPath, json);
            }
            catch { }
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
                    if (data.TryGetProperty(nameof(MinumumDuration), out prop)) MinumumDuration = JsonSerializer.Deserialize<TimeSpan>(prop.GetRawText());
                    if (data.TryGetProperty(nameof(EnableAutoRefresh), out prop)) EnableAutoRefresh = prop.GetBoolean();
                    if (data.TryGetProperty(nameof(RefreshIntervalSeconds), out prop)) RefreshIntervalSeconds = prop.GetInt32();
                    if (data.TryGetProperty(nameof(UserExcludedProcesses), out prop)) UserExcludedProcesses = JsonSerializer.Deserialize<List<string>>(prop.GetRawText()) ?? new List<string>();
                    if (data.TryGetProperty(nameof(ThemeMode), out prop)) ThemeMode = prop.GetString();
                    if (data.TryGetProperty(nameof(MinimizeOnExit), out prop)) MinimizeOnExit = prop.GetBoolean();
                    if (data.TryGetProperty(nameof(AppTimeLimits), out prop)) AppTimeLimits = JsonSerializer.Deserialize<Dictionary<string, int>>(prop.GetRawText()) ?? new Dictionary<string, int>();
                    if (data.TryGetProperty(nameof(CustomTags), out prop)) CustomTags = JsonSerializer.Deserialize<List<CustomAppTag>>(prop.GetRawText()) ?? new List<CustomAppTag>();
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
                        new CustomAppTag(5, "Utility", "#808080"),
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
    }
}
