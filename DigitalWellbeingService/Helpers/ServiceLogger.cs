using System;
using System.IO;
using static System.Environment;

namespace DigitalWellbeingService.Helpers;

/// <summary>
/// Provides file-based logging for the background service.
/// </summary>
public static class ServiceLogger
{
    private static string? _logPath;
    private static readonly object _lock = new();

    private static string GetLogPath()
    {
        if (_logPath is null)
        {
            string folder = Path.Combine(GetFolderPath(SpecialFolder.LocalApplicationData), "digital-wellbeing");
            if (!Directory.Exists(folder))
            {
                try { Directory.CreateDirectory(folder); } catch { /* Ignore */ }
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
                string line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}";
                File.AppendAllText(path, line + Environment.NewLine);
            }
        }
        catch
        {
            // Fail silently to avoid recursive crashes
        }
    }

    public static void Log(string category, string message) => Log($"[{category}] {message}");

    public static void LogError(string context, Exception ex) => 
        Log($"[ERROR] [{context}] {ex.Message}\n{ex.StackTrace}");
}
