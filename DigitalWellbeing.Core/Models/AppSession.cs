using System;

namespace DigitalWellbeing.Core.Models
{
    public class AppSession
    {
        public string ProcessName { get; set; }
        public string ProgramName { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public bool IsAfk { get; set; }

        public TimeSpan Duration => EndTime - StartTime;

        public AppSession(string processName, string programName, DateTime startTime, DateTime endTime, bool isAfk = false)
        {
            ProcessName = processName;
            ProgramName = programName;
            StartTime = startTime;
            EndTime = endTime;
            IsAfk = isAfk;
        }

        public AppSession() { }
    }
}
