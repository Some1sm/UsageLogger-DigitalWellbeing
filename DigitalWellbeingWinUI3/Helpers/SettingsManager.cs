using DigitalWellbeing.Core;
using DigitalWellbeing.Core.Models;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace DigitalWellbeingWinUI3.Helpers
{
    public static class SettingsManager
    {
        static string folderPath = ApplicationPath.SettingsFolder;

        public static Task WaitForInit { get; private set; }

        static SettingsManager()
        {
            WaitForInit = Task.WhenAll(LoadAppTimeLimits(), LoadAppTags());
        }

        #region App Time Limits
        public static Dictionary<string, int> appTimeLimits = new Dictionary<string, int>();
        static string appTimeLimitsFilePath = folderPath + "app-time-limits.txt";

        public static Dictionary<string, AppTag> GetAllAppTags()
        {
            return new Dictionary<string, AppTag>(appTags);
        }

        private static async Task LoadAppTimeLimits()
        {
            appTimeLimits.Clear();

            try
            {
                string text = await Task.Run(() => File.ReadAllText(appTimeLimitsFilePath));

                string[] rows = text.Split('\n');

                foreach (string row in rows)
                {
                    try
                    {
                        string[] cells = row.Split('\t');

                        string processName = cells[0];
                        int timeLimitInMins = int.Parse(cells[1]);

                        appTimeLimits.Add(processName, timeLimitInMins);
                    }
                    catch (IndexOutOfRangeException)
                    {
                        // No indicated cells, possibly last line in txt
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex);
                        throw;
                    }
                }
            }
            catch (FileNotFoundException)
            {
                // AppLogger.WriteLine($"CANNOT FIND: {appTimeLimitsFilePath}");

                // Saves an empty one
                SaveAppTimeLimits();
            }
            catch (DirectoryNotFoundException)
            {
                Directory.CreateDirectory(folderPath);
            }
            catch (IOException)
            {
                Console.WriteLine("Can't read, file is still being used");
            }
            catch (Exception ex)
            {
                // AppLogger.WriteLine(ex.Message);
                Debug.WriteLine(ex.Message);
            }
        }

        private static void SaveAppTimeLimits()
        {
            List<string> lines = new List<string>();

            foreach (KeyValuePair<string, int> timeLimit in appTimeLimits)
            {
                lines.Add($"{timeLimit.Key}\t{timeLimit.Value}");
            }

            File.WriteAllLines(appTimeLimitsFilePath, lines);
        }

        public static void UpdateAppTimeLimit(string processName, TimeSpan timeLimit)
        {
            int totalMins = (int)timeLimit.TotalMinutes;

            // Remove time limit if set to 0 mins
            if (totalMins <= 0)
            {
                if (appTimeLimits.ContainsKey(processName))
                {
                    appTimeLimits.Remove(processName);
                }
            }
            // Else, update or add new
            else
            {
                if (appTimeLimits.ContainsKey(processName))
                {
                    appTimeLimits[processName] = totalMins;
                }
                else
                {
                    appTimeLimits.Add(processName, totalMins);
                }
            }

            SaveAppTimeLimits();
        }

        #endregion

        #region App Tags

        public static Dictionary<string, AppTag> appTags = new Dictionary<string, AppTag>();
        static string appTagsPath = folderPath + "app-tags.txt";

        // Title/Keyword Tags: ProcessName|Keyword -> TagId
        // Dictionary key format: "ProcessName|Keyword" (separator |)
        public static Dictionary<string, int> titleTags = new Dictionary<string, int>();
        static string titleTagsPath = folderPath + "title-tags.txt";

        private static async Task LoadAppTags()
        {
            await LoadTitleTags(); // Load titles too

            appTags.Clear();

            try
            {
                string text = await Task.Run(() => File.ReadAllText(appTagsPath));
                // ... existing AppTag loading ...
                string[] rows = text.Split('\n');
                foreach (string row in rows)
                {
                    try
                    {
                        string[] cells = row.Split('\t');
                        if (cells.Length >= 2)
                        {
                            string processName = cells[0];
                            AppTag appTag = (AppTag)int.Parse(cells[1]);
                            if (!appTags.ContainsKey(processName)) appTags.Add(processName, appTag);
                        }
                    }
                    catch { }
                }
            }
            catch (FileNotFoundException) { SaveAppTags(); }
            catch (DirectoryNotFoundException) { Directory.CreateDirectory(folderPath); }
            catch { }
        }

        private static async Task LoadTitleTags()
        {
            titleTags.Clear();
            try
            {
                if (File.Exists(titleTagsPath))
                {
                    string text = await Task.Run(() => File.ReadAllText(titleTagsPath));
                    string[] rows = text.Split('\n');
                    foreach (string row in rows)
                    {
                        try
                        {
                            // Format: ProcessName \t Keyword \t TagId
                            string[] cells = row.Split('\t');
                            if (cells.Length >= 3)
                            {
                                string key = cells[0] + "|" + cells[1];
                                int tagId = int.Parse(cells[2]);
                                if (!titleTags.ContainsKey(key)) titleTags.Add(key, tagId);
                            }
                        }
                        catch { }
                    }
                }
            }
            catch { }
        }

        private static void SaveAppTags()
        {
            List<string> lines = new List<string>();
            foreach (KeyValuePair<string, AppTag> appTag in appTags)
            {
                lines.Add($"{appTag.Key}\t{(int)appTag.Value}");
            }
            File.WriteAllLines(appTagsPath, lines);
        }

        private static void SaveTitleTags()
        {
            List<string> lines = new List<string>();
            foreach (var kvp in titleTags)
            {
                // Key is "Process|Keyword"
                var parts = kvp.Key.Split('|');
                if (parts.Length == 2)
                {
                    lines.Add($"{parts[0]}\t{parts[1]}\t{kvp.Value}");
                }
            }
            try { File.WriteAllLines(titleTagsPath, lines); } catch { }
        }

        public static void UpdateAppTag(string processName, AppTag appTag)
        {
            if (appTag == AppTag.Untagged)
            {
                if (appTags.ContainsKey(processName)) appTags.Remove(processName);
            }
            else
            {
                if (appTags.ContainsKey(processName)) appTags[processName] = appTag;
                else appTags.Add(processName, appTag);
            }
            SaveAppTags();
        }

        public static void UpdateTitleTag(string processName, string keyword, int tagId)
        {
            string key = processName + "|" + keyword;
            
            if (tagId == 0) // Untagged/Remove
            {
                if (titleTags.ContainsKey(key)) titleTags.Remove(key);
            }
            else
            {
                if (titleTags.ContainsKey(key)) titleTags[key] = tagId;
                else titleTags.Add(key, tagId);
            }
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

        public static void SetRunOnStartup(bool enabled)
        {
            RegistryKey key = Registry.CurrentUser.OpenSubKey(ApplicationPath.AUTORUN_REGPATH, true);
            
            if (enabled)
            {
                // Set registry key
                using (Process process = Process.GetCurrentProcess())
                {
                    key.SetValue(ApplicationPath.AUTORUN_REGKEY, process.MainModule.FileName);
                }
            }
            else
            {
                key.DeleteValue(ApplicationPath.AUTORUN_REGKEY, false);
            }
        }

        public static bool IsRunningOnStartup()
        {
            RegistryKey key = Registry.CurrentUser.OpenSubKey(ApplicationPath.AUTORUN_REGPATH);

            return key?.GetValue(ApplicationPath.AUTORUN_REGKEY) != null;
        }

        #endregion

    }
}
