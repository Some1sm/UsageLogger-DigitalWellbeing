using UsageLogger.Core.Helpers;
using UsageLogger.Core;
using UsageLogger.Core.Models;
using UsageLogger.ViewModels;
using UsageLogger;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;

// Alias WinUI DispatcherTimer to avoid confusion
using DispatcherTimer = Microsoft.UI.Xaml.DispatcherTimer;

namespace UsageLogger.Helpers
{
    public static class Notifier
    {
        private static MainWindow _mainWindow;

        public static void Init(MainWindow mainWindow)
        {
            _mainWindow = mainWindow;
            
            // Register for notifications (required for unpackaged apps)
            if (AppNotificationManager.Default.Setting == AppNotificationSetting.Enabled)
            {
                // Unpackaged registration is often automatic with Bootstrapper but explicit call acts as sanity check
                // For unpackaged, we rely on the OS allowing it via the raw executable identity
            }
        }

        public static void Dispose()
        {
            // Unregister if needed, but typically AppNotificationManager.Default.Unregister() is for cleanup
            AppNotificationManager.Default.Unregister();
        }

        public static void ShowNotification(string title, string message, EventHandler clickHandler = null, object icon = null)
        {
            try
            {
                var builder = new AppNotificationBuilder()
                    .AddText(title)
                    .AddText(message);

                var notification = builder.BuildNotification();
                AppNotificationManager.Default.Show(notification);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to show notification: {ex.Message}");
            }
        }

        #region App Time Limit Checker (DEPRECATED - Moved to TimeLimitEnforcer.cs)
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
