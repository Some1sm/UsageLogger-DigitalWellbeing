using DigitalWellbeing.Core.Data;
using DigitalWellbeing.Core.Models;
using DigitalWellbeingService.NET4._6.Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace DigitalWellbeingService.NET4._6
{
    public class SessionManager
    {
        private const int AFK_THRESHOLD_MS = 2 * 60 * 1000; // 2 minutes

        private AppSessionRepository _repository;
        private List<AppSession> _sessionBuffer; // Completed sessions not yet on disk
        
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
                }
            }

            if (shouldStartNew)
            {
                _currentSession = new AppSession(processName, programName, now, now, isAfk, audioSources);
            }

            // Write ALL sessions (buffer + current) to RAM for real-time UI updates
            UpdateRAMCache();
        }

        /// <summary>
        /// Write ALL sessions (completed buffer + current) to RAM cache.
        /// This allows UI to read real-time data without disk access.
        /// </summary>
        private void UpdateRAMCache()
        {
            try 
            {
                // Build list of all sessions for today
                var allSessions = new List<AppSession>(_sessionBuffer);
                if (_currentSession != null)
                {
                    allSessions.Add(_currentSession);
                }
                
                DigitalWellbeing.Core.Helpers.LiveSessionCache.WriteAll(allSessions);
            } 
            catch {}
        }

        private void FinalizeCurrentSession(DateTime endTime)
        {
            if (_currentSession == null) return;
            
            _currentSession.EndTime = endTime;
            
            // Add to BUFFER (RAM) - will be flushed to disk every 5 min
            if (_currentSession.Duration.TotalSeconds > 1) 
            {
                _sessionBuffer.Add(_currentSession);
            }
            
            _currentSession = null;
            // Note: Don't clear RAM cache here - UpdateRAMCache will update it
        }

        public void FlushBuffer()
        {
            try 
            {
                // 1. Flush Completed Sessions to disk
                if (_sessionBuffer.Count > 0)
                {
                    _repository.AppendSessions(_sessionBuffer);
                    _sessionBuffer.Clear();
                }

                // 2. Persist Active Session (Checkpoint)
                if (_currentSession != null && _currentSession.Duration.TotalSeconds > 1)
                {
                    _repository.UpdateOrAppend(_currentSession);
                }
                
                // Update RAM cache (buffer is now empty, only current remains)
                UpdateRAMCache();
            } 
            catch (Exception ex)
            {
               Console.WriteLine("Flush Error: " + ex.Message);
            }
        }

        public void SaveOnExit()
        {
            FinalizeCurrentSession(DateTime.Now);
            FlushBuffer();
            // Clear RAM cache on exit
            try 
            {
                DigitalWellbeing.Core.Helpers.LiveSessionCache.Clear();
            } catch {}
        }
    }
}
