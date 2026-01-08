using DigitalWellbeingWinUI3.Helpers;
using DigitalWellbeing.Core.Helpers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace DigitalWellbeingWinUI3.ViewModels
{
    /// <summary>
    /// Handles time limit enforcement including notifications and dialogs.
    /// </summary>
    public static class TimeLimitEnforcer
    {
        // Track apps that have already been notified today to avoid spam
        private static HashSet<string> _notifiedToday = new HashSet<string>();
        private static DateTime _lastResetDate = DateTime.MinValue;

        /// <summary>
        /// Checks if any time limits are exceeded and shows notifications/dialogs.
        /// </summary>
        /// <param name="appUsages">Dictionary of process name to total duration</param>
        /// <param name="titleUsages">Dictionary of "ProcessName|Title" to duration</param>
        /// <param name="xamlRoot">XamlRoot for showing dialogs</param>
        public static async Task CheckTimeLimitsAsync(
            Dictionary<string, TimeSpan> appUsages,
            Dictionary<string, TimeSpan> titleUsages,
            XamlRoot xamlRoot)
        {
            // Reset notifications at midnight
            if (_lastResetDate.Date != DateTime.Now.Date)
            {
                _notifiedToday.Clear();
                _lastResetDate = DateTime.Now;
            }

            // Check app time limits
            foreach (var kvp in UserPreferences.AppTimeLimits)
            {
                string processName = kvp.Key;
                int limitMinutes = kvp.Value;
                
                if (appUsages.TryGetValue(processName, out TimeSpan usage))
                {
                    if (usage.TotalMinutes >= limitMinutes && !_notifiedToday.Contains(processName))
                    {
                        _notifiedToday.Add(processName);
                        await ShowTimeLimitDialog(processName, null, usage, TimeSpan.FromMinutes(limitMinutes), xamlRoot);
                    }
                }
            }

            // Check title time limits
            foreach (var kvp in UserPreferences.TitleTimeLimits)
            {
                string key = kvp.Key;  // "ProcessName|Title"
                int limitMinutes = kvp.Value;
                
                if (titleUsages.TryGetValue(key, out TimeSpan usage))
                {
                    if (usage.TotalMinutes >= limitMinutes && !_notifiedToday.Contains(key))
                    {
                        _notifiedToday.Add(key);
                        // Parse process name from key
                        string[] parts = key.Split('|');
                        string processName = parts[0];
                        string title = parts.Length > 1 ? parts[1] : key;
                        await ShowTimeLimitDialog(processName, title, usage, TimeSpan.FromMinutes(limitMinutes), xamlRoot);
                    }
                }
            }
        }

        /// <summary>
        /// Shows a dialog when time limit is exceeded.
        /// </summary>
        private static async Task ShowTimeLimitDialog(string processName, string title, TimeSpan usage, TimeSpan limit, XamlRoot xamlRoot)
        {
            if (xamlRoot == null) return;

            string displayName = title ?? UserPreferences.GetDisplayName(processName);
            string key = title != null ? $"{processName}|{title}" : processName;
            bool isTitle = title != null;

            // Show toast notification first
            ShowToastNotification(displayName, usage, limit);

            // Then show dialog
            var dialog = new ContentDialog
            {
                Title = "⏰ Time Limit Exceeded",
                Content = $"You've spent {StringHelper.FormatDurationCompact(usage)} on '{displayName}'.\n\nYour limit was {StringHelper.FormatDurationCompact(limit)}.",
                PrimaryButtonText = "Ignore",
                SecondaryButtonText = "+5 min",
                CloseButtonText = "Close App",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = xamlRoot
            };

            var result = await dialog.ShowAsync();

            if (result == ContentDialogResult.Secondary)
            {
                // Add 5 minutes to the limit
                if (isTitle)
                {
                    int currentLimit = UserPreferences.TitleTimeLimits.GetValueOrDefault(key, 0);
                    UserPreferences.UpdateTitleTimeLimit(key, TimeSpan.FromMinutes(currentLimit + 5));
                }
                else
                {
                    int currentLimit = UserPreferences.AppTimeLimits.GetValueOrDefault(processName, 0);
                    UserPreferences.UpdateAppTimeLimit(processName, TimeSpan.FromMinutes(currentLimit + 5));
                }
                // Allow re-notification after the new limit
                _notifiedToday.Remove(key.Length > 0 ? key : processName);
            }
            else if (result == ContentDialogResult.None) // CloseButtonText
            {
                // Close the app
                CloseProcess(processName);
            }
            // Ignore = do nothing, dialog closes
        }

        /// <summary>
        /// Shows a toast notification for time limit exceeded.
        /// </summary>
        private static void ShowToastNotification(string appName, TimeSpan usage, TimeSpan limit)
        {
            try
            {
                var builder = new AppNotificationBuilder()
                    .AddText($"⏰ Time Limit Exceeded: {appName}")
                    .AddText($"You've spent {StringHelper.FormatDurationCompact(usage)} (limit: {StringHelper.FormatDurationCompact(limit)})");

                var notification = builder.BuildNotification();
                AppNotificationManager.Default.Show(notification);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to show toast notification: {ex.Message}");
            }
        }

        /// <summary>
        /// Closes all processes with the given name.
        /// </summary>
        private static void CloseProcess(string processName)
        {
            try
            {
                var processes = Process.GetProcessesByName(processName);
                foreach (var proc in processes)
                {
                    try
                    {
                        proc.CloseMainWindow();
                        // Give it a moment to close gracefully
                        if (!proc.WaitForExit(2000))
                        {
                            proc.Kill();
                        }
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to close process {processName}: {ex.Message}");
            }
        }


        
        /// <summary>
        /// Allows manually resetting the notification state (e.g., for testing).
        /// </summary>
        public static void ResetNotifications()
        {
            _notifiedToday.Clear();
        }
    }
}
