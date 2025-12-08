using DigitalWellbeing.Core.Helpers;
using DigitalWellbeing.Core.Models;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using System;
using System.Diagnostics;

namespace DigitalWellbeingWinUI3.Views
{
    public sealed partial class AlertWindow : Window
    {
        private string _processName = "";
        private AppWindow m_AppWindow;

        public AlertWindow(AppUsage appUsage, TimeSpan limit)
        {
            this.InitializeComponent();
            
            // Get AppWindow
            IntPtr hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            Microsoft.UI.WindowId windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
            m_AppWindow = AppWindow.GetFromWindowId(windowId);

            // Resize (WinUI 3 windows are large by default)
            m_AppWindow.Resize(new Windows.Graphics.SizeInt32(400, 250));
            
            // Set Title
            this.Title = "Time Limit Reached";

            _processName = appUsage.ProcessName;

            RunProgramName.Text = appUsage.ProgramName;
            RunProcessName.Text = appUsage.ProcessName;
            RunUsageTime.Text = StringHelper.TimeSpanToShortString(appUsage.Duration);
            RunTimeLimit.Text = StringHelper.TimeSpanToShortString(limit);

            BtnCloseApp.Content = $"Exit ({appUsage.ProcessName})";
            
            // Try to set topmost
            m_AppWindow.SetPresenter(AppWindowPresenterKind.CompactOverlay);
            m_AppWindow.SetPresenter(AppWindowPresenterKind.Default); // Switch back to allow moving/resizing but this hack might not work for Just Topmost.
            // Actually CompactOverlay is "Always On Top" which is what we want for an Alert?
            // But it removes minimize/maximize buttons. That's probably fine for an alert.
            m_AppWindow.SetPresenter(AppWindowPresenterKind.CompactOverlay); 
        }

        private void BtnOK_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void BtnCloseApp_Click(object sender, RoutedEventArgs e)
        {
            Process[] ps = Process.GetProcessesByName(_processName);

            try
            {
                foreach (Process p in ps)
                {
                    p.CloseMainWindow();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Cannot close app: {ex.Message}");
            }
            finally
            {
                this.Close();
            }
        }
    }
}
