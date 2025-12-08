using DigitalWellbeing.Core;
using DigitalWellbeing.Core.Data;
using DigitalWellbeing.Core.Models;
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

namespace DigitalWellbeingService.NET4._6
{
    public class ActivityLogger
    {
        public static readonly int TIMER_INTERVAL_SEC = 3;
        private static readonly int BUFFER_FLUSH_INTERVAL_SEC = 60; // Flush every minute

        private string folderPath;
        private string autoRunFilePath;

        private AppUsageRepository _repository;
        private List<AppUsage> _cachedUsage;
        private DateTime _lastFlushTime;
        private SessionManager _sessionManager;

        public ActivityLogger()
        {
            folderPath = ApplicationPath.UsageLogsFolder;
            autoRunFilePath = ApplicationPath.autorunFilePath;
            _repository = new AppUsageRepository(folderPath);
            _sessionManager = new SessionManager(folderPath);

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
            IntPtr handle = ForegroundWindowManager.GetForegroundWindow();
            uint currProcessId = ForegroundWindowManager.GetForegroundProcessId(handle);
            
            // Handle edge case where handle is invalid or process exited
            Process proc = null;
            try 
            {
                 proc = Process.GetProcessById((int)currProcessId);
            }
            catch { return; }

            UpdateTimeEntry(proc);
            
            // Session / Interval Tracking
            string programName = ""; 
            try { programName = ForegroundWindowManager.GetActiveProgramName(proc); } catch {}
            if (string.IsNullOrEmpty(programName)) programName = "";
            
            _sessionManager.Update(proc, programName);
        }

        private void UpdateTimeEntry(Process proc)
        {
            string processName = "";
            string programName = "";

            try
            {
                processName = proc.ProcessName;
                // Optimization: Don't fetch Program Name every time if we already have it in cache? 
                // For now, keep it simple or fetch if missing.
            }
            catch { return; }

            // Find in cache
            var existingEntry = _cachedUsage.FirstOrDefault(u => u.ProcessName == processName);

            if (existingEntry != null)
            {
                existingEntry.Duration = existingEntry.Duration.Add(TimeSpan.FromSeconds(TIMER_INTERVAL_SEC));
            }
            else
            {
                // New entry
                try
                {
                    programName = ForegroundWindowManager.GetActiveProgramName(proc);
                } 
                catch {}
                
                // Fallback / Cleanup
                if (string.IsNullOrEmpty(programName)) programName = "";

                _cachedUsage.Add(new AppUsage(processName, programName, TimeSpan.FromSeconds(TIMER_INTERVAL_SEC)));
            }

            // Check if we should flush to disk
            if ((DateTime.Now - _lastFlushTime).TotalSeconds >= BUFFER_FLUSH_INTERVAL_SEC)
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
                    
                    // We also need to reload today's data if application was already running (in case of restart or parallel)
                    // But effectively we start fresh for the new day
                    // Optionally: _cachedUsage = _repository.GetUsageForDate(now); 
                    // But usually new day starts empty.
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
