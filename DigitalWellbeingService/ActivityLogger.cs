using DigitalWellbeing.Core;
using DigitalWellbeing.Core.Data;
using DigitalWellbeing.Core.Interfaces;
using DigitalWellbeing.Core.Models;
using DigitalWellbeingService.Helpers;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using static System.Environment;

namespace DigitalWellbeingService;

public class ActivityLogger
{
    public static readonly int TIMER_INTERVAL_SEC = 3;
    private static int _bufferFlushIntervalSec = 300; // Default: 5 minutes
    private static List<string> _ignoredWindowTitles = new List<string>(); // Cached keywords to hide
    private static List<CustomTitleRule> _customTitleRules = new List<CustomTitleRule>();
    private static DateTime _lastSettingsRead = DateTime.MinValue;
    private static DateTime _lastFileWriteTime = DateTime.MinValue;
    private static readonly string _settingsPath = Path.Combine(
        Environment.GetFolderPath(SpecialFolder.LocalApplicationData),
        "digital-wellbeing",
        "user_preferences.json");

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
                 if ((DateTime.Now - _lastSettingsRead).TotalSeconds < 30)
                     return _bufferFlushIntervalSec;
                 
                 // If > 30s, we might check again (redundant if we trust timestamp, but harmless)
                 _lastSettingsRead = DateTime.Now;
            }
        }
        else
        {
             if ((DateTime.Now - _lastSettingsRead).TotalSeconds < 30)
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
            int useRamCacheIdx = json.IndexOf("\"UseRamCache\":");
            if (useRamCacheIdx >= 0)
            {
                int colonIdx = json.IndexOf(':', useRamCacheIdx);
                int endIdx = json.IndexOf(',', colonIdx);
                if (endIdx < 0) endIdx = json.IndexOf('}', colonIdx);
                
                string valueStr = json.Substring(colonIdx + 1, endIdx - colonIdx - 1).Trim().ToLowerInvariant();
                if (valueStr == "false")
                {
                    _bufferFlushIntervalSec = 1; // Immediate write mode
                    return _bufferFlushIntervalSec;
                }
            }

            // Default: use DataFlushIntervalSeconds for RAM cache mode
            int startIdx = json.IndexOf("\"DataFlushIntervalSeconds\":");
            if (startIdx >= 0)
            {
                int colonIdx = json.IndexOf(':', startIdx);
                int endIdx = json.IndexOf(',', colonIdx);
                if (endIdx < 0) endIdx = json.IndexOf('}', colonIdx);
                
                string valueStr = json.Substring(colonIdx + 1, endIdx - colonIdx - 1).Trim();
                if (int.TryParse(valueStr, out int val) && val >= 60)
                {
                    _bufferFlushIntervalSec = val;
                }
            }
            
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
            catch { } // If parsing fails, keep existing list

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
            catch { }
        }
        catch { }

        return _bufferFlushIntervalSec;
    }
    
    /// <summary>
    /// Checks if a window title should be hidden (merged into parent process).
    /// </summary>
    private bool ShouldHideSubApp(string windowTitle)
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

    private string folderPath;
    private string autoRunFilePath;

    private readonly IAppUsageRepository _repository;
    private List<AppUsage> _cachedUsage;
    private DateTime _lastFlushTime;
    private readonly SessionManager _sessionManager;
    
    // Extracted Components
    private readonly AudioUsageTracker _audioTracker;
    private readonly IncognitoMonitor _incognitoMonitor;

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
        // Refresh settings cache (includes IgnoredWindowTitles)
        GetBufferFlushInterval();
        
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
                    string rawTitle = ForegroundWindowManager.GetWindowTitle(handle);
                    
                    // Check Hidden SubApps filter FIRST (on raw title)
                    // If keyword matches, skip parsing and use process name
                    if (ShouldHideSubApp(rawTitle))
                    {
                        programName = processName;
                    }
                    // Fallback to Product Name if Title is empty
                    else if (string.IsNullOrWhiteSpace(rawTitle))
                    {
                        programName = ForegroundWindowManager.GetActiveProgramName(proc);
                    }
                    else
                    {
                        programName = DigitalWellbeing.Core.Helpers.WindowTitleParser.Parse(processName, rawTitle, _customTitleRules);
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
                await _repository.UpdateUsageAsync(_lastFlushTime.Date, _cachedUsage);
                
                // Clear cache for the new day
                _cachedUsage.Clear();
            }
            else
            {
                // Same day, update normal log
                await _repository.UpdateUsageAsync(now, _cachedUsage);
            }

            _lastFlushTime = now;
            await _sessionManager.FlushBufferAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to flush buffer: {ex.Message}");
        }
    }
    
    // Ensure we save on shutdown
    public async Task SaveOnExitAsync()
    {
        await FlushBufferAsync();
        await _sessionManager.SaveOnExitAsync();
    }
}
