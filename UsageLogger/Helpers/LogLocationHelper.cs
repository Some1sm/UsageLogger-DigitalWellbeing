using System;
using System.Diagnostics;
using UsageLogger.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace UsageLogger.Helpers;

/// <summary>
/// Encapsulates log location management logic for the Settings page.
/// Extracted from SettingsPage.xaml.cs.
/// </summary>
public static class LogLocationHelper
{
    public static void LoadLogLocation(TextBox txtLogLocation)
    {
        string customPath = ApplicationPath.GetCustomLogsFolderRaw();
        txtLogLocation.Text = string.IsNullOrEmpty(customPath) ? ApplicationPath.UsageLogsFolder : customPath;
    }

    public static void BrowseLogLocation(TextBox txtLogLocation, Window mainWindow, Action checkForChanges)
    {
        try
        {
            var dialog = new NativeFolderDialog();
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(mainWindow);

            string selectedPath = dialog.ShowDialog(hwnd, txtLogLocation.Text);
            if (!string.IsNullOrEmpty(selectedPath))
            {
                txtLogLocation.Text = selectedPath;
                checkForChanges?.Invoke();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"FolderPicker failed: {ex.Message}");
        }
    }

    public static void ResetLogLocation(TextBox txtLogLocation, Action restartService)
    {
        ApplicationPath.ClearCustomLogsFolder();
        txtLogLocation.Text = ApplicationPath.UsageLogsFolder;
        restartService?.Invoke();
    }

    public static void OpenLogFolder()
    {
        string logFolder = ApplicationPath.UsageLogsFolder;
        if (System.IO.Directory.Exists(logFolder))
        {
            Process.Start(new ProcessStartInfo(logFolder) { UseShellExecute = true });
        }
    }
}
