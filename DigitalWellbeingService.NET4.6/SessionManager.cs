using DigitalWellbeing.Core.Data;
using DigitalWellbeing.Core.Models;
using DigitalWellbeingService.NET4._6.Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace DigitalWellbeingService.NET4._6
{
    public class SessionManager
    {
        private const int AFK_THRESHOLD_MS = 2 * 60 * 1000; // 2 minutes

        private AppSessionRepository _repository;
        private List<AppSession> _sessionBuffer;
        
        private AppSession _currentSession;
        private DateTime _lastFlushTime;

        public SessionManager(string folderPath)
        {
            _repository = new AppSessionRepository(folderPath);
            _sessionBuffer = new List<AppSession>();
            _lastFlushTime = DateTime.Now;
        }

        public void Update(Process process, string programName)
        {
            string processName = "";
            try { processName = process.ProcessName; } catch { return; }

            uint idleTime = UserInputInfo.GetIdleTime();
            bool isAfk = idleTime > AFK_THRESHOLD_MS;

            DateTime now = DateTime.Now;

            // Decision Logic:
            // 1. If no current session, start one.
            // 2. If Current != New (Process changed), close current, start new.
            // 3. If Current == New but AFK state changed, close current, start new (with new AFK state).
            // 4. Else (Same process, same State), just extend EndTime.

            bool shouldStartNew = false;
            
            if (_currentSession == null)
            {
                shouldStartNew = true;
            }
            else
            {
                bool appChanged = _currentSession.ProcessName != processName;
                bool stateChanged = _currentSession.IsAfk != isAfk;

                if (appChanged || stateChanged)
                {
                    // Close current
                    FinalizeCurrentSession(now);
                    shouldStartNew = true;
                }
                else
                {
                    // Extend
                    _currentSession.EndTime = now;

                    // Feature: Real-time update without splitting.
                    // Update the last entry in the file.
                    // Optional: Throttle this? Every 3s is okay for a single file on SSD.
                    _repository.UpdateOrAppend(_currentSession);
                }
            }

            if (shouldStartNew)
            {
                _currentSession = new AppSession(processName, programName, now, now, isAfk);
            }
        }

        private void FinalizeCurrentSession(DateTime endTime)
        {
            if (_currentSession == null) return;
            
            _currentSession.EndTime = endTime;
            
            // Final update to disk
            if (_currentSession.Duration.TotalSeconds > 1) 
            {
                _repository.UpdateOrAppend(_currentSession);
            }
            
            _currentSession = null;
        }

        public void FlushBuffer()
        {
            if (_sessionBuffer.Count > 0)
            {
                _repository.AppendSessions(_sessionBuffer);
                _sessionBuffer.Clear();
            }
        }

        public void SaveOnExit()
        {
            FinalizeCurrentSession(DateTime.Now);
            FlushBuffer();
        }
    }
}
