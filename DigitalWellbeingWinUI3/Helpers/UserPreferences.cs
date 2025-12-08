using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

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
                    AppTimeLimits
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
