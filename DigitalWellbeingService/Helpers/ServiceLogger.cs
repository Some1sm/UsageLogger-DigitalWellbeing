using System;
using System.IO;
using static System.Environment;

namespace DigitalWellbeingService.Helpers
{
    public static class ServiceLogger
    {
        private static string _logPath;
        private static readonly object _lock = new object();

        private static string GetLogPath()
        {
            if (_logPath == null)
            {
                string folder = Path.Combine(GetFolderPath(SpecialFolder.LocalApplicationData), "digital-wellbeing");
                if (!Directory.Exists(folder))
                {
                    try { Directory.CreateDirectory(folder); } catch { }
                }
                _logPath = Path.Combine(folder, "service_debug.log");
            }
            return _logPath;
        }

        public static void Log(string message)
        {
            try
            {
                lock (_lock)
                {
                    string path = GetLogPath();
                    // Keep file size in check? Maybe rotate if > 1MB. For now just append.
                    string line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}";
                    File.AppendAllText(path, line + Environment.NewLine);
                }
            }
            catch (Exception)
            {
                // Last resort: fail silently to avoid recursive crashes
            }
        }

        public static void Log(string category, string message)
        {
            Log($"[{category}] {message}");
        }

        public static void LogError(string context, Exception ex)
        {
            Log($"[ERROR] [{context}] {ex.Message}\n{ex.StackTrace}");
        }
    }
}
