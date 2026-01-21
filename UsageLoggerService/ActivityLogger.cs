#nullable enable
using UsageLogger.Core;
using UsageLogger.Core.Data;
using UsageLogger.Core.Interfaces;
using UsageLogger.Core.Models;
using UsageLoggerService.Helpers;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using static System.Environment;

namespace UsageLoggerService;

public class ActivityLogger
{
    // Timer Constants
    public static readonly int TIMER_INTERVAL_SEC = 2; // Increased frequency for better responsiveness
    private const int DEFAULT_BUFFER_FLUSH_INTERVAL_SEC = 300; // 5 minutes
    private const int IMMEDIATE_FLUSH_INTERVAL_SEC = 1;
    private const int SETTINGS_THROTTLE_SEC = 30;
    private const int MIN_IDLE_THRESHOLD_SEC = 60;
    private const int MIN_FLUSH_INTERVAL_SEC = 60;
    
    private static int _bufferFlushIntervalSec = DEFAULT_BUFFER_FLUSH_INTERVAL_SEC;
    private static List<string> _ignoredWindowTitles = new List<string>();
    private static List<CustomTitleRule> _customTitleRules = new List<CustomTitleRule>();
    private static DateTime _lastSettingsRead = DateTime.MinValue;
    private static DateTime _lastFileWriteTime = DateTime.MinValue;
    private static readonly string _settingsPath = ApplicationPath.UserPreferencesFile;

    // Focus Schedule Fields
    private static List<ServiceFocusSession> _focusSessions = new List<ServiceFocusSession>();
    private static bool _focusMonitoringEnabled = false;
    private static bool _focusLoaded = false;
    private static Dictionary<string, DateTime> _focusLastAlert = new Dictionary<string, DateTime>();
    private const int FOCUS_ALERT_DEBOUNCE_MINUTES = 5;
    private const int TOAST_DEBOUNCE_MINUTES = 1;

    /// <summary>
    /// Parses a simple JSON property value from a JSON string.
    /// </summary>
    private static string? ParseJsonPropertyRaw(string json, string propertyName)
    {
        int idx = json.IndexOf($"\"{propertyName}\":");
        if (idx < 0) return null;
        
        int colonIdx = json.IndexOf(':', idx);
        int endIdx = json.IndexOf(',', colonIdx);
        if (endIdx < 0) endIdx = json.IndexOf('}', colonIdx);
        if (endIdx < 0) return null;
        
        return json.Substring(colonIdx + 1, endIdx - colonIdx - 1).Trim();
    }

    private static bool ParseJsonBool(string json, string propertyName, bool defaultValue)
    {
        string? value = ParseJsonPropertyRaw(json, propertyName);
        if (value == null) return defaultValue;
        return value.ToLowerInvariant() == "true";
    }

    private static int ParseJsonInt(string json, string propertyName, int defaultValue, int minValue = int.MinValue)
    {
        string? value = ParseJsonPropertyRaw(json, propertyName);
        if (value == null) return defaultValue;
        if (int.TryParse(value, out int result) && result >= minValue)
            return result;
        return defaultValue;
    }

    private int GetBufferFlushInterval()
    {
        // Check file timestamp every loop (cheap) to detect changes instantly
        if (File.Exists(_settingsPath))
        {
            var currentWriteTime = File.GetLastWriteTime(_settingsPath);
            if (currentWriteTime != _lastFileWriteTime)
            {
                // File changed! Force read immediately (bypass 30s throttle)
                _lastFileWriteTime = currentWriteTime;
                _lastSettingsRead = DateTime.Now; // Update read time so we don't re-read in the throttle block if we just read it
                
                // Continue to read logic...
            }
            else
            {
                 // File hasn't changed. Check if we should re-read anyway based on timer?
                 // No, if file hasn't changed, no need to read.
                 // BUT we still need to respect the 30s throttle for other reasons? 
                 // Actually, if file hasn't changed, we NEVER need to read it.
                 // So we can just return cached value.
                 
                 // However, to be safe and mimicking old behavior (maybe external change without timestamp change? unlikely):
                 if ((DateTime.Now - _lastSettingsRead).TotalSeconds < SETTINGS_THROTTLE_SEC)
                     return _bufferFlushIntervalSec;
                 
                 _lastSettingsRead = DateTime.Now;
            }
        }
        else
        {
             if ((DateTime.Now - _lastSettingsRead).TotalSeconds < SETTINGS_THROTTLE_SEC)
                return _bufferFlushIntervalSec;
             _lastSettingsRead = DateTime.Now;
        }

        try
        {
            if (!File.Exists(_settingsPath)) return _bufferFlushIntervalSec;

            // _lastFileWriteTime is already updated above if changed
            
            string json;

            using (var fs = new FileStream(_settingsPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var sr = new StreamReader(fs))
            {
                json = sr.ReadToEnd();
            }

            // Check UseRamCache first (if false, flush every 1 second for immediate writes)
            bool useRamCache = ParseJsonBool(json, "UseRamCache", true);
            if (!useRamCache)
            {
                _bufferFlushIntervalSec = IMMEDIATE_FLUSH_INTERVAL_SEC;
                return _bufferFlushIntervalSec;
            }

            // Default: use DataFlushIntervalSeconds for RAM cache mode
            _bufferFlushIntervalSec = ParseJsonInt(json, "DataFlushIntervalSeconds", DEFAULT_BUFFER_FLUSH_INTERVAL_SEC, MIN_FLUSH_INTERVAL_SEC);
            
            // Parse IgnoredWindowTitles array
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("IgnoredWindowTitles", out var titlesElement) && 
                    titlesElement.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    _ignoredWindowTitles = titlesElement.EnumerateArray()
                        .Where(e => e.ValueKind == System.Text.Json.JsonValueKind.String)
                        .Select(e => e.GetString() ?? "")
                        .Where(s => !string.IsNullOrEmpty(s))
                        .ToList();
                }
            }
            catch (System.Text.Json.JsonException ex)
            {
                ServiceLogger.Log("Settings", $"Failed to parse IgnoredWindowTitles: {ex.Message}");
            }

            // Parse CustomTitleRules array
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("CustomTitleRules", out var rulesElement) && 
                    rulesElement.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    _customTitleRules = System.Text.Json.JsonSerializer.Deserialize<List<CustomTitleRule>>(rulesElement.GetRawText()) 
                                        ?? new List<CustomTitleRule>();
                }
            }
            catch (System.Text.Json.JsonException ex)
            {
                ServiceLogger.Log("Settings", $"Failed to parse CustomTitleRules: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            ServiceLogger.Log("Settings", $"Failed to read settings: {ex.Message}");
        }

        return _bufferFlushIntervalSec;
    }
    
    /// <summary>
    /// Checks if a window title should be hidden (merged into parent process).
    /// </summary>
    private bool ShouldHideSubApp(string? windowTitle)
    {
        if (string.IsNullOrEmpty(windowTitle) || _ignoredWindowTitles.Count == 0)
            return false;
            
        foreach (var keyword in _ignoredWindowTitles)
        {
            if (windowTitle.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }
        return false;
    }

    private string folderPath = string.Empty;
    private string autoRunFilePath = string.Empty;

    private readonly IAppUsageRepository _repository;
    private List<AppUsage> _cachedUsage = [];
    private DateTime _lastFlushTime;
    private readonly SessionManager _sessionManager;
    private bool _isLocked = false; // Track workstation lock/unlock state
    private static int _idleThresholdSec = 300; // AFK threshold (default 5 min), updated from settings
    
    // Extracted Components
    private readonly AudioUsageTracker _audioTracker;
    private readonly IncognitoMonitor _incognitoMonitor;

    /// <summary>
    /// Gets the idle threshold in milliseconds, reading from settings if available.
    /// </summary>
    private int GetIdleThresholdMs()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                string json;
                using (var fs = new FileStream(_settingsPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var sr = new StreamReader(fs))
                {
                    json = sr.ReadToEnd();
                }

                _idleThresholdSec = ParseJsonInt(json, "IdleThresholdSeconds", _idleThresholdSec, MIN_IDLE_THRESHOLD_SEC);
            }
        }
        catch (Exception ex)
        {
            ServiceLogger.Log("Settings", $"Failed to read idle threshold: {ex.Message}");
        }

        return _idleThresholdSec * 1000; // Convert to ms
    }


    public ActivityLogger(IAppUsageRepository repository, SessionManager sessionManager)
    {
        folderPath = ApplicationPath.UsageLogsFolder;
        autoRunFilePath = ApplicationPath.autorunFilePath;
        
        _repository = repository;
        _sessionManager = sessionManager;
        
        // Initialize extracted components
        _audioTracker = new AudioUsageTracker();
        _incognitoMonitor = new IncognitoMonitor();
        
        Debug.WriteLine(folderPath);
        Debug.WriteLine(autoRunFilePath);

        TryCreateAutoRunFile();
        
        // Initialize with empty list, actual load happens in InitializeAsync
        _cachedUsage = [];
        _lastFlushTime = DateTime.Now;

        // Subscribe to Session events (Lock/Unlock) - wrapped in try-catch for console app compatibility
        try
        {
            Microsoft.Win32.SystemEvents.SessionSwitch += OnSessionSwitch;
            Helpers.ServiceLogger.Log("ActivityLogger", "SessionSwitch event subscribed successfully.");
        }
        catch (Exception ex)
        {
            Helpers.ServiceLogger.LogError("SessionSwitch", ex);
            // Continue without session events - AFK detection will still work
        }
    }

    private void OnSessionSwitch(object sender, Microsoft.Win32.SessionSwitchEventArgs e)
    {
        if (e.Reason == Microsoft.Win32.SessionSwitchReason.SessionLock)
        {
            _isLocked = true;
            Helpers.ServiceLogger.Log("SessionSwitch", "Workstation LOCKED");
        }
        else if (e.Reason == Microsoft.Win32.SessionSwitchReason.SessionUnlock)
        {
            _isLocked = false;
            Helpers.ServiceLogger.Log("SessionSwitch", "Workstation UNLOCKED");
        }
    }

    /// <summary>
    /// Async initialization - call before starting the timer loop.
    /// </summary>
    public async Task InitializeAsync()
    {
        _cachedUsage = await _repository.GetUsageForDateAsync(DateTime.Now);
    }

    // AutoRun only
    private void TryCreateAutoRunFile()
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(ApplicationPath.AUTORUN_REGPATH);

        bool isAutoRun = key?.GetValue(ApplicationPath.AUTORUN_REGKEY) != null;

        // Create an empty file that UI will check, do startup things (like hiding window) and delete.
        if (isAutoRun) File.Create(autoRunFilePath).Dispose();
    }

    // Main Timer Logic
    public async Task OnTimerAsync()
    {
        // Refresh settings cache (includes IgnoredWindowTitles and IdleThreshold)
        GetBufferFlushInterval();

        // Check for manual flush request from UI
        if (UsageLogger.Core.Helpers.LiveSessionCache.CheckAndClearFlushRequest())
        {
            ServiceLogger.Log("Service", "Manual flush requested via IPC.");
            await FlushBufferAsync();
        }
        
        // --- Priority 1: Locked Screen ---
        if (_isLocked)
        {
            await UpdateTimeEntryAsync("LogonUI", "Windows Lock");
            _sessionManager.Update("LogonUI", "Windows Lock", new List<string>());
            return;
        }

        // --- Priority 2: AFK (Idle Timeout) - but NOT if media is playing ---
        uint idleMs = Helpers.UserInputInfo.GetIdleTime();
        int idleThresholdMs = GetIdleThresholdMs();
        if (idleMs > idleThresholdMs)
        {
            // Check if audio is playing (e.g., watching a video without mouse/keyboard input)
            if (!Helpers.AudioSessionTracker.IsGlobalAudioPlaying())
            {
                await UpdateTimeEntryAsync("Away", "AFK");
                _sessionManager.Update("Away", "AFK", new List<string>());
                return;
            }
            // Audio is playing, so we consider the user "engaged" - fall through to Active App
        }

        // --- Priority 3: Active App ---
        // Use fallback-aware method instead of direct GetForegroundWindow
        IntPtr handle = ForegroundWindowManager.GetActiveWindowHandle();
        uint currProcessId = ForegroundWindowManager.GetForegroundProcessId(handle);
        
        // Handle PID 0 (System Idle / Desktop / No foreground window)
        if (currProcessId == 0)
        {
            // Log as "System Idle" rather than failing
            await UpdateTimeEntryAsync("System Idle", "Desktop");
            _sessionManager.Update("System Idle", "Desktop", new List<string>());
            return;
        }
        
        // Handle edge case where handle is invalid or process exited
        // Use using block to properly dispose Process handle and prevent leaks
        Process? proc = null;
        try 
        {
             proc = Process.GetProcessById((int)currProcessId);
        }
        catch (Exception ex)
        {
            Helpers.ServiceLogger.Log("OnTimer", $"GetProcessById({currProcessId}) failed: {ex.Message}");
            return;
        }

        // Wrap all process usage in try-finally to ensure disposal
        try
        {
            // Calculate Program Name (Window Title)
            string programName = "";
            string processName = proc.ProcessName;
            
            // Use extracted Incognito Monitor
            if (_incognitoMonitor.IsIncognitoMode)
            {
                // In Incognito, we SKIP window title parsing.
                // ProgramName becomes the ProcessName (or pretty version)
                programName = processName; 
            }
            else
            {
                try 
                { 
                    string? rawTitle = ForegroundWindowManager.GetWindowTitle(handle);
                    
                    // Check Hidden SubApps filter FIRST (on raw title)
                    // If keyword matches, skip parsing and use process name
                    if (ShouldHideSubApp(rawTitle))
                    {
                        programName = processName;
                    }
                    // Fallback to Product Name if Title is empty
                    else if (string.IsNullOrWhiteSpace(rawTitle))
                    {
                        programName = ForegroundWindowManager.GetActiveProgramName(proc) ?? processName;
                    }
                    else
                    {
                        programName = UsageLogger.Core.Helpers.WindowTitleParser.Parse(processName, rawTitle ?? "", _customTitleRules);
                    }
                } 
                catch {}
            }
            
            if (string.IsNullOrEmpty(programName)) programName = "";

            await UpdateTimeEntryAsync(processName, programName);
            
            // Audio Tracking with Persistence (Debounce) - use extracted tracker
            var currentAudioApps = AudioSessionTracker.GetActiveAudioSessions();
            
            // Fallback: If no specific sessions found, check global audio peak
            if (currentAudioApps.Count == 0 && AudioSessionTracker.IsGlobalAudioPlaying())
            {
                // Heuristic: If audio is playing but we can't identify the source,
                // assume it's the foreground app (e.g., exclusive mode games, hidden sessions)
                if (!string.IsNullOrEmpty(processName))
                {
                    currentAudioApps.Add(processName);
                }
            }
            
            var validAudioApps = _audioTracker.UpdatePersistence(currentAudioApps);

            _sessionManager.Update(processName, programName, validAudioApps);
            
            // Check Focus Schedule enforcement
            CheckFocusSchedules(processName, programName);
        }
        finally
        {
            proc?.Dispose();
        }
    }

    private async Task UpdateTimeEntryAsync(string processName, string programName)
    {
        if (string.IsNullOrEmpty(processName)) return;

        // Find in cache
        var existingEntry = _cachedUsage.FirstOrDefault(u => u.ProcessName == processName);

        if (existingEntry != null)
        {
            existingEntry.Duration = existingEntry.Duration.Add(TimeSpan.FromSeconds(TIMER_INTERVAL_SEC));
        }
        else
        {
            // New entry
            // Fallback / Cleanup
            if (string.IsNullOrEmpty(programName)) programName = "";

            _cachedUsage.Add(new AppUsage(processName, programName, TimeSpan.FromSeconds(TIMER_INTERVAL_SEC)));
        }

        // Check if we should flush to disk
        if ((DateTime.Now - _lastFlushTime).TotalSeconds >= GetBufferFlushInterval())
        {
            await FlushBufferAsync();
        }
    }

    private async Task FlushBufferAsync()
    {
        try
        {
            DateTime now = DateTime.Now;

            // Check if day changed since last flush (or initial load)
            if (now.Date > _lastFlushTime.Date)
            {
                // Flush accumulated data to the OLD date
                // Consolidated: We no longer write summary logs here.
                // Summary is reconstructed from sessions in the repository.
                // await _repository.UpdateUsageAsync(_lastFlushTime.Date, _cachedUsage);
                
                // Clear cache for the new day
                _cachedUsage.Clear();
            }
            else
            {
                // Same day, update normal log
                // Consolidated: We no longer write summary logs here.
                // await _repository.UpdateUsageAsync(now, _cachedUsage);
            }

            _lastFlushTime = now;
            await _sessionManager.FlushBufferAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to flush buffer: {ex.Message}");
        }
    }
    
    public async Task SaveOnExitAsync()
    {
        // Unsubscribe from session events to prevent memory leaks
        Microsoft.Win32.SystemEvents.SessionSwitch -= OnSessionSwitch;
        
        await FlushBufferAsync();
        await _sessionManager.SaveOnExitAsync();
    }

    // ========== Focus Schedule Methods ==========
    
    private DateTime _lastFocusFileWriteTime = DateTime.MinValue;

    private DateTime _lastSettingsTimeFocus = DateTime.MinValue;

    private void LoadFocusSchedule()
    {
        try
        {
            // Check if Focus Monitoring is enabled
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
                        // Default to false if missing
                       _focusMonitoringEnabled = false;
                    }
                    _lastSettingsTimeFocus = settingsTime;
                }
            }

            // Load Focus Schedule
            string focusPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "digital-wellbeing",
                "focus_schedule.json");

            if (File.Exists(focusPath))
            {
                // Check timestamp to avoid redundant reads
                DateTime currentWriteTime = File.GetLastWriteTime(focusPath);
                if (currentWriteTime != _lastFocusFileWriteTime || !_focusLoaded)
                {
                    string json = File.ReadAllText(focusPath);
                    _focusSessions = System.Text.Json.JsonSerializer.Deserialize<List<ServiceFocusSession>>(json) 
                                     ?? new List<ServiceFocusSession>();
                    
                    _lastFocusFileWriteTime = currentWriteTime;
                    _focusLoaded = true;
                    // Debug.WriteLine($"Reloaded Focus Schedule. Count: {_focusSessions.Count}");
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

    private static readonly HashSet<string> _whitelistedProcesses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "explorer",
        "taskmgr",
        "dwm",
        "csrss",
        "winlogon",
        "services",
        "lsass",
        "svchost",
        "ApplicationFrameHost",
        "ShellExperienceHost",
        "StartMenuExperienceHost",
        "SearchHost",
        "UsageLogger",
        "UsageLoggerService",
        "UsageLogger",
        "UsageLogger (Startup)",
        "LockApp"
    };

    public void CheckFocusSchedules(string activeProcessName, string activeWindowTitle)
    {
        LoadFocusSchedule();
        if (!_focusMonitoringEnabled) return;
        if (_focusSessions.Count == 0) return;

        foreach (var session in _focusSessions)
        {
            if (!session.IsEnabled) continue;
            if (!session.IsActiveNow()) continue;
            
            // Is current app a match for the focus target?
            bool isMatch = string.Equals(session.ProcessName, activeProcessName, StringComparison.OrdinalIgnoreCase);

            // Sub-app check
            if (isMatch && !string.IsNullOrEmpty(session.ProgramName))
            {
                isMatch = activeWindowTitle.IndexOf(session.ProgramName, StringComparison.OrdinalIgnoreCase) >= 0;
            }

            if (!isMatch)
            {
                // Check whitelist
                if (_whitelistedProcesses.Contains(activeProcessName))
                {
                    continue;
                }

                // Violation! 
                // ONLY show popup if the main UI is NOT running.
                bool isUiRunning = System.Diagnostics.Process.GetProcessesByName("UsageLogger").Length > 0 
                                || System.Diagnostics.Process.GetProcessesByName("UsageLogger (Startup)").Length > 0;

                if (!isUiRunning)
                {
                    if (session.Mode == 0) // Chill Notification
                    {
                        ShowToast($"Focus: {session.Name}", $"You're using '{activeProcessName}' instead of '{session.ProcessName}'");
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
                            ShowToast($"Focus: {session.Name}", $"Closed '{activeProcessName}'");
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

    private void ShowToast(string title, string message)
    {
        // Debounce Toast
        string key = $"Toast_{title}_{message}";
        if (_focusLastAlert.TryGetValue(key, out DateTime lastAlert))
        {
            if ((DateTime.Now - lastAlert).TotalMinutes < 1) return; // 1 min debounce
        }
        _focusLastAlert[key] = DateTime.Now;

        try
        {
            TrayManager.ShowNotification(title, message);
        }
        catch (Exception ex)
        {
            ServiceLogger.Log("Toast", ex.Message);
        }
    }

    private void ShowFocusEnforcementPopup(ServiceFocusSession session, string violatingProcess)
    {
        // Debounce check
        string key = $"Focus_{session.Id}_{violatingProcess}";
        if (_focusLastAlert.TryGetValue(key, out DateTime lastAlert))
        {
            if ((DateTime.Now - lastAlert).TotalMinutes < FOCUS_ALERT_DEBOUNCE_MINUTES)
            {
                return; // Too soon
            }
        }
        
        _focusLastAlert[key] = DateTime.Now;
        
        // Show popup in background thread
        System.Threading.Tasks.Task.Run(() =>
        {
            try
            {
                TimeSpan endTimeSpan = session.StartTime + session.Duration;
                string endTime = endTimeSpan.ToString(@"hh\:mm");
                bool disableClicked = UI.EnforcementPopup.ShowPopup(
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

