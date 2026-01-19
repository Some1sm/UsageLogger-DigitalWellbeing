using UsageLogger.Core;
using UsageLogger.Core.Models;
using UsageLogger.Core.Helpers;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace UsageLogger.Helpers
{
    public static class SettingsManager
    {
        public static Task WaitForInit { get; private set; } = Task.CompletedTask; // No async load needed anymore

        static SettingsManager()
        {
            // Data is loaded by UserPreferences static constructor synchronously
        }

        #region App Time Limits

        // Delegate to UserPreferences
        public static Dictionary<string, int> appTimeLimits => UserPreferences.AppTimeLimits;
        
        // Deprecated/Unused but kept for compatibility if needed
        private static void SaveAppTimeLimits() => UserPreferences.Save();

        public static Dictionary<string, AppTag> GetAllAppTags()
        {
            return new Dictionary<string, AppTag>(UserPreferences.AppTags);
        }

        // Deprecated: No-op or just ensure loaded
        private static Task LoadAppTimeLimits() => Task.CompletedTask;

        public static void UpdateAppTimeLimit(string processName, TimeSpan timeLimit)
        {
            // UserPreferences has the logic now
            UserPreferences.UpdateAppTimeLimit(processName, timeLimit);
        }

        #endregion

        #region App Tags

        // Delegate to UserPreferences
        public static Dictionary<string, AppTag> appTags => UserPreferences.AppTags;
        public static Dictionary<string, int> titleTags => UserPreferences.TitleTags;

        // Deprecated
        private static Task LoadAppTags() => Task.CompletedTask;
        private static Task LoadTitleTags() => Task.CompletedTask;
        private static void SaveAppTags() => UserPreferences.Save();
        private static void SaveTitleTags() => UserPreferences.Save();



        public static void UpdateAppTag(string processName, AppTag appTag)
        {
            if (appTag == AppTag.Untagged) appTags.Remove(processName);
            else appTags[processName] = appTag;
            SaveAppTags();
        }

        public static void UpdateTitleTag(string processName, string keyword, int tagId)
        {
            string key = processName + "|" + keyword;
            if (tagId == 0) titleTags.Remove(key);
            else titleTags[key] = tagId;
            SaveTitleTags();
        }

        public static void RemoveTag(int tagId)
        {
            // Remove from Apps
            var keysToUpdate = appTags.Where(kvp => (int)kvp.Value == tagId).Select(kvp => kvp.Key).ToList();
            foreach (var key in keysToUpdate) appTags.Remove(key);
            if (keysToUpdate.Count > 0) SaveAppTags();

            // Remove from Titles
            var titlesToUpdate = titleTags.Where(kvp => kvp.Value == tagId).Select(kvp => kvp.Key).ToList();
            foreach (var key in titlesToUpdate) titleTags.Remove(key);
            if (titlesToUpdate.Count > 0) SaveTitleTags();
        }

        public static AppTag GetAppTag(string processName)
        {
            if (appTags.ContainsKey(processName)) return appTags[processName];
            return AppTag.Untagged;
        }

        // New lookup method
        public static int? GetTitleTagId(string processName, string title)
        {
            // Simple keyword matching against dictionary keys?
            // "Process|Keyword".
            // Since we can't efficiently search "Contains" on 1000 dictionary keys every frame, we should cache or optimize.
            // But usually titleTags count is small (<50).
            
            foreach (var kvp in titleTags)
            {
                var parts = kvp.Key.Split('|');
                if (parts[0] == processName && title.IndexOf(parts[1], StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return kvp.Value;
                }
            }
            return null;
        }

        #endregion

        #region Run on Startup

        /// <summary>
        /// Startup mode options for the application.
        /// </summary>
        public enum StartupMode
        {
            None = 0,           // Don't run on startup
            TrackerOnly = 1,    // Run only the background Service/Tracker
            TrackerAndUI = 2    // Run both the Tracker and the UI interface
        }

        /// <summary>
        /// Sets the startup mode in the Windows registry.
        /// </summary>
        public static void SetStartupMode(StartupMode mode)
        {
            try
            {
                RegistryKey key = Registry.CurrentUser.OpenSubKey(ApplicationPath.AUTORUN_REGPATH, true);
                if (key == null) return;

                switch (mode)
                {
                    case StartupMode.None:
                        key.DeleteValue(ApplicationPath.AUTORUN_REGKEY, false);
                        break;

                    case StartupMode.TrackerOnly:
                        // Set to Service executable
                        string servicePath = GetServicePath();
                        if (!string.IsNullOrEmpty(servicePath))
                        {
                            key.SetValue(ApplicationPath.AUTORUN_REGKEY, servicePath);
                        }
                        break;

                    case StartupMode.TrackerAndUI:
                        // Set to UI executable (which also starts Service)
                        using (Process process = Process.GetCurrentProcess())
                        {
                            key.SetValue(ApplicationPath.AUTORUN_REGKEY, process.MainModule.FileName);
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SettingsManager] SetStartupMode error: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets the current startup mode from the registry.
        /// </summary>
        public static StartupMode GetStartupMode()
        {
            try
            {
                RegistryKey key = Registry.CurrentUser.OpenSubKey(ApplicationPath.AUTORUN_REGPATH);
                var value = key?.GetValue(ApplicationPath.AUTORUN_REGKEY) as string;

                if (string.IsNullOrEmpty(value))
                    return StartupMode.None;

                // Check if the registered path is the Service or the UI
                if (value.Contains("UsageLoggerService", StringComparison.OrdinalIgnoreCase))
                    return StartupMode.TrackerOnly;
                
                if (value.Contains("DigitalWellbeingWinUI3", StringComparison.OrdinalIgnoreCase))
                    return StartupMode.TrackerAndUI;

                // Legacy or unknown - treat as UI
                return StartupMode.TrackerAndUI;
            }
            catch
            {
                return StartupMode.None;
            }
        }

        private static string GetServicePath()
        {
            string[] possiblePaths = new[]
            {
                Path.Combine(AppContext.BaseDirectory, "Service", "UsageLoggerService.exe"),
                Path.Combine(AppContext.BaseDirectory, "UsageLoggerService.exe"),
                Path.Combine(AppContext.BaseDirectory, "..", "Service", "UsageLoggerService.exe")
            };

            foreach (var path in possiblePaths)
            {
                if (File.Exists(path))
                    return Path.GetFullPath(path);
            }
            return null;
        }

        // Legacy methods for backward compatibility
        public static void SetRunOnStartup(bool enabled)
        {
            SetStartupMode(enabled ? StartupMode.TrackerAndUI : StartupMode.None);
        }

        public static bool IsRunningOnStartup()
        {
            return GetStartupMode() != StartupMode.None;
        }

        #endregion

    }
}
