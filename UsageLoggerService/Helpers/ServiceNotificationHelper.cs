#nullable enable
using System;
using System.Collections.Generic;

namespace UsageLoggerService.Helpers;

/// <summary>
/// Handles toast notifications for the background service.
/// Extracted from ActivityLogger to separate UI concerns from logging logic.
/// </summary>
public static class ServiceNotificationHelper
{
    private const int TOAST_DEBOUNCE_MINUTES = 1;

    /// <summary>
    /// Shows a tray toast notification with debouncing to prevent spam.
    /// </summary>
    public static void ShowToast(string title, string message, Dictionary<string, DateTime> lastAlertTracker)
    {
        string key = $"Toast_{title}_{message}";
        if (lastAlertTracker.TryGetValue(key, out DateTime lastAlert))
        {
            if ((DateTime.Now - lastAlert).TotalMinutes < TOAST_DEBOUNCE_MINUTES) return;
        }
        lastAlertTracker[key] = DateTime.Now;

        try
        {
            TrayManager.ShowNotification(title, message);
        }
        catch (Exception ex)
        {
            ServiceLogger.Log("Toast", ex.Message);
        }
    }
}
