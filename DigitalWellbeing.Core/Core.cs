using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Environment;

namespace DigitalWellbeing.Core
{
    public static class ApplicationPath
    {
        static readonly SpecialFolder applicationPath = SpecialFolder.LocalApplicationData;
        static readonly string applicationFolderName = "digital-wellbeing";
        static readonly string imageCacheFolderName = "processicons";
        static readonly string dailyLogsFolderName = "dailylogs";
        static readonly string internalLogsFolder = "internal-logs";
        static readonly string settingsFolder = "settings";
        static readonly string autorunFileName = ".autorun";

        public static readonly string AUTORUN_REGPATH = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run";
#if DEBUG
        public static readonly string AUTORUN_REGKEY = "DigitalWellbeingWPFDEBUG";
#else
        public static readonly string AUTORUN_REGKEY = "DigitalWellbeingWPF";
#endif



        public static string APP_LOCATION
        {
            get 
            {
#if DEBUG
                return System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DigitalWellbeingDebug");
#else
                return GetFolderPath(applicationPath) + $@"\{applicationFolderName}";
#endif
            }
        }

        public static string autorunFilePath
        {
            get => APP_LOCATION + $@"\{autorunFileName}";
        }

        public static string UsageLogsFolder
        {
            get
            {
                // Check for custom path
                string customPathFile = System.IO.Path.Combine(APP_LOCATION, "custom_log_path.txt");
                if (System.IO.File.Exists(customPathFile))
                {
                    try
                    {
                        string customPath = System.IO.File.ReadAllText(customPathFile).Trim();
                        if (!string.IsNullOrEmpty(customPath) && System.IO.Directory.Exists(customPath))
                        {
                            return customPath.EndsWith("\\") ? customPath : customPath + "\\";
                        }
                    }
                    catch { }
                }
                // Default
                return APP_LOCATION + $@"\{dailyLogsFolderName}\";
            }
        }

        public static void SetCustomLogsFolder(string path)
        {
            string customPathFile = System.IO.Path.Combine(APP_LOCATION, "custom_log_path.txt");
            System.IO.Directory.CreateDirectory(APP_LOCATION);
            System.IO.File.WriteAllText(customPathFile, path ?? "");
        }

        public static void ClearCustomLogsFolder()
        {
            string customPathFile = System.IO.Path.Combine(APP_LOCATION, "custom_log_path.txt");
            if (System.IO.File.Exists(customPathFile))
                System.IO.File.Delete(customPathFile);
        }

        public static string GetCustomLogsFolderRaw()
        {
            string customPathFile = System.IO.Path.Combine(APP_LOCATION, "custom_log_path.txt");
            if (System.IO.File.Exists(customPathFile))
            {
                try
                {
                    return System.IO.File.ReadAllText(customPathFile).Trim();
                }
                catch { }
            }
            return null;
        }

        public static string SettingsFolder
        {
            get => APP_LOCATION + $@"\{settingsFolder}\";
        }

        public static string InternalLogsFolder
        {
            get => APP_LOCATION + $@"\{internalLogsFolder}\";
        }

        public static string GetImageCacheLocation(string appName = "")
        {
            string location = APP_LOCATION + $@"\{imageCacheFolderName}\";
            if (appName != "") { location += $"{appName}.ico"; }
            return location;
        }

        public static string GetCustomIconsLocation()
        {
            return APP_LOCATION + @"\CustomIcons\";
        }
    }

}
