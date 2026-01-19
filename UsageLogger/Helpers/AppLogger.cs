using UsageLogger.Core;
using System;
using System.Diagnostics;
using System.IO;

namespace UsageLogger.Helpers
{
    public static class AppLogger
    {
        public static void WriteLine(object logObj, bool logToFile = true)
        {
            string path = $"{ApplicationPath.InternalLogsFolder}{DateTime.Now:MM-dd-yyyy}.log";

            try
            {
                string[] lines = new string[] { $"[{DateTime.Now}]\t{logObj}" };

                Debug.WriteLine(lines[0]);

                if (logToFile)
                {
                    File.AppendAllLines(path, lines);
                }
            }
            catch (DirectoryNotFoundException)
            {
                Directory.CreateDirectory(ApplicationPath.InternalLogsFolder);
            }
            catch { }
        }
    }
}
