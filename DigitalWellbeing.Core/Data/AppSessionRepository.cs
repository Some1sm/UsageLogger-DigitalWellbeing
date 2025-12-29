using DigitalWellbeing.Core.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace DigitalWellbeing.Core.Data
{
    public class AppSessionRepository
    {
        private readonly string _logsFolderPath;

        public AppSessionRepository(string logsFolderPath)
        {
            _logsFolderPath = logsFolderPath;
        }

        private string GetFilePath(DateTime date) => Path.Combine(_logsFolderPath, $"sessions_{date:MM-dd-yyyy}.log");

        public List<AppSession> GetSessionsForDate(DateTime date)
        {
            string filePath = GetFilePath(date);
            List<AppSession> sessions = new List<AppSession>();

            if (!File.Exists(filePath))
            {
                // FALLBACK: Try Legacy Usage Log
                string legacyPath = Path.Combine(_logsFolderPath, $"{date:MM-dd-yyyy}.log");
                if (File.Exists(legacyPath))
                {
                    try 
                    {
                        using (var fs = new FileStream(legacyPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        using (var reader = new StreamReader(fs))
                        {
                            string line;
                            while ((line = reader.ReadLine()) != null)
                            {
                                if (string.IsNullOrWhiteSpace(line)) continue;

                                string[] parts = line.Split('\t');
                                if (parts.Length >= 2)
                                {
                                    string processName = parts[0];
                                    if (int.TryParse(parts[1], out int seconds))
                                    {
                                        string programName = parts.Length > 2 ? parts[2] : "";
                                        // Create Pseudo-Session for legacy data compatibility
                                        // We just put it at midnight so the Dashboard can calculate the sum.
                                        // The Timeline will show one block at 00:00, which is acceptable for legacy.
                                        DateTime start = date.Date;
                                        DateTime end = start.AddSeconds(seconds);
                                        sessions.Add(new AppSession(processName, programName, start, end, false, null));
                                    }
                                }
                            }
                        }
                    } 
                    catch { }
                }
                return sessions;
            }

            try
            {
                using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var reader = new StreamReader(fs))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (string.IsNullOrWhiteSpace(line)) continue;

                        try
                        {
                            string[] parts = line.Split('\t');
                            if (parts.Length >= 5)
                            {
                                string processName = parts[0];
                                string programName = parts[1];
                                long startTicks = long.Parse(parts[2], CultureInfo.InvariantCulture);
                                long endTicks = long.Parse(parts[3], CultureInfo.InvariantCulture);
                                bool isAfk = bool.Parse(parts[4]);

                                System.Collections.Generic.List<string> audioSources = null;
                                if (parts.Length >= 6 && !string.IsNullOrEmpty(parts[5]))
                                {
                                    audioSources = parts[5].Split(';').ToList();
                                }

                                sessions.Add(new AppSession(processName, programName, new DateTime(startTicks), new DateTime(endTicks), isAfk, audioSources));
                            }
                        }
                        catch { } // Skip malformed lines
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Session Repo Read Error: {ex.Message}");
            }

            // MERGE ALL LIVE SESSIONS (from RAM)
            // This allows the UI to see ALL sessions (buffer + current) even if not flushed to disk yet.
            if (date.Date == DateTime.Now.Date)
            {
                try
                {
                    var ramSessions = DigitalWellbeing.Core.Helpers.LiveSessionCache.ReadAll();
                    foreach (var ramSession in ramSessions)
                    {
                        if (ramSession == null || ramSession.StartTime.Date != date.Date) continue;
                        
                        // Check if we already have this session on disk
                        var existing = sessions.FirstOrDefault(s => s.ProcessName == ramSession.ProcessName && s.StartTime == ramSession.StartTime);
                        
                        if (existing != null)
                        {
                            // Update with fresher RAM data
                            existing.EndTime = ramSession.EndTime;
                            existing.IsAfk = ramSession.IsAfk;
                        }
                        else
                        {
                            // New session not yet on disk
                            sessions.Add(ramSession);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Live Session Merge Error: " + ex.Message);
                }
            }

            return sessions;
        }

        public void AppendSession(AppSession session)
        {
            // We append immediately for now, or we can use the same bulk update strategy.
            // Given the requirement for real-time-ish updates, and that sessions are unique rows (not aggregated),
            // Appending is safer than rewriting the whole file every 3 seconds.
            // BUT, `ActivityLogger` does buffering. So we can use a bulk append.

            string filePath = GetFilePath(session.StartTime.Date);
            Directory.CreateDirectory(_logsFolderPath);

            string audioStr = (session.AudioSources != null && session.AudioSources.Count > 0) ? string.Join(";", session.AudioSources) : "";
            string line = $"{session.ProcessName}\t{session.ProgramName}\t{session.StartTime.Ticks}\t{session.EndTime.Ticks}\t{session.IsAfk}\t{audioStr}";

            try
            {
                using (var fs = new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.Read))
                using (var writer = new StreamWriter(fs))
                {
                    writer.WriteLine(line);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Session Repo Write Error: {ex.Message}");
            }
        }
        
        public void AppendSessions(List<AppSession> sessions)
        {
            if (sessions == null || sessions.Count == 0) return;

            // Group by date to handle midnight crossover (though SessionManager usually handles this by splitting)
            var groups = sessions.GroupBy(s => s.StartTime.Date);

            foreach(var group in groups)
            {
                string filePath = GetFilePath(group.Key);
                Directory.CreateDirectory(_logsFolderPath);

                try
                {
                    using (var fs = new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.Read))
                    using (var writer = new StreamWriter(fs))
                    {
                        foreach (var session in group)
                        {
                            string audioStr = (session.AudioSources != null && session.AudioSources.Count > 0) ? string.Join(";", session.AudioSources) : "";
                            string line = $"{session.ProcessName}\t{session.ProgramName}\t{session.StartTime.Ticks}\t{session.EndTime.Ticks}\t{session.IsAfk}\t{audioStr}";
                            writer.WriteLine(line);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Session Repo Bulk Write Error: {ex.Message}");
                }
            }
        }

        public void UpdateOrAppend(AppSession session)
        {
            try
            {
                string filePath = GetFilePath(session.StartTime.Date);
                Directory.CreateDirectory(_logsFolderPath);

                string audioStr = (session.AudioSources != null && session.AudioSources.Count > 0) ? string.Join(";", session.AudioSources) : "";
                string newLine = $"{session.ProcessName}\t{session.ProgramName}\t{session.StartTime.Ticks}\t{session.EndTime.Ticks}\t{session.IsAfk}\t{audioStr}";

                using (var fs = new FileStream(filePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite))
                {
                    if (fs.Length == 0)
                    {
                        // Empty file, just write
                        using (var writer = new StreamWriter(fs))
                        {
                            writer.WriteLine(newLine);
                        }
                        return;
                    }

                    // Find start of last line
                    long position = fs.Length - 1;
                    
                    // Skip trailing newline if exists
                    if (position >= 0)
                    {
                         fs.Seek(position, SeekOrigin.Begin);
                         int b = fs.ReadByte();
                         if (b == '\n') position--;
                         if (position >= 0)
                         {
                             fs.Seek(position, SeekOrigin.Begin);
                             b = fs.ReadByte();
                             if (b == '\r') position--;
                         }
                    }

                    // Scan backwards for previous newline
                    while (position >= 0)
                    {
                        fs.Seek(position, SeekOrigin.Begin);
                        int b = fs.ReadByte();
                        if (b == '\n') 
                        {
                            // Found newline, so line starts at position + 1
                            position++;
                            break;
                        }
                        position--;
                    }
                    
                    if (position < 0) position = 0; // First line

                    // Read last line
                    fs.Seek(position, SeekOrigin.Begin);
                    string lastLine;
                    using (var reader = new StreamReader(fs, System.Text.Encoding.UTF8, false, 1024, true)) // leaveOpen=true
                    {
                        lastLine = reader.ReadLine();
                    }

                    bool shouldOverwrite = false;

                    if (!string.IsNullOrWhiteSpace(lastLine))
                    {
                        string[] parts = lastLine.Split('\t');
                        if (parts.Length >= 5)
                        {
                            string processName = parts[0];
                            long startTicks = long.Parse(parts[2]);
                            
                            // Check if it's the same session (Same Start Time exactly)
                            if (processName == session.ProcessName && startTicks == session.StartTime.Ticks)
                            {
                                shouldOverwrite = true;
                            }
                        }
                    }

                    if (shouldOverwrite)
                    {
                        // Truncate to start of last line
                        fs.SetLength(position);
                        fs.Seek(position, SeekOrigin.Begin);
                    }
                    else
                    {
                        // Append to end
                        fs.Seek(0, SeekOrigin.End);
                    }

                    // Write
                    // Need to construct StreamWriter to write from current position
                    // Be careful with encoding/newline. StreamWriter adds newline.
                    using (var writer = new StreamWriter(fs))
                    {
                        writer.WriteLine(newLine);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Session Repo Update Error: {ex.Message}");
            }
        }
    }
}
