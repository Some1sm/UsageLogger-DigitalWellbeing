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
using System.Threading.Tasks;

namespace UsageLoggerService;

public class ActivityLogger
{
    // Timer Constants
    public static readonly int TIMER_INTERVAL_SEC = 2;

    private string folderPath = string.Empty;
    private string autoRunFilePath = string.Empty;

    private readonly IAppUsageRepository _repository;
    private List<AppUsage> _cachedUsage = [];
    private DateTime _lastFlushTime;
    private readonly SessionManager _sessionManager;
    private bool _isLocked = false;

    // Extracted Components
    private readonly AudioUsageTracker _audioTracker;
    private readonly IncognitoMonitor _incognitoMonitor;
    private readonly ServiceSettingsReader _settingsReader;
    private readonly FocusScheduleManager _focusManager;

    public ActivityLogger(IAppUsageRepository repository, SessionManager sessionManager)
    {
        folderPath = ApplicationPath.UsageLogsFolder;
        autoRunFilePath = ApplicationPath.autorunFilePath;

        _repository = repository;
        _sessionManager = sessionManager;

        // Initialize extracted components
        _audioTracker = new AudioUsageTracker();
        _incognitoMonitor = new IncognitoMonitor();
        _settingsReader = new ServiceSettingsReader();
        _focusManager = new FocusScheduleManager();

        Debug.WriteLine(folderPath);
        Debug.WriteLine(autoRunFilePath);

        TryCreateAutoRunFile();

        _cachedUsage = [];
        _lastFlushTime = DateTime.Now;

        // Subscribe to Session events (Lock/Unlock)
        try
        {
            Microsoft.Win32.SystemEvents.SessionSwitch += OnSessionSwitch;
            ServiceLogger.Log("ActivityLogger", "SessionSwitch event subscribed successfully.");
        }
        catch (Exception ex)
        {
            ServiceLogger.LogError("SessionSwitch", ex);
        }
    }

    private void OnSessionSwitch(object sender, Microsoft.Win32.SessionSwitchEventArgs e)
    {
        if (e.Reason == Microsoft.Win32.SessionSwitchReason.SessionLock)
        {
            _isLocked = true;
            ServiceLogger.Log("SessionSwitch", "Workstation LOCKED");
        }
        else if (e.Reason == Microsoft.Win32.SessionSwitchReason.SessionUnlock)
        {
            _isLocked = false;
            ServiceLogger.Log("SessionSwitch", "Workstation UNLOCKED");
        }
    }

    /// <summary>
    /// Async initialization - call before starting the timer loop.
    /// </summary>
    public async Task InitializeAsync()
    {
        _cachedUsage = await _repository.GetUsageForDateAsync(DateTime.Now);
    }

    private void TryCreateAutoRunFile()
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(ApplicationPath.AUTORUN_REGPATH);
        bool isAutoRun = key?.GetValue(ApplicationPath.AUTORUN_REGKEY) != null;
        if (isAutoRun) File.Create(autoRunFilePath).Dispose();
    }

    // Main Timer Logic
    public async Task OnTimerAsync()
    {
        // Refresh settings cache (includes IgnoredWindowTitles and IdleThreshold)
        _settingsReader.GetBufferFlushInterval();

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
        bool audioPlaying = AudioSessionTracker.IsGlobalAudioPlaying();
        uint idleMs = UserInputInfo.GetIdleTime();
        int idleThresholdMs = _settingsReader.GetIdleThresholdMs();
        if (idleMs > idleThresholdMs)
        {
            if (!audioPlaying)
            {
                await UpdateTimeEntryAsync("Away", "AFK");
                _sessionManager.Update("Away", "AFK", new List<string>());
                return;
            }
        }

        // --- Priority 3: Active App ---
        IntPtr handle = ForegroundWindowManager.GetActiveWindowHandle();
        uint currProcessId = ForegroundWindowManager.GetForegroundProcessId(handle);

        if (currProcessId == 0)
        {
            await UpdateTimeEntryAsync("System Idle", "Desktop");
            _sessionManager.Update("System Idle", "Desktop", new List<string>(), audioPlaying);
            return;
        }

        Process? proc = null;
        try
        {
            proc = Process.GetProcessById((int)currProcessId);
        }
        catch (Exception ex)
        {
            ServiceLogger.Log("OnTimer", $"GetProcessById({currProcessId}) failed: {ex.Message}");
            return;
        }

        try
        {
            string programName = "";
            string processName = proc.ProcessName;

            if (_incognitoMonitor.IsIncognitoMode)
            {
                programName = processName;
            }
            else
            {
                try
                {
                    string? rawTitle = ForegroundWindowManager.GetWindowTitle(handle);

                    if (_settingsReader.ShouldHideSubApp(rawTitle))
                    {
                        programName = processName;
                    }
                    else if (string.IsNullOrWhiteSpace(rawTitle))
                    {
                        programName = ForegroundWindowManager.GetActiveProgramName(proc) ?? processName;
                    }
                    else
                    {
                        programName = UsageLogger.Core.Helpers.WindowTitleParser.Parse(
                            processName, rawTitle ?? "", _settingsReader.CustomTitleRules.ToList());
                    }
                }
                catch { }
            }

            if (string.IsNullOrEmpty(programName)) programName = "";

            await UpdateTimeEntryAsync(processName, programName);

            // Audio Tracking with Persistence (Debounce)
            var currentAudioApps = AudioSessionTracker.GetActiveAudioSessions();

            if (currentAudioApps.Count == 0 && AudioSessionTracker.IsGlobalAudioPlaying())
            {
                if (!string.IsNullOrEmpty(processName))
                {
                    currentAudioApps.Add(processName);
                }
            }

            var validAudioApps = _audioTracker.UpdatePersistence(currentAudioApps);
            _sessionManager.Update(processName, programName, validAudioApps, audioPlaying);

            // Check Focus Schedule enforcement
            _focusManager.CheckFocusSchedules(processName, programName);
        }
        finally
        {
            proc?.Dispose();
        }
    }

    private async Task UpdateTimeEntryAsync(string processName, string programName)
    {
        if (string.IsNullOrEmpty(processName)) return;

        var existingEntry = _cachedUsage.FirstOrDefault(u => u.ProcessName == processName);

        if (existingEntry != null)
        {
            existingEntry.Duration = existingEntry.Duration.Add(TimeSpan.FromSeconds(TIMER_INTERVAL_SEC));
        }
        else
        {
            if (string.IsNullOrEmpty(programName)) programName = "";
            _cachedUsage.Add(new AppUsage(processName, programName, TimeSpan.FromSeconds(TIMER_INTERVAL_SEC)));
        }

        if ((DateTime.Now - _lastFlushTime).TotalSeconds >= _settingsReader.GetBufferFlushInterval())
        {
            await FlushBufferAsync();
        }
    }

    private async Task FlushBufferAsync()
    {
        try
        {
            DateTime now = DateTime.Now;

            if (now.Date > _lastFlushTime.Date)
            {
                _cachedUsage.Clear();
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
        Microsoft.Win32.SystemEvents.SessionSwitch -= OnSessionSwitch;
        await FlushBufferAsync();
        await _sessionManager.SaveOnExitAsync();
    }
}
