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

        public void Update(Process process, string programName, List<string> audioSources = null)
        {
            if (audioSources == null) audioSources = new List<string>();

            string processName = "";
            try { processName = process.ProcessName; } catch { return; }

            uint idleTime = UserInputInfo.GetIdleTime();
            bool isAfk = idleTime > AFK_THRESHOLD_MS;

            DateTime now = DateTime.Now;

            // Decision Logic:
            // 1. If no current session, start one.
            // 2. If Current != New (Process changed), close current, start new.
            // 3. If Current == New but AFK state changed, close current, start new (with new AFK state).
            // 4. NEW: If Audio Sources changed, close current, start new.
            // 5. Else (Same process, same State, same Audio), just extend EndTime.

            bool shouldStartNew = false;
            
            if (_currentSession == null)
            {
                shouldStartNew = true;
            }
            else
            {
                bool appChanged = _currentSession.ProcessName != processName;
                bool stateChanged = _currentSession.IsAfk != isAfk;
                
                // Audio Change Check
                bool audioChanged = false;
                var currentAudio = _currentSession.AudioSources ?? new List<string>();
                if (currentAudio.Count != audioSources.Count)
                {
                    audioChanged = true;
                }
                else
                {
                    // Check contents (Ignoring order? usually order is consistent from tracker but HashSet is safer)
                    var set1 = new HashSet<string>(currentAudio);
                    var set2 = new HashSet<string>(audioSources);
                    if (!set1.SetEquals(set2)) audioChanged = true;
                }

                if (appChanged || stateChanged || audioChanged)
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
                _currentSession = new AppSession(processName, programName, now, now, isAfk, audioSources);
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
