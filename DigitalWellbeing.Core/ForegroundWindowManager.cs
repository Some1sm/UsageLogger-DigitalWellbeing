using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace DigitalWellbeing.Core;

/// <summary>
/// Provides methods to detect the active foreground window and process.
/// </summary>
public static class ForegroundWindowManager
{
    public static uint GetForegroundProcessId(IntPtr handle)
    {
        GetWindowThreadProcessId(handle, out uint processId);
        return processId;
    }

    public static string? GetActiveProcessName(Process p)
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

    public static string? GetActiveProgramName(Process p)
    {
        try
        {
            return p.MainModule?.FileVersionInfo.ProductName;
        }
        catch
        {
            return null;
        }
    }

    public static string? GetWindowTitle(IntPtr handle)
    {
        try
        {
            const int nChars = 256;
            StringBuilder buff = new(nChars);
            if (GetWindowText(handle, buff, nChars) > 0)
            {
                return buff.ToString();
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

    // Fallback APIs
    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern IntPtr WindowFromPoint(POINT Point);

    [DllImport("user32.dll")]
    private static extern bool GetGUIThreadInfo(uint idThread, ref GUITHREADINFO lpgui);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct GUITHREADINFO
    {
        public int cbSize;
        public uint flags;
        public IntPtr hwndActive;
        public IntPtr hwndFocus;
        public IntPtr hwndCapture;
        public IntPtr hwndMenuOwner;
        public IntPtr hwndMoveSize;
        public IntPtr hwndCaret;
        public RECT rcCaret;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left, Top, Right, Bottom;
    }

    /// <summary>
    /// Gets the active window handle using multiple fallback strategies.
    /// Returns IntPtr.Zero only if all methods fail.
    /// </summary>
    public static IntPtr GetActiveWindowHandle()
    {
        // Primary: Standard foreground window
        IntPtr handle = GetForegroundWindow();
        if (handle != IntPtr.Zero)
            return handle;

        // Fallback 1: Window under cursor
        if (GetCursorPos(out POINT pt))
        {
            handle = WindowFromPoint(pt);
            if (handle != IntPtr.Zero)
                return handle;
        }

        // Fallback 2: GUI Thread Info (foreground thread's active window)
        GUITHREADINFO info = new();
        info.cbSize = Marshal.SizeOf(info);
        if (GetGUIThreadInfo(0, ref info))
        {
            if (info.hwndActive != IntPtr.Zero)
                return info.hwndActive;
            if (info.hwndFocus != IntPtr.Zero)
                return info.hwndFocus;
        }

        return IntPtr.Zero;
    }
}
