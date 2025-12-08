using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Runtime.InteropServices;
using Windows.Foundation;
using Windows.Foundation.Collections;
using DigitalWellbeingWinUI3.Views;

namespace DigitalWellbeingWinUI3
{
    public sealed partial class MainWindow : Window
    {
        public MainWindow()
        {
            this.InitializeComponent();
            Title = "Digital Wellbeing";
            
            // Apply Theme
            try
            {
                var theme = Helpers.UserPreferences.ThemeMode;
                if (!string.IsNullOrEmpty(theme) && theme != "System")
                {
                    if (Enum.TryParse(theme, out ElementTheme rTheme))
                    {
                        (this.Content as FrameworkElement).RequestedTheme = rTheme;
                    }
                }
            }
            catch { }
            
            InitWindowManagement();
            
            // Custom Title Bar
            ExtendsContentIntoTitleBar = true;
            SetTitleBar(AppTitleBar);
        }

        private void NavView_Loaded(object sender, RoutedEventArgs e)
        {
            NavView.SelectedItem = NavView.MenuItems[0];
            ContentFrame.Navigate(typeof(DayAppUsagePage));
        }

        private void NavView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
        {
            if (args.IsSettingsInvoked)
            {
                ContentFrame.Navigate(typeof(SettingsPage));
            }
            else
            {
                var item = args.InvokedItemContainer as NavigationViewItem;
                if (item != null) 
                {
                    System.Diagnostics.Debug.WriteLine($"[Nav] Invoked: {item.Tag}");
                    switch(item.Tag.ToString())
                    {
                        case "Dashboard":
                            ContentFrame.Navigate(typeof(DayAppUsagePage));
                            break;
                         case "History":
                            // Navigate to History Page
                            ContentFrame.Navigate(typeof(HistoryPage));
                            break;
                         case "Settings":
                             ContentFrame.Navigate(typeof(SettingsPage));
                             break;
                    }
                }
            }
        }
        public void NavigateToDashboard()
        {
             NavView.SelectedItem = NavView.MenuItems[0];
             ContentFrame.Navigate(typeof(DayAppUsagePage));
        }

        public void NavigateToSettings()
        {
             NavView.SelectedItem = NavView.SettingsItem;
             ContentFrame.Navigate(typeof(SettingsPage));
        }

        #region Window Management & Notifier

        private Microsoft.UI.Windowing.AppWindow m_AppWindow;

        public void InitWindowManagement()
        {
            // Get AppWindow
            IntPtr hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            Microsoft.UI.WindowId windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
            m_AppWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
            
            // Set Icon
            string iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "icon.ico");
            if (File.Exists(iconPath))
            {
                m_AppWindow.SetIcon(iconPath);
            }
            
            // Handle Closing
            m_AppWindow.Closing += M_AppWindow_Closing;

            // Init Notifier
            Helpers.Notifier.Init(TrayIcon);
            Helpers.Notifier.InitNotifierTimer();
        }

        private void M_AppWindow_Closing(Microsoft.UI.Windowing.AppWindow sender, Microsoft.UI.Windowing.AppWindowClosingEventArgs args)
        {
            if (Helpers.UserPreferences.MinimizeOnExit)
            {
                args.Cancel = true;
                MinimizeToTray();
            }
            else
            {
                // Clean exit
                Helpers.Notifier.Dispose();
            }
        }

        public void MinimizeToTray()
        {
            this.PreMinimize();
            // WinUI 3 doesn't hide window easily without using Win32 ShowWindow
             IntPtr hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
             PInvoke.ShowWindow(hWnd, 0); // SW_HIDE
        }

        public void RestoreWindow()
        {
             IntPtr hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
             PInvoke.ShowWindow(hWnd, 9); // SW_RESTORE
             PInvoke.SetForegroundWindow(hWnd);
             this.PostRestore();
        }
        
        // Helper hooks to ensure UI updates if needed
        private void PreMinimize() { }
        private void PostRestore() 
        {
             // Refresh current page
             if (ContentFrame.Content is DayAppUsagePage page)
             {
                 page.ViewModel?.RefreshDayView();
             }
        }

        public void ForceClose()
        {
            Helpers.Notifier.Dispose();
            Application.Current.Exit();
        }

        public void ShowAlertUsage(DigitalWellbeing.Core.Models.AppUsage app, TimeSpan timeLimit, bool warnOnly = false)
        {
             this.DispatcherQueue.TryEnqueue(() =>
             {
                 if (warnOnly)
                 {
                     Helpers.Notifier.ShowNotification(
                         $"Warning for {app.ProgramName}",
                         $"You have less than 15m left. Used: {DigitalWellbeing.Core.Helpers.StringHelper.TimeSpanToShortString(app.Duration)}.",
                         (s, e) => RestoreWindow(),
                         null
                     );
                 }
                 else
                 {
                     RestoreWindow();
                     AlertWindow alert = new AlertWindow(app, timeLimit);
                     alert.Activate();
                 }
             });
        }
        #endregion
    }

    // Simple PInitialoke wrapper class
    internal static class PInvoke
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);
    }
}
