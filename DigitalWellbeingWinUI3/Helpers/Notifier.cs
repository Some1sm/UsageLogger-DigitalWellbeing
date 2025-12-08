using DigitalWellbeing.Core.Helpers;
using DigitalWellbeing.Core;
using DigitalWellbeing.Core.Models;
using DigitalWellbeingWinUI3.ViewModels;
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
        private static int NOTIFICATION_TIMOUT_SECONDS = 10;
        private static TimeSpan warningLimit = TimeSpan.FromMinutes(15);
        private static int CHECK_INTERVAL = 60; // Check every 60 seconds

        public static void Init(TaskbarIcon icon)
        {
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

            // Create Context Menu (MenuFlyout)
            var flyout = new MenuFlyout();
            
            var itemOpen = new MenuFlyoutItem { Text = "Open" };
            itemOpen.Click += (s, e) => GetMainWindow()?.RestoreWindow();
            flyout.Items.Add(itemOpen);

            var itemSettings = new MenuFlyoutItem { Text = "Settings" };
            itemSettings.Click += (s, e) => 
            {
                 var w = GetMainWindow();
                 if (w != null)
                 {
                     w.RestoreWindow();
                     w.NavigateToSettings();
                 }
            };
            flyout.Items.Add(itemSettings);

            var itemExit = new MenuFlyoutItem { Text = "Exit" };
            itemExit.Click += (s, e) => GetMainWindow()?.ForceClose();
            flyout.Items.Add(itemExit);

            TrayIcon.ContextFlyout = flyout;

            // Double click to restore
            TrayIcon.DoubleTapped += (s, e) => GetMainWindow()?.RestoreWindow();
            
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

        #region App Time Limit Checker

        private static DispatcherTimer notifierTimer;
        private static List<string> notifiedApps = new List<string>();
        private static List<string> warnNotifiedApps = new List<string>();

        public static void InitNotifierTimer()
        {
            TimeSpan intervalDuration = TimeSpan.FromSeconds(CHECK_INTERVAL);

            notifierTimer = new DispatcherTimer() { Interval = intervalDuration };
            notifierTimer.Tick += (s, e) => CheckForExceedingAppTimeLimits();

            notifierTimer.Start();
        }

        private static async void CheckForExceedingAppTimeLimits()
        {
            try
            {
                // Get Source Data
                List<AppUsage> todayUsage = await AppUsageViewModel.GetData(DateTime.Now);
                var _limits = UserPreferences.AppTimeLimits; // Used SettingsManager in WPF, but here UserPreferences seems to hold settings?
                // Wait, UserPreferences.cs handles exclusions.
                // Does it handle TimeLimits? I need to check SettingsPage.
                // SettingsPage.xaml.cs used 'UserPreferences.AppTimeLimits' (List)? 
                // Or maybe SettingsManager was ported?
                // I need to verify where TimeLimits are stored in WinUI 3.
                // In a previous turn I viewed UserPreferences.cs and it had ExcludedApps.
                // I suspect AppTimeLimits might be missing or I missed it.
                // I'll check UserPreferences.cs AFTER writing this stub, and fix if needed.
                // For now assuming UserPreferences.AppTimeLimits exists (Dictionary<string, int>).

                if (_limits == null) return;

                // Get Active Process / Program
                IntPtr _hnd = ForegroundWindowManager.GetForegroundWindow();
                uint _procId = ForegroundWindowManager.GetForegroundProcessId(_hnd);
                Process _proc = Process.GetProcessById((int)_procId);
                string activeProcessName = ForegroundWindowManager.GetActiveProcessName(_proc);

                AppUsage currApp = todayUsage.SingleOrDefault(app => app.ProcessName == activeProcessName);

                if (currApp == null) return;

                // Skip if already notified
                if (notifiedApps.Contains(currApp.ProcessName)) return;

                // If app has time limit
                if (_limits.ContainsKey(currApp.ProcessName))
                {
                    TimeSpan timeLimit = TimeSpan.FromMinutes(_limits[currApp.ProcessName]);

                    bool reachedWarnLimit = currApp.Duration > (timeLimit - warningLimit);
                    bool reachedTimeLimit = currApp.Duration > timeLimit;

                    if (reachedTimeLimit && !notifiedApps.Contains(currApp.ProcessName))
                    {
                        warnNotifiedApps.Add(currApp.ProcessName);
                        notifiedApps.Add(currApp.ProcessName);

                        GetMainWindow()?.ShowAlertUsage(currApp, timeLimit);
                    }
                    else if (reachedWarnLimit && !warnNotifiedApps.Contains(currApp.ProcessName))
                    {
                        warnNotifiedApps.Add(currApp.ProcessName);

                        GetMainWindow()?.ShowAlertUsage(currApp, timeLimit, true);
                    }
                }
            }
            catch (Exception ex)
            {
                 // Ignore errors
                 Debug.WriteLine($"Notifier Error: {ex.Message}");
            }
        }

        public static void ResetNotificationForApp(string processName)
        {
            notifiedApps.RemoveAll(p => p == processName);
            warnNotifiedApps.RemoveAll(p => p == processName);
        }
        #endregion
    }
}
