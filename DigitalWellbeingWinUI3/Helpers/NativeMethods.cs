using System;
using System.Runtime.InteropServices;

namespace DigitalWellbeingWinUI3.Helpers
{
    /// <summary>
    /// Centralized P/Invoke declarations for Windows native methods.
    /// </summary>
    internal static class NativeMethods
    {
        private const string USER32 = "user32.dll";
        
        // Window visibility constants
        public const int SW_HIDE = 0;
        public const int SW_RESTORE = 9;
        
        /// <summary>
        /// Shows or hides the specified window.
        /// </summary>
        [DllImport(USER32)]
        public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        
        /// <summary>
        /// Brings the specified window to the foreground.
        /// </summary>
        [DllImport(USER32)]
        public static extern bool SetForegroundWindow(IntPtr hWnd);
    }
}
