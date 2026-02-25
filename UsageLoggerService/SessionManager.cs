#nullable enable
using UsageLogger.Core.Data;
using UsageLogger.Core.Interfaces;
using UsageLogger.Core.Models;
using UsageLoggerService.Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace UsageLoggerService;

public class SessionManager
{
    private const int AFK_THRESHOLD_MS = 2 * 60 * 1000; // 2 minutes

    private readonly IAppSessionRepository _repository;
    private List<AppSession> _sessionBuffer; // Completed sessions not yet on disk
    
    private AppSession? _currentSession;
    
    public SessionManager(IAppSessionRepository repository)
    {
        _repository = repository;
        _sessionBuffer = new List<AppSession>();
    }

    public void Update(string processName, string programName, List<string>? audioSources = null, bool audioPlaying = false)
    {
        if (string.IsNullOrEmpty(processName)) return;
        if (audioSources == null) audioSources = new List<string>();

        uint idleTime = UserInputInfo.GetIdleTime();
        bool isAfk = idleTime > AFK_THRESHOLD_MS && !audioPlaying;

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
            bool programChanged = _currentSession.ProgramName != programName; // Detect incognito mode changes
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

            if (appChanged || programChanged || stateChanged || audioChanged)
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
            
            UsageLogger.Core.Helpers.LiveSessionCache.WriteAll(allSessions);
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

    public async Task FlushBufferAsync()
    {
        try 
        {
            // 1. Flush Completed Sessions to disk
            if (_sessionBuffer.Count > 0)
            {
                await _repository.AppendSessionsAsync(_sessionBuffer);
                _sessionBuffer.Clear();
            }

            // 2. Persist Active Session (Checkpoint)
            if (_currentSession != null && _currentSession.Duration.TotalSeconds > 1)
            {
                await _repository.UpdateOrAppendAsync(_currentSession);
            }
            
            // Update RAM cache (buffer is now empty, only current remains)
            UpdateRAMCache();
        } 
        catch (Exception ex)
        {
            Console.WriteLine("Flush Error: " + ex.Message);
        }
    }

    public async Task SaveOnExitAsync()
    {
        FinalizeCurrentSession(DateTime.Now);
        await FlushBufferAsync();
        // Clear RAM cache on exit
        try 
        {
            UsageLogger.Core.Helpers.LiveSessionCache.Clear();
        } 
        catch {}
    }
}
