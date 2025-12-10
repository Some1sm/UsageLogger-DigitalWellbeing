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

        public System.Collections.Generic.List<string> AudioSources { get; set; } = new System.Collections.Generic.List<string>();

        public AppSession(string processName, string programName, DateTime startTime, DateTime endTime, bool isAfk = false, System.Collections.Generic.List<string> audioSources = null)
        {
            ProcessName = processName;
            ProgramName = programName;
            StartTime = startTime;
            EndTime = endTime;
            IsAfk = isAfk;
            if (audioSources != null)
            {
                AudioSources = audioSources;
            }
        }

        public AppSession() { }
    }
}
