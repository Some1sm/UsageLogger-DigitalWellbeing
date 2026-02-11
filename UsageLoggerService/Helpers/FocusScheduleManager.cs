#nullable enable
using System;
using System.Collections.Generic;
using System.IO;

namespace UsageLoggerService.Helpers;

/// <summary>
/// Manages focus schedule loading, persistence, and enforcement checking.
/// Extracted from ActivityLogger to separate focus-mode logic from usage logging.
/// </summary>
public class FocusScheduleManager
{
    private const int FOCUS_ALERT_DEBOUNCE_MINUTES = 5;

    private List<ServiceFocusSession> _focusSessions = new List<ServiceFocusSession>();
    private bool _focusMonitoringEnabled = false;
    private bool _focusLoaded = false;
    private DateTime _lastFocusFileWriteTime = DateTime.MinValue;
    private DateTime _lastSettingsTimeFocus = DateTime.MinValue;
    private readonly Dictionary<string, DateTime> _focusLastAlert = new Dictionary<string, DateTime>();

    private static readonly string _settingsPath = UsageLogger.Core.ApplicationPath.UserPreferencesFile;

    private static readonly HashSet<string> _whitelistedProcesses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "explorer", "taskmgr", "dwm", "csrss", "winlogon", "services", "lsass", "svchost",
        "ApplicationFrameHost", "ShellExperienceHost", "StartMenuExperienceHost", "SearchHost",
        "UsageLogger", "UsageLoggerService", "UsageLogger (Startup)", "LockApp"
    };

    /// <summary>
    /// Reloads the focus schedule data from disk (if changed) and checks enforcement.
    /// </summary>
    public void CheckFocusSchedules(string activeProcessName, string activeWindowTitle)
    {
        LoadFocusSchedule();
        if (!_focusMonitoringEnabled) return;
        if (_focusSessions.Count == 0) return;

        foreach (var session in _focusSessions)
        {
            if (!session.IsEnabled) continue;
            if (!session.IsActiveNow()) continue;

            bool isMatch = string.Equals(session.ProcessName, activeProcessName, StringComparison.OrdinalIgnoreCase);

            if (isMatch && !string.IsNullOrEmpty(session.ProgramName))
            {
                isMatch = activeWindowTitle.IndexOf(session.ProgramName, StringComparison.OrdinalIgnoreCase) >= 0;
            }

            if (!isMatch)
            {
                if (_whitelistedProcesses.Contains(activeProcessName))
                    continue;

                bool isUiRunning = System.Diagnostics.Process.GetProcessesByName("UsageLogger").Length > 0
                                || System.Diagnostics.Process.GetProcessesByName("UsageLogger (Startup)").Length > 0;

                if (!isUiRunning)
                {
                    if (session.Mode == 0) // Chill Notification
                    {
                        ServiceNotificationHelper.ShowToast(
                            $"Focus: {session.Name}",
                            $"You're using '{activeProcessName}' instead of '{session.ProcessName}'",
                            _focusLastAlert);
                    }
                    else if (session.Mode == 2) // Focus Mode (Strict Kill)
                    {
                        try
                        {
                            var procs = System.Diagnostics.Process.GetProcessesByName(activeProcessName);
                            foreach (var p in procs)
                            {
                                try { p.Kill(); } catch { }
                                p.Dispose();
                            }
                            ServiceNotificationHelper.ShowToast(
                                $"Focus: {session.Name}",
                                $"Closed '{activeProcessName}'",
                                _focusLastAlert);
                        }
                        catch { }
                    }
                    else // Blocking Popup (Normal Mode)
                    {
                        ShowFocusEnforcementPopup(session, activeProcessName);
                    }
                }
            }
        }
    }

    private void LoadFocusSchedule()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                DateTime settingsTime = File.GetLastWriteTime(_settingsPath);
                if (settingsTime != _lastSettingsTimeFocus)
                {
                    string json = File.ReadAllText(_settingsPath);
                    int idx = json.IndexOf("\"FocusMonitoringEnabled\":");
                    if (idx >= 0)
                    {
                        int colonIdx = json.IndexOf(':', idx);
                        int endIdx = json.IndexOf(',', colonIdx);
                        if (endIdx < 0) endIdx = json.IndexOf('}', colonIdx);
                        string valToken = json.Substring(colonIdx + 1, endIdx - colonIdx - 1).Trim().ToLowerInvariant();
                        _focusMonitoringEnabled = valToken == "true";
                    }
                    else
                    {
                        _focusMonitoringEnabled = false;
                    }
                    _lastSettingsTimeFocus = settingsTime;
                }
            }

            string focusPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "digital-wellbeing",
                "focus_schedule.json");

            if (File.Exists(focusPath))
            {
                DateTime currentWriteTime = File.GetLastWriteTime(focusPath);
                if (currentWriteTime != _lastFocusFileWriteTime || !_focusLoaded)
                {
                    string json = File.ReadAllText(focusPath);
                    _focusSessions = System.Text.Json.JsonSerializer.Deserialize<List<ServiceFocusSession>>(json)
                                     ?? new List<ServiceFocusSession>();
                    _lastFocusFileWriteTime = currentWriteTime;
                    _focusLoaded = true;
                }
            }
            else
            {
                _focusSessions.Clear();
                _focusLoaded = true;
            }
        }
        catch { }
    }

    private void SaveFocusSchedule()
    {
        try
        {
            string focusPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "digital-wellbeing",
                "focus_schedule.json");

            string? dir = Path.GetDirectoryName(focusPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

            string json = System.Text.Json.JsonSerializer.Serialize(_focusSessions,
                new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(focusPath, json);
        }
        catch { }
    }

    private void ShowFocusEnforcementPopup(ServiceFocusSession session, string violatingProcess)
    {
        string key = $"Focus_{session.Id}_{violatingProcess}";
        if (_focusLastAlert.TryGetValue(key, out DateTime lastAlert))
        {
            if ((DateTime.Now - lastAlert).TotalMinutes < FOCUS_ALERT_DEBOUNCE_MINUTES)
                return;
        }

        _focusLastAlert[key] = DateTime.Now;

        System.Threading.Tasks.Task.Run(() =>
        {
            try
            {
                TimeSpan endTimeSpan = session.StartTime + session.Duration;
                string endTime = endTimeSpan.ToString(@"hh\:mm");
                bool disableClicked = UsageLoggerService.UI.EnforcementPopup.ShowPopup(
                    session.Name ?? "Focus Session",
                    session.ProcessName ?? "Unknown App",
                    endTime
                );

                if (disableClicked)
                {
                    session.IsEnabled = false;
                    SaveFocusSchedule();
                }
            }
            catch { }
        });
    }
}

// ServiceFocusSession - mirrors the UI's FocusSession for JSON compatibility
public class ServiceFocusSession
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public TimeSpan StartTime { get; set; }
    public TimeSpan Duration { get; set; }
    public List<DayOfWeek> Days { get; set; } = [];
    public string ProcessName { get; set; } = string.Empty;
    public string ProgramName { get; set; } = string.Empty;
    public int Mode { get; set; } // 0=Chill, 1=Normal, 2=Focus
    public bool IsEnabled { get; set; }

    public bool IsActiveNow()
    {
        DateTime now = DateTime.Now;
        if (Days != null && Days.Count > 0 && !Days.Contains(now.DayOfWeek)) return false;

        TimeSpan currentTime = now.TimeOfDay;
        TimeSpan endTime = StartTime + Duration;

        if (endTime > TimeSpan.FromHours(24))
        {
            return currentTime >= StartTime || currentTime < (endTime - TimeSpan.FromHours(24));
        }
        return currentTime >= StartTime && currentTime < endTime;
    }
}
