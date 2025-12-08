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

        private static async Task LoadAppTags()
        {
            appTags.Clear();

            try
            {
                string text = await Task.Run(() => File.ReadAllText(appTagsPath));

                string[] rows = text.Split('\n');

                foreach (string row in rows)
                {
                    try
                    {
                        string[] cells = row.Split('\t');

                        string processName = cells[0];
                        AppTag appTag = (AppTag)int.Parse(cells[1]);

                        appTags.Add(processName, appTag);
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
                // AppLogger.WriteLine($"CANNOT FIND: {appTagsPath}");

                // Saves an empty one
                SaveAppTags();
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

        private static void SaveAppTags()
        {
            List<string> lines = new List<string>();

            foreach (KeyValuePair<string, AppTag> appTag in appTags)
            {
                lines.Add($"{appTag.Key}\t{(int)appTag.Value}");
            }

            File.WriteAllLines(appTagsPath, lines);
        }

        public static void UpdateAppTag(string processName, AppTag appTag)
        {
            // Remove tag if set to NONE
            if (appTag == AppTag.Untagged)
            {
                if (appTags.ContainsKey(processName))
                {
                    appTags.Remove(processName);
                }
            }
            // Else, update or add new
            else
            {
                if (appTags.ContainsKey(processName))
                {
                    appTags[processName] = appTag;
                }
                else
                {
                    appTags.Add(processName, appTag);
                }
            }

            SaveAppTags();
        }

        public static void RemoveTag(int tagId)
        {
            var keysToUpdate = appTags.Where(kvp => (int)kvp.Value == tagId).Select(kvp => kvp.Key).ToList();
            
            foreach (var key in keysToUpdate)
            {
                appTags.Remove(key); // Removing means resetting to Untagged (which is absence from list or 0)
            }
            
            if (keysToUpdate.Count > 0)
            {
                SaveAppTags();
            }
        }

        public static AppTag GetAppTag(string processName)
        {
            if (appTags.ContainsKey(processName))
            {
                return appTags[processName];
            }
            return AppTag.Untagged;
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
