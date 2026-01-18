#nullable enable
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace DigitalWellbeingService.Helpers
{
    /// <summary>
    /// Manages the system tray icon for the Background Service.
    /// Provides functionality to launch/close the UI and exit the service.
    /// </summary>
    public static class TrayManager
    {
        private static NotifyIcon? _notifyIcon;
        private static ContextMenuStrip? _contextMenu;

        /// <summary>
        /// Initializes the tray icon. Call from Main before starting the loop.
        /// </summary>
        public static void Init()
        {
            _contextMenu = new ContextMenuStrip();
            _contextMenu.Items.Add("Open", null, (s, e) => LaunchUI());
            _contextMenu.Items.Add("-"); // Separator
            _contextMenu.Items.Add("Exit", null, (s, e) => ExitAll());

            _notifyIcon = new NotifyIcon
            {
                Text = "Digital Wellbeing",
                ContextMenuStrip = _contextMenu,
                Visible = true
            };

            // Try to load icon
            try
            {
                string iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "icon.ico");
                if (File.Exists(iconPath))
                {
                    _notifyIcon.Icon = new Icon(iconPath);
                }
                else
                {
                    // Fallback to system icon
                    _notifyIcon.Icon = SystemIcons.Application;
                }
            }
            catch
            {
                _notifyIcon.Icon = SystemIcons.Application;
            }

            // Double-click to open
            _notifyIcon.DoubleClick += (s, e) => LaunchUI();

            ServiceLogger.Log("TrayManager", "Tray icon initialized.");
        }

        /// <summary>
        /// Launches the WinUI 3 UI application.
        /// </summary>
        public static void LaunchUI()
        {
            try
            {
                // Find the UI executable
                string[] possiblePaths = new string[]
                {
                    Path.Combine(AppContext.BaseDirectory, "DigitalWellbeingWinUI3.exe"),
                    Path.Combine(AppContext.BaseDirectory, "..", "DigitalWellbeingWinUI3.exe"),
                    Path.Combine(AppContext.BaseDirectory, "..", "DigitalWellbeing", "DigitalWellbeingWinUI3.exe")
                };

                string? uiPath = null;
                foreach (var path in possiblePaths)
                {
                    if (File.Exists(path))
                    {
                        uiPath = path;
                        break;
                    }
                }

                if (uiPath != null)
                {
                    // Check if already running - Single Instance handled by UI App itself (mutex)
                    // var existing = Process.GetProcessesByName("DigitalWellbeingWinUI3"); ...

                    Process.Start(new ProcessStartInfo(uiPath)
                    {
                        UseShellExecute = true
                    });
                    ServiceLogger.Log("TrayManager", "UI launched.");
                }
                else
                {
                    ServiceLogger.Log("TrayManager", $"UI not found. Checked: {string.Join(", ", possiblePaths)}");
                }
            }
            catch (Exception ex)
            {
                ServiceLogger.LogError("TrayManager.LaunchUI", ex);
            }
        }

        /// <summary>
        /// Exits both the Service and any running UI instances.
        /// </summary>
        public static void ExitAll()
        {
            try
            {
                // Kill UI if running
                var uiProcesses = Process.GetProcessesByName("DigitalWellbeingWinUI3");
                foreach (var proc in uiProcesses)
                {
                    try { proc.Kill(); } catch { }
                }
            }
            catch { }

            Dispose();
            Environment.Exit(0);
        }

        /// <summary>
        /// Cleans up the tray icon.
        /// </summary>
        public static void Dispose()
        {
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
                _notifyIcon = null;
            }
            _contextMenu?.Dispose();
        }

        public static void ShowNotification(string title, string message)
        {
            if (_notifyIcon != null)
            {
                _notifyIcon.ShowBalloonTip(5000, title, message, ToolTipIcon.Info);
            }
        }
    }
}
