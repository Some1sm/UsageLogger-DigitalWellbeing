#nullable enable
using System;
using System.IO;
using static System.Environment;

namespace UsageLogger.Core;

/// <summary>
/// Provides centralized paths for application data storage.
/// </summary>
public static class ApplicationPath
{
    private const SpecialFolder ApplicationPathFolder = SpecialFolder.LocalApplicationData;
    private const string ApplicationFolderName = "UsageLoggerData"; // Separated from Install Folder
    private const string LegacyFolderName = "digital-wellbeing"; 
    // We also check "usagelogger" in case user migrated to mixed folder previously
    private const string MixedFolderName = "usagelogger"; 

    // ... (Lines 16-246 same) ...

    /// <summary>
    /// Migrates data from legacy folders ('digital-wellbeing', 'usagelogger') to 'UsageLoggerData'.
    /// </summary>
    public static void MigrateLegacyData()
    {
#if !DEBUG
        try
        {
            string newPath = APP_LOCATION;
            if (Directory.Exists(newPath)) return; // Already migrated/setup

            var sources = new[] 
            {
                Path.Combine(GetFolderPath(ApplicationPathFolder), LegacyFolderName),
                Path.Combine(GetFolderPath(ApplicationPathFolder), MixedFolderName),
                Path.Combine(GetFolderPath(ApplicationPathFolder), LegacyFolderName + ".bak"), // Auto-recover from .bak
                Path.Combine(GetFolderPath(ApplicationPathFolder), MixedFolderName + ".bak")
            };

            foreach (var legacyPath in sources)
            {
                if (!Directory.Exists(legacyPath)) continue;

                try 
                {
                    // Copy ONLY Data (in case legacy is Mixed App+Data)
                    string[] foldersToCopy = { "dailylogs", "settings", "Icons", "CustomIcons", "Debug" };
                    string[] filesToCopy = { "user_preferences.json", "custom_log_path.txt", "known_apps.json" };

                    bool foundData = false;

                    // 1. Copy Folders
                    bool allCopied = true;
                    foreach (var dirName in foldersToCopy)
                    {
                        string srcDir = Path.Combine(legacyPath, dirName);
                        if (Directory.Exists(srcDir))
                        {
                            foundData = true;
                            string destDir = Path.Combine(newPath, dirName);
                            Directory.CreateDirectory(destDir);
                            
                            // Recursive copy using Relative Path
                            foreach (string dirPath in Directory.GetDirectories(srcDir, "*", SearchOption.AllDirectories))
                            {
                                string relative = Path.GetRelativePath(srcDir, dirPath);
                                Directory.CreateDirectory(Path.Combine(destDir, relative));
                            }
                            foreach (string filePath in Directory.GetFiles(srcDir, "*.*", SearchOption.AllDirectories))
                            {
                                try
                                {
                                    string relative = Path.GetRelativePath(srcDir, filePath);
                                    File.Copy(filePath, Path.Combine(destDir, relative), true);
                                }
                                catch (Exception copyEx) 
                                { 
                                    allCopied = false; 
                                    System.Diagnostics.Debug.WriteLine($"[Migration] Copy failed for {filePath}: {copyEx.Message}");
                                }
                            }
                        }
                    }

                    // 2. Copy Config Files
                    Directory.CreateDirectory(newPath); // Ensure root exists
                    foreach (var fileName in filesToCopy)
                    {
                        string srcFile = Path.Combine(legacyPath, fileName);
                        if (File.Exists(srcFile))
                        {
                            foundData = true;
                            try 
                            { 
                                File.Copy(srcFile, Path.Combine(newPath, fileName), true); 
                            }
                            catch { allCopied = false; }
                        }
                    }

                    // 3. DO NOT RENAME Source. Leaving it intact as per user request.
                    // string backupPath = legacyPath + ".bak"; ... Directory.Move ...
                    
                    if (foundData && allCopied) 
                    {
                         System.Diagnostics.Debug.WriteLine($"[Migration] Migrated data from '{legacyPath}' to '{newPath}' (Source preserved)");
                         break; // Found primary data source, stop looking
                    }
                    else if (foundData && !allCopied)
                    {
                        System.Diagnostics.Debug.WriteLine($"[Migration] Data found in '{legacyPath}' but copy incomplete. Source preserved.");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[Migration] Error processing '{legacyPath}': {ex.Message}");
                }
            }

            // Sanitization: If custom log path still points to "digital-wellbeing" or "usagelogger", reset
            try
            {
                 string? customPath = GetCustomLogsFolderRaw();
                 if (!string.IsNullOrEmpty(customPath))
                 {
                     if (customPath.IndexOf("digital-wellbeing", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         customPath.IndexOf("usagelogger", StringComparison.OrdinalIgnoreCase) >= 0)
                     {
                         ClearCustomLogsFolder();
                     }
                 }
            }
            catch { }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Migration] Data migration failed: {ex.Message}");
        }
#endif
    }
    private const string SettingsFolderName = "settings";
    private const string IconsFolderName = "Icons";
    private const string DebugFolderName = "Debug";
    private const string ImageCacheFolderName = "processicons";
    private const string CustomIconsFolderName = "CustomIcons";
    private const string DailyLogsFolderName = "dailylogs";
    private const string InternalLogsFolderName = "internal-logs";
    private const string AutorunFileName = ".autorun";
    private const string CustomLogPathFileName = "custom_log_path.txt";
    private const string UserPreferencesFileName = "user_preferences.json";
    private const string ServiceDebugFileName = "service_debug.log";
    private const string TitleTagDebugFileName = "debug_titletag.log";

    public static readonly string AUTORUN_REGPATH = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";

#if DEBUG
    public static readonly string AUTORUN_REGKEY = "UsageLoggerDEBUG";
    private const string LEGACY_AUTORUN_REGKEY = "DigitalWellbeingWPFDEBUG";
#else
    public static readonly string AUTORUN_REGKEY = "UsageLogger";
    private const string LEGACY_AUTORUN_REGKEY = "DigitalWellbeingWPF";
#endif

    public static string APP_LOCATION =>
#if DEBUG
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "UsageLoggerDebug");
#else
        Path.Combine(GetFolderPath(ApplicationPathFolder), ApplicationFolderName);
#endif

    public static string autorunFilePath => Path.Combine(APP_LOCATION, AutorunFileName);

    public static string UserPreferencesFile
    {
        get
        {
            string newPath = Path.Combine(SettingsFolder, UserPreferencesFileName);
            string oldPath = Path.Combine(APP_LOCATION, UserPreferencesFileName);

            if (!File.Exists(newPath) && File.Exists(oldPath))
            {
                try
                {
                    Directory.CreateDirectory(SettingsFolder);
                    File.Move(oldPath, newPath, true);
                }
                catch { }
            }
            return newPath;
        }
    }

    private static string CustomLogPathFile
    {
        get
        {
            string newPath = Path.Combine(SettingsFolder, CustomLogPathFileName);
            string oldPath = Path.Combine(APP_LOCATION, CustomLogPathFileName);

            if (!File.Exists(newPath) && File.Exists(oldPath))
            {
                try
                {
                    Directory.CreateDirectory(SettingsFolder);
                    File.Move(oldPath, newPath, true);
                }
                catch { }
            }
            return newPath;
        }
    }

    public static string UsageLogsFolder
    {
        get
        {
            try
            {
                string customPathFile = CustomLogPathFile;
                if (File.Exists(customPathFile))
                {
                    string customPath = File.ReadAllText(customPathFile).Trim();
                    if (!string.IsNullOrEmpty(customPath) && Directory.Exists(customPath))
                    {
                        return customPath.EndsWith(Path.DirectorySeparatorChar.ToString()) 
                            ? customPath 
                            : customPath + Path.DirectorySeparatorChar;
                    }
                }
            }
            catch { /* Fallback to default */ }

            return Path.Combine(APP_LOCATION, DailyLogsFolderName) + Path.DirectorySeparatorChar;
        }
    }

    public static void SetCustomLogsFolder(string? path)
    {
        string customPathFile = CustomLogPathFile;
        Directory.CreateDirectory(SettingsFolder);
        File.WriteAllText(customPathFile, path ?? "");
    }

    public static void ClearCustomLogsFolder()
    {
        string customPathFile = CustomLogPathFile;
        if (File.Exists(customPathFile))
            File.Delete(customPathFile);
    }

    public static string? GetCustomLogsFolderRaw()
    {
        string customPathFile = CustomLogPathFile;
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

    public static string IconsFolder => Path.Combine(APP_LOCATION, IconsFolderName) + Path.DirectorySeparatorChar;

    public static string DebugFolder => Path.Combine(APP_LOCATION, DebugFolderName) + Path.DirectorySeparatorChar;

    public static string InternalLogsFolder
    {
        get
        {
            string newLocation = Path.Combine(DebugFolder, InternalLogsFolderName) + Path.DirectorySeparatorChar;
            string oldLocation = Path.Combine(APP_LOCATION, InternalLogsFolderName) + Path.DirectorySeparatorChar;

            if (!Directory.Exists(newLocation) && Directory.Exists(oldLocation))
            {
                try
                {
                    Directory.CreateDirectory(DebugFolder);
                    Directory.Move(oldLocation.TrimEnd(Path.DirectorySeparatorChar), newLocation.TrimEnd(Path.DirectorySeparatorChar));
                }
                catch { }
            }
            return newLocation;
        }
    }

    public static string ServiceDebugFile
    {
        get
        {
            string newPath = Path.Combine(DebugFolder, ServiceDebugFileName);
            string oldPath = Path.Combine(APP_LOCATION, ServiceDebugFileName);

            if (!File.Exists(newPath) && File.Exists(oldPath))
            {
                try
                {
                    Directory.CreateDirectory(DebugFolder);
                    File.Move(oldPath, newPath, true);
                }
                catch { }
            }
            return newPath;
        }
    }

    public static string TitleTagDebugFile
    {
        get
        {
            string newPath = Path.Combine(DebugFolder, TitleTagDebugFileName);
            string oldPath = Path.Combine(APP_LOCATION, TitleTagDebugFileName);

            if (!File.Exists(newPath) && File.Exists(oldPath))
            {
                try
                {
                    Directory.CreateDirectory(DebugFolder);
                    File.Move(oldPath, newPath, true);
                }
                catch { }
            }
            return newPath;
        }
    }

    public static string GetImageCacheLocation(string appName = "")
    {
        string newLocation = Path.Combine(IconsFolder, ImageCacheFolderName) + Path.DirectorySeparatorChar;
        string oldLocation = Path.Combine(APP_LOCATION, ImageCacheFolderName) + Path.DirectorySeparatorChar;

        if (!Directory.Exists(newLocation) && Directory.Exists(oldLocation))
        {
            try
            {
                Directory.CreateDirectory(IconsFolder);
                Directory.Move(oldLocation.TrimEnd(Path.DirectorySeparatorChar), newLocation.TrimEnd(Path.DirectorySeparatorChar));
            }
            catch { }
        }

        if (!string.IsNullOrEmpty(appName))
        {
            return Path.Combine(newLocation, $"{appName}.ico");
        }
        return newLocation;
    }

    public static string GetCustomIconsLocation()
    {
        string newLocation = Path.Combine(IconsFolder, CustomIconsFolderName) + Path.DirectorySeparatorChar;
        string oldLocation = Path.Combine(APP_LOCATION, CustomIconsFolderName) + Path.DirectorySeparatorChar;

        if (!Directory.Exists(newLocation) && Directory.Exists(oldLocation))
        {
            try
            {
                Directory.CreateDirectory(IconsFolder);
                Directory.Move(oldLocation.TrimEnd(Path.DirectorySeparatorChar), newLocation.TrimEnd(Path.DirectorySeparatorChar));
            }
            catch { }
        }

        return newLocation;
    }



    /// <summary>
    /// Removes the legacy autorun registry key if present.
    /// Should be called once at application startup.
    /// </summary>
    public static void CleanupLegacyRegistry()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(AUTORUN_REGPATH, writable: true);
            if (key?.GetValue(LEGACY_AUTORUN_REGKEY) != null)
            {
                key.DeleteValue(LEGACY_AUTORUN_REGKEY, throwOnMissingValue: false);
                System.Diagnostics.Debug.WriteLine($"[Migration] Removed legacy registry key: {LEGACY_AUTORUN_REGKEY}");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Migration] Registry cleanup failed: {ex.Message}");
        }
    }
}

