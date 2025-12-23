using DigitalWellbeing.Core.Helpers;
using DigitalWellbeing.Core;
using DigitalWellbeing.Core.Models;
using DigitalWellbeingWinUI3.ViewModels;
using DigitalWellbeingWinUI3;
using H.NotifyIcon;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

// Alias WinUI DispatcherTimer to avoid confusion
using DispatcherTimer = Microsoft.UI.Xaml.DispatcherTimer;

namespace DigitalWellbeingWinUI3.Helpers
{
    public static class Notifier
    {
        public static TaskbarIcon TrayIcon;

        private static TimeSpan warningLimit = TimeSpan.FromMinutes(15);
        private static int CHECK_INTERVAL = 60; // Check every 60 seconds

        private static MainWindow _mainWindow;

        public static void Init(MainWindow mainWindow, TaskbarIcon icon)
        {
            _mainWindow = mainWindow;
            TrayIcon = icon;
            
            try
            {
                 // Try to load icon from Assets
                 string iconPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "icon.ico");
                 if (System.IO.File.Exists(iconPath))
                 {
                     TrayIcon.Icon = new System.Drawing.Icon(iconPath);
                 }
            }
            catch 
            {
                // Fallback or ignore
            }
            
            // Context Menu is now defined in XAML

            // Force visibility? 
            TrayIcon.Visibility = Visibility.Visible;
        }

        public static void Dispose()
        {
            if (TrayIcon != null)
            {
                TrayIcon.Dispose();
            }
        }

        private static MainWindow GetMainWindow()
        {
            if (_mainWindow != null) return _mainWindow;

            if (App.Current is App myApp && myApp.m_window is MainWindow window)
            {
                return window;
            }
            return null;
        }

        public static void ShowNotification(string title, string message, EventHandler clickHandler = null, object icon = null) // icon param unused for now or map to H.NotifyIcon
        {
            if (TrayIcon == null) return;

             // Map WinForms icon to H.NotifyIcon equivalent?
             // H.NotifyIcon uses 'NotificationIcon' enum or just title/message
             
             // TrayIcon.ShowNotification(title, message, iconType);
             // We'll just show text for now.
             
             TrayIcon.ShowNotification(title, message);
             
             // Click handler? 
             // TrayIcon.TrayBalloonTipClicked += ... 
             // But H.NotifyIcon might handle it differently.
             // We'll assume simple notification logic for now.
             
             // If we really need click handler:
             // TrayIcon.TrayBalloonTipClicked += (s,e) => { clickHandler?.Invoke(s,e); };
        }

        #region App Time Limit Checker (DEPRECATED - Moved to TimeLimitEnforcer.cs)

        // Legacy checker removed to avoid duplicate alerts.
        // Logic handled by TimeLimitEnforcer.CheckTimeLimitsAsync called from AppUsageViewModel.

        public static void InitNotifierTimer()
        {
            // No-op
        }

        public static void ResetNotificationForApp(string processName)
        {
            // No-op
        }
        #endregion
    }
}
