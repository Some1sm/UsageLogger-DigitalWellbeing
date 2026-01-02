using DigitalWellbeing.Core;
using DigitalWellbeing.Core.Data;
using DigitalWellbeing.Core.Models;
using DigitalWellbeingService.Helpers;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using static System.Environment;

namespace DigitalWellbeingService
{
    public class ActivityLogger
    {
        public static readonly int TIMER_INTERVAL_SEC = 3;
        private static int _bufferFlushIntervalSec = 300; // Default: 5 minutes
        private static DateTime _lastSettingsRead = DateTime.MinValue;
        private static DateTime _lastFileWriteTime = DateTime.MinValue;
        private static readonly string _settingsPath = Path.Combine(
            Environment.GetFolderPath(SpecialFolder.LocalApplicationData),
            "digital-wellbeing",
            "user_preferences.json");

        private int GetBufferFlushInterval()
        {
            // Re-check settings file every 30 seconds
            if ((DateTime.Now - _lastSettingsRead).TotalSeconds < 30)
                return _bufferFlushIntervalSec;

            _lastSettingsRead = DateTime.Now;
            
            try
            {
                if (!File.Exists(_settingsPath)) return _bufferFlushIntervalSec;

                var currentWriteTime = File.GetLastWriteTime(_settingsPath);
                if (currentWriteTime == _lastFileWriteTime) return _bufferFlushIntervalSec;
                _lastFileWriteTime = currentWriteTime;

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
            }
            catch { }

            return _bufferFlushIntervalSec;
        }

        private string folderPath;
        private string autoRunFilePath;

        private AppUsageRepository _repository;
        private List<AppUsage> _cachedUsage;
        private DateTime _lastFlushTime;
        private SessionManager _sessionManager;
        
        // Extracted Components
        private readonly AudioUsageTracker _audioTracker;
        private readonly IncognitoMonitor _incognitoMonitor;

        public ActivityLogger()
        {
            folderPath = ApplicationPath.UsageLogsFolder;
            autoRunFilePath = ApplicationPath.autorunFilePath;
            _repository = new AppUsageRepository(folderPath);
            _sessionManager = new SessionManager(folderPath);
            
            // Initialize extracted components
            _audioTracker = new AudioUsageTracker();
            _incognitoMonitor = new IncognitoMonitor();
            
            Debug.WriteLine(folderPath);
            Debug.WriteLine(autoRunFilePath);

            TryCreateAutoRunFile();
            
            // Initial Load for buffer
            _cachedUsage = _repository.GetUsageForDate(DateTime.Now);
            _lastFlushTime = DateTime.Now;
        }

        // AutoRun only
        private void TryCreateAutoRunFile()
        {
            RegistryKey key = Registry.CurrentUser.OpenSubKey(ApplicationPath.AUTORUN_REGPATH);

            bool isAutoRun = key.GetValue(ApplicationPath.AUTORUN_REGKEY) != null ? true : false;

            // Create an empty file that UI will check, do startup things (like hiding window) and delete.
            if (isAutoRun) File.Create(autoRunFilePath).Dispose();
        }

        // Main Timer Logic
        public void OnTimer()
        {
            // Use fallback-aware method instead of direct GetForegroundWindow
            IntPtr handle = ForegroundWindowManager.GetActiveWindowHandle();
            uint currProcessId = ForegroundWindowManager.GetForegroundProcessId(handle);
            
            // Handle PID 0 (System Idle / Desktop / No foreground window)
            if (currProcessId == 0)
            {
                // Log as "System Idle" rather than failing
                UpdateTimeEntry("System Idle", "Desktop");
                _sessionManager.Update("System Idle", "Desktop", new List<string>());
                return;
            }
            
            // Handle edge case where handle is invalid or process exited
            // Use using block to properly dispose Process handle and prevent leaks
            Process proc = null;
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
                        // Fallback to Product Name if Title is empty
                        if (string.IsNullOrWhiteSpace(rawTitle))
                        {
                            programName = ForegroundWindowManager.GetActiveProgramName(proc);
                        }
                        else
                        {
                            programName = DigitalWellbeing.Core.Helpers.WindowTitleParser.Parse(processName, rawTitle);
                        }
                    } 
                    catch {}
                }
                
                if (string.IsNullOrEmpty(programName)) programName = "";

                UpdateTimeEntry(processName, programName);
                
                
                // Audio Tracking with Persistence (Debounce) - use extracted tracker
                var currentAudioApps = AudioSessionTracker.GetActiveAudioSessions();
                var validAudioApps = _audioTracker.UpdatePersistence(currentAudioApps);

                _sessionManager.Update(processName, programName, validAudioApps);
            }
            finally
            {
                proc?.Dispose();
            }
        }

        private void UpdateTimeEntry(string processName, string programName)
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
                FlushBuffer();
            }
        }

        private void FlushBuffer()
        {
            try
            {
                DateTime now = DateTime.Now;

                // Check if day changed since last flush (or initial load)
                if (now.Date > _lastFlushTime.Date)
                {
                    // Flush accumulated data to the OLD date
                    _repository.UpdateUsage(_lastFlushTime.Date, _cachedUsage);
                    
                    // Clear cache for the new day
                    _cachedUsage.Clear();
                }
                else
                {
                    // Same day, update normal log
                    _repository.UpdateUsage(now, _cachedUsage);
                }

                _lastFlushTime = now;
                _sessionManager.FlushBuffer();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to flush buffer: {ex.Message}");
            }
        }
        
        // Ensure we save on shutdown
        public void SaveOnExit()
        {
            FlushBuffer();
            _sessionManager.SaveOnExit();
        }
    }
}
