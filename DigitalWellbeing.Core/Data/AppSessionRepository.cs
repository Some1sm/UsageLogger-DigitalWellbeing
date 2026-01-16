using DigitalWellbeing.Core.Interfaces;
using DigitalWellbeing.Core.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace DigitalWellbeing.Core.Data;

public class AppSessionRepository : IAppSessionRepository
{
    private readonly string _logsFolderPath;

    public AppSessionRepository(string logsFolderPath)
    {
        _logsFolderPath = logsFolderPath;
    }

    private string GetFilePath(DateTime date) => Path.Combine(_logsFolderPath, $"sessions_{date:MM-dd-yyyy}.log");

    public async Task<List<AppSession>> GetSessionsForDateAsync(DateTime date)
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
                    await using var fs = new FileStream(legacyPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    using var reader = new StreamReader(fs);
                    
                    string? line;
                    while ((line = await reader.ReadLineAsync()) != null)
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
                                DateTime start = date.Date;
                                DateTime end = start.AddSeconds(seconds);
                                sessions.Add(new AppSession(processName, programName, start, end, false, null));
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
            await using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(fs);
            
            string? line;
            while ((line = await reader.ReadLineAsync()) != null)
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

                        System.Collections.Generic.List<string>? audioSources = null;
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
        catch (Exception ex)
        {
            Console.WriteLine($"Session Repo Read Error: {ex.Message}");
        }

        // MERGE ALL LIVE SESSIONS (from RAM)
        if (date.Date == DateTime.Now.Date)
        {
            try
            {
                var ramSessions = DigitalWellbeing.Core.Helpers.LiveSessionCache.ReadAll();
                foreach (var ramSession in ramSessions)
                {
                    if (ramSession == null || ramSession.StartTime.Date != date.Date) continue;
                    
                    var existing = sessions.FirstOrDefault(s => s.ProcessName == ramSession.ProcessName && Math.Abs((s.StartTime - ramSession.StartTime).TotalSeconds) < 2);
                    
                    if (existing != null)
                    {
                        existing.EndTime = ramSession.EndTime;
                        existing.IsAfk = ramSession.IsAfk;
                    }
                    else
                    {
                        sessions.Add(ramSession);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Live Session Merge Error: " + ex.Message);
            }
        }

        // CONSOLIDATE: Merge overlapping intervals
        sessions = ConsolidateSessions(sessions);

        return sessions;
    }

    private List<AppSession> ConsolidateSessions(List<AppSession> sessions)
    {
        if (sessions == null || sessions.Count <= 1) return sessions ?? new List<AppSession>();

        var result = new List<AppSession>();

        var groups = sessions.GroupBy(s => new { s.ProcessName, s.ProgramName });

        foreach (var group in groups)
        {
            var sorted = group.OrderBy(s => s.StartTime).ToList();
            AppSession current = sorted[0];

            for (int i = 1; i < sorted.Count; i++)
            {
                var next = sorted[i];

                if (next.StartTime <= current.EndTime.AddSeconds(2))
                {
                    if (next.EndTime > current.EndTime)
                        current.EndTime = next.EndTime;
                    
                    if (next.AudioSources != null && next.AudioSources.Count > 0)
                    {
                        if (current.AudioSources == null) current.AudioSources = new List<string>();
                        foreach (var src in next.AudioSources)
                        {
                            if (!current.AudioSources.Contains(src))
                                current.AudioSources.Add(src);
                        }
                    }
                }
                else
                {
                    result.Add(current);
                    current = next;
                }
            }
            result.Add(current);
        }

        return result;
    }

    public async Task AppendSessionsAsync(List<AppSession> sessions)
    {
        if (sessions == null || sessions.Count == 0) return;

        var groups = sessions.GroupBy(s => s.StartTime.Date);

        foreach(var group in groups)
        {
            string filePath = GetFilePath(group.Key);
            Directory.CreateDirectory(_logsFolderPath);

            try
            {
                await using var fs = new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.Read);
                using var writer = new StreamWriter(fs);
                
                foreach (var session in group)
                {
                    string audioStr = (session.AudioSources != null && session.AudioSources.Count > 0) ? string.Join(";", session.AudioSources) : "";
                    string line = $"{session.ProcessName}\t{session.ProgramName}\t{session.StartTime.Ticks}\t{session.EndTime.Ticks}\t{session.IsAfk}\t{audioStr}";
                    await writer.WriteLineAsync(line);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Session Repo Bulk Write Error: {ex.Message}");
            }
        }
    }

    public async Task UpdateOrAppendAsync(AppSession session)
    {
        try
        {
            string filePath = GetFilePath(session.StartTime.Date);
            Directory.CreateDirectory(_logsFolderPath);

            string cleanProcess = session.ProcessName?.Replace("\t", " ")?.Replace("\r", "")?.Replace("\n", "") ?? "";
            string cleanProgram = session.ProgramName?.Replace("\t", " ")?.Replace("\r", "")?.Replace("\n", "") ?? "";

            string audioStr = (session.AudioSources != null && session.AudioSources.Count > 0) ? string.Join(";", session.AudioSources) : "";
            string newLine = $"{cleanProcess}\t{cleanProgram}\t{session.StartTime.Ticks}\t{session.EndTime.Ticks}\t{session.IsAfk}\t{audioStr}";

            await using var fs = new FileStream(filePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);

            if (fs.Length == 0)
            {
                using var writer = new StreamWriter(fs);
                await writer.WriteLineAsync(newLine);
                return;
            }

            // Sync seeking logic (Stream doesn't have Async Seek)
            long position = fs.Length - 1;
            
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

            while (position >= 0)
            {
                fs.Seek(position, SeekOrigin.Begin);
                int b = fs.ReadByte();
                if (b == '\n') 
                {
                    position++;
                    break;
                }
                position--;
            }
            
            if (position < 0) position = 0;

            fs.Seek(position, SeekOrigin.Begin);
            string? lastLine;
            
            // Allow StreamReader to leave stream open so we can truncate if needed
            using (var reader = new StreamReader(fs, System.Text.Encoding.UTF8, false, 1024, true))
            {
                lastLine = await reader.ReadLineAsync();
            }

            bool shouldOverwrite = false;

            if (!string.IsNullOrWhiteSpace(lastLine))
            {
                string[] parts = lastLine.Split('\t');
                if (parts.Length >= 5)
                {
                    string processName = parts[0];
                    if (long.TryParse(parts[2], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out long startTicks))
                    {
                        if (processName == session.ProcessName && startTicks == session.StartTime.Ticks)
                        {
                            shouldOverwrite = true;
                        }
                    }
                }
            }

            if (shouldOverwrite)
            {
                fs.SetLength(position);
                fs.Seek(position, SeekOrigin.Begin);
            }
            else
            {
                fs.Seek(0, SeekOrigin.End);
            }

            using (var writer = new StreamWriter(fs))
            {
                await writer.WriteLineAsync(newLine);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Session Repo Update Error: {ex.Message}");
        }
    }
    public async Task<int> GetTotalDaysCountAsync()
    {
        return await Task.Run(() =>
        {
            try
            {
                if (!Directory.Exists(_logsFolderPath)) return 0;

                var files = Directory.GetFiles(_logsFolderPath, "*.log");
                var dates = new HashSet<DateTime>();

                foreach (var file in files)
                {
                    string name = Path.GetFileNameWithoutExtension(file);
                    
                    if (name.StartsWith("sessions_"))
                    {
                        name = name.Substring(9);
                    }
                    
                    if (DateTime.TryParseExact(name, "MM-dd-yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime date))
                    {
                        dates.Add(date.Date);
                    }
                }
                
                dates.Add(DateTime.Now.Date);

                return dates.Count;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error counting days: {ex.Message}");
                return 1;
            }
        });
    }
}
