using System;
using System.IO;
using static System.Environment;

namespace DigitalWellbeing.Core;

/// <summary>
/// Provides centralized paths for application data storage.
/// </summary>
public static class ApplicationPath
{
    private static readonly SpecialFolder ApplicationPathFolder = SpecialFolder.LocalApplicationData;
    private const string ApplicationFolderName = "digital-wellbeing";
    private const string ImageCacheFolderName = "processicons";
    private const string DailyLogsFolderName = "dailylogs";
    private const string InternalLogsFolderName = "internal-logs";
    private const string SettingsFolderName = "settings";
    private const string AutorunFileName = ".autorun";
    private const string CustomLogPathFileName = "custom_log_path.txt";

    public static readonly string AUTORUN_REGPATH = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";

#if DEBUG
    public static readonly string AUTORUN_REGKEY = "DigitalWellbeingWPFDEBUG";
#else
    public static readonly string AUTORUN_REGKEY = "DigitalWellbeingWPF";
#endif

    public static string APP_LOCATION =>
#if DEBUG
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DigitalWellbeingDebug");
#else
        Path.Combine(GetFolderPath(ApplicationPathFolder), ApplicationFolderName);
#endif

    public static string autorunFilePath => Path.Combine(APP_LOCATION, AutorunFileName);

    public static string UsageLogsFolder
    {
        get
        {
            string customPathFile = Path.Combine(APP_LOCATION, CustomLogPathFileName);
            if (File.Exists(customPathFile))
            {
                try
                {
                    string customPath = File.ReadAllText(customPathFile).Trim();
                    if (!string.IsNullOrEmpty(customPath) && Directory.Exists(customPath))
                    {
                        return customPath.EndsWith(Path.DirectorySeparatorChar.ToString()) 
                            ? customPath 
                            : customPath + Path.DirectorySeparatorChar;
                    }
                }
                catch { /* Fallback to default */ }
            }
            return Path.Combine(APP_LOCATION, DailyLogsFolderName) + Path.DirectorySeparatorChar;
        }
    }

    public static void SetCustomLogsFolder(string? path)
    {
        string customPathFile = Path.Combine(APP_LOCATION, CustomLogPathFileName);
        Directory.CreateDirectory(APP_LOCATION);
        File.WriteAllText(customPathFile, path ?? "");
    }

    public static void ClearCustomLogsFolder()
    {
        string customPathFile = Path.Combine(APP_LOCATION, CustomLogPathFileName);
        if (File.Exists(customPathFile))
            File.Delete(customPathFile);
    }

    public static string? GetCustomLogsFolderRaw()
    {
        string customPathFile = Path.Combine(APP_LOCATION, CustomLogPathFileName);
        if (File.Exists(customPathFile))
        {
            try
            {
                return File.ReadAllText(customPathFile).Trim();
            }
            catch { /* Return null */ }
        }
        return null;
    }

    public static string SettingsFolder => Path.Combine(APP_LOCATION, SettingsFolderName) + Path.DirectorySeparatorChar;

    public static string InternalLogsFolder => Path.Combine(APP_LOCATION, InternalLogsFolderName) + Path.DirectorySeparatorChar;

    public static string GetImageCacheLocation(string appName = "")
    {
        string location = Path.Combine(APP_LOCATION, ImageCacheFolderName) + Path.DirectorySeparatorChar;
        if (!string.IsNullOrEmpty(appName))
        {
            location = Path.Combine(location, $"{appName}.ico");
        }
        return location;
    }

    public static string GetCustomIconsLocation() => Path.Combine(APP_LOCATION, "CustomIcons") + Path.DirectorySeparatorChar;
}

