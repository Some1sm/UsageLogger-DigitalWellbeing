using System;
using System.IO;

namespace DigitalWellbeingService.Helpers;

/// <summary>
/// Monitors user preferences for Incognito Mode state.
/// Uses file-based polling with caching to minimize disk reads.
/// </summary>
public class IncognitoMonitor
{
    private bool _incognitoMode;
    private DateTime _lastSettingsCheck = DateTime.MinValue;
    private DateTime _lastFileWriteTime = DateTime.MinValue;
    private static readonly TimeSpan SettingsCheckInterval = TimeSpan.FromSeconds(5);
    private readonly string _settingsPath;

    public IncognitoMonitor()
    {
        _settingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "digital-wellbeing",
            "user_preferences.json");
    }

    /// <summary>
    /// Returns current incognito mode state, checking file if interval has passed.
    /// </summary>
    public bool IsIncognitoMode
    {
        get
        {
            CheckSettings();
            return _incognitoMode;
        }
    }

    private void CheckSettings()
    {
        if ((DateTime.Now - _lastSettingsCheck) < SettingsCheckInterval) return;

        _lastSettingsCheck = DateTime.Now;
        try
        {
            if (!File.Exists(_settingsPath)) return;

            var currentWriteTime = File.GetLastWriteTime(_settingsPath);
            if (currentWriteTime == _lastFileWriteTime) return;
            _lastFileWriteTime = currentWriteTime;

            using var fs = new FileStream(_settingsPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var sr = new StreamReader(fs);
            string json = sr.ReadToEnd();

            _incognitoMode = json.Contains("\"IncognitoMode\": true", StringComparison.OrdinalIgnoreCase);
        }
        catch { /* Ignore settings read errors */ }
    }
}
