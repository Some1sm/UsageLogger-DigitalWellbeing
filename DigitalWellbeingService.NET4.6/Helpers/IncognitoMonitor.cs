using System;
using System.IO;

namespace DigitalWellbeingService.NET4._6.Helpers
{
    /// <summary>
    /// Monitors user preferences for Incognito Mode state.
    /// Uses file-based polling with caching to minimize disk reads.
    /// </summary>
    public class IncognitoMonitor
    {
        private bool _incognitoMode = false;
        private DateTime _lastSettingsCheck = DateTime.MinValue;
        private DateTime _lastFileWriteTime = DateTime.MinValue;
        private readonly TimeSpan _settingsCheckInterval = TimeSpan.FromSeconds(5);
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

        /// <summary>
        /// Polls settings file for changes (with caching).
        /// </summary>
        private void CheckSettings()
        {
            if ((DateTime.Now - _lastSettingsCheck) < _settingsCheckInterval) return;

            _lastSettingsCheck = DateTime.Now;
            try
            {
                if (!File.Exists(_settingsPath)) return;

                // Skip file read if file hasn't been modified since last check
                var currentWriteTime = File.GetLastWriteTime(_settingsPath);
                if (currentWriteTime == _lastFileWriteTime) return;
                _lastFileWriteTime = currentWriteTime;

                // Read file only when it has changed
                string json;
                using (var fs = new FileStream(_settingsPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var sr = new StreamReader(fs))
                {
                    json = sr.ReadToEnd();
                }

                _incognitoMode = json.Contains("\"IncognitoMode\": true");
            }
            catch { }
        }
    }
}
