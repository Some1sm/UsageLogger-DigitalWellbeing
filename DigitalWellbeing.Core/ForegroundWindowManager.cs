using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace DigitalWellbeing.Core
{
    public static class ForegroundWindowManager
    {
        public static uint GetForegroundProcessId(IntPtr handle)
        {
            GetWindowThreadProcessId(handle, out uint processId);

            return processId;
        }

        public static string GetActiveProcessName(Process p)
        {
            try
            {
                return p.ProcessName;
            }
            catch
            {
                return null;
            }
        }

        public static string GetActiveProgramName(Process p)
        {
            try
            {
                // Fallback to Product Name if specific window title fetching isn't used
                return p.MainModule.FileVersionInfo.ProductName;
            }
            catch
            {
                return null;
            }
        }

        public static string GetWindowTitle(IntPtr handle)
        {
            try
            {
                const int nChars = 256;
                StringBuilder Buff = new StringBuilder(nChars);
                if (GetWindowText(handle, Buff, nChars) > 0)
                {
                    return Buff.ToString();
                }
                return null;
            }
            catch
            {
                return null;
            }
        }

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern IntPtr GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);
    }
}
