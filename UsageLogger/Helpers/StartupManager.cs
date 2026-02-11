using UsageLogger.Core;
using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;

namespace UsageLogger.Helpers
{
    /// <summary>
    /// Manages application startup configuration (registry, shortcuts).
    /// Renamed from SettingsManager after tag/time-limit methods were moved to UserPreferences.
    /// </summary>
    public static class StartupManager
    {
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
                Debug.WriteLine($"[StartupManager] SetStartupMode called with: {mode}");
                
                RegistryKey key = Registry.CurrentUser.OpenSubKey(ApplicationPath.AUTORUN_REGPATH, true);
                if (key == null) 
                {
                    Debug.WriteLine($"[StartupManager] ERROR: Could not open registry key for writing");
                    return;
                }

                switch (mode)
                {
                    case StartupMode.None:
                        // First check if registry entry exists
                        var existingValue = key.GetValue(ApplicationPath.AUTORUN_REGKEY);
                        if (existingValue != null)
                        {
                            Debug.WriteLine($"[StartupManager] Deleting registry key: {ApplicationPath.AUTORUN_REGKEY}");
                            key.DeleteValue(ApplicationPath.AUTORUN_REGKEY, false);
                            
                            // Verify deletion
                            var afterDelete = key.GetValue(ApplicationPath.AUTORUN_REGKEY);
                            if (afterDelete != null)
                            {
                                Debug.WriteLine($"[StartupManager] WARNING: Key still exists after deletion!");
                            }
                            else
                            {
                                Debug.WriteLine($"[StartupManager] Successfully deleted startup entry");
                            }
                        }
                        else
                        {
                            Debug.WriteLine($"[StartupManager] No startup registry entry to delete");
                        }
                        
                        // Also cleanup any legacy startup folder shortcuts (from old installer versions)
                        CleanupStartupShortcuts();
                        break;

                    case StartupMode.TrackerOnly:
                        // Set to Service executable
                        string servicePath = GetServicePath();
                        if (!string.IsNullOrEmpty(servicePath))
                        {
                            key.SetValue(ApplicationPath.AUTORUN_REGKEY, servicePath);
                            Debug.WriteLine($"[StartupManager] Set startup to service: {servicePath}");
                        }
                        break;

                    case StartupMode.TrackerAndUI:
                        // Set to UI executable (which also starts Service)
                        using (Process process = Process.GetCurrentProcess())
                        {
                            key.SetValue(ApplicationPath.AUTORUN_REGKEY, process.MainModule.FileName);
                            Debug.WriteLine($"[StartupManager] Set startup to UI: {process.MainModule.FileName}");
                        }
                        break;
                }
                
                key.Close();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[StartupManager] SetStartupMode error: {ex.Message}");
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

        /// <summary>
        /// Removes any startup folder shortcuts that may have been created by old installer versions.
        /// This ensures that disabling startup in Settings truly disables all startup methods.
        /// </summary>
        private static void CleanupStartupShortcuts()
        {
            string[] shortcutPaths = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), "UsageLogger.lnk"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), "UsageLogger Service.lnk"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), "DigitalWellbeing.lnk"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), "DigitalWellbeing Service.lnk")
            };

            foreach (var path in shortcutPaths)
            {
                if (File.Exists(path))
                {
                    try 
                    { 
                        File.Delete(path);
                        Debug.WriteLine($"[StartupManager] Deleted startup shortcut: {path}");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[StartupManager] Failed to delete shortcut {path}: {ex.Message}");
                    }
                }
            }
        }
    }
}
