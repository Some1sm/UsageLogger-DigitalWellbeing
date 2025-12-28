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
using DigitalWellbeingWinUI3.Helpers;
using DigitalWellbeingWinUI3.Views;
using System.Windows.Input;

namespace DigitalWellbeingWinUI3
{
    public sealed partial class MainWindow : Window
    {
        public ICommand OpenCommand { get; private set; }
        public ICommand SettingsCommand { get; private set; }
        public ICommand ExitCommand { get; private set; }

        public MainWindow()
        {
            this.InitializeComponent();
            Title = "Digital Wellbeing";
            
            InitializeCommands();

            // Apply Theme
            try
            {
                var theme = UserPreferences.ThemeMode;
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

            this.Activated += MainWindow_Activated;

            EnsureServiceRunning();
        }

        private void EnsureServiceRunning()
        {
            try
            {
                var processes = System.Diagnostics.Process.GetProcessesByName("DigitalWellbeingService");
                if (processes.Length > 0) return;

                string[] possiblePaths = new string[]
                {
                    System.IO.Path.Combine(AppContext.BaseDirectory, "DigitalWellbeingService.exe"),
                    System.IO.Path.Combine(AppContext.BaseDirectory, "Service", "DigitalWellbeingService.exe")
                };

                string servicePath = possiblePaths.FirstOrDefault(p => System.IO.File.Exists(p));

                if (!string.IsNullOrEmpty(servicePath))
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(servicePath) 
                    { 
                        UseShellExecute = true, 
                        WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden 
                    });
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"Service not found. Checked: {string.Join(", ", possiblePaths)}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Service start failed: {ex.Message}");
            }
        }

        private void InitializeCommands()
        {
            OpenCommand = new RelayCommand((param) => 
            {
                RestoreWindow();
            });

            SettingsCommand = new RelayCommand((param) => 
            {
                RestoreWindow();
                NavigateToSettings();
            });

            ExitCommand = new RelayCommand((param) => 
            {
                ForceClose();
                Application.Current.Exit();
            });
        }

        private async void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
        {
             // Validate context on activation/loading
             await AppTagHelper.ValidateAppTags();
        }

        private void NavView_Loaded(object sender, RoutedEventArgs e)
        {
            NavView.SelectedItem = NavView.MenuItems[0];
            ContentFrame.Navigate(typeof(DayAppUsagePage));
            UpdateIncognitoWatermark();
        }

        /// <summary>
        /// Updates the incognito watermark visibility based on the setting.
        /// Call this when the window loads or settings change.
        /// </summary>
        public void UpdateIncognitoWatermark()
        {
            IncognitoWatermark.Visibility = UserPreferences.IncognitoMode 
                ? Visibility.Visible 
                : Visibility.Collapsed;
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
                         case "Sessions":
                             ContentFrame.Navigate(typeof(SessionsPage));
                             break;
                         case "Focus":
                             ContentFrame.Navigate(typeof(FocusPage));
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
        
        // Property for external navigation access
        public Frame RootFrame => ContentFrame;
        
        // Navigate to Sessions page with a specific date
        private DateTime? _pendingSessionDate;
        public void NavigateToSessionsWithDate(DateTime date)
        {
            _pendingSessionDate = date;
            
            // Find and select Sessions nav item
            foreach (var item in NavView.MenuItems)
            {
                if (item is NavigationViewItem navItem && navItem.Tag?.ToString() == "Sessions")
                {
                    NavView.SelectedItem = navItem;
                    break;
                }
            }
            
            ContentFrame.Navigate(typeof(SessionsPage), date);
        }
        
        public DateTime? GetPendingSessionDate()
        {
            var date = _pendingSessionDate;
            _pendingSessionDate = null;
            return date;
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
            Helpers.Notifier.Init(this, TrayIcon);
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
                // Clean exit - also stop the service
                StopService();
                Helpers.Notifier.Dispose();
            }
        }

        public void MinimizeToTray()
        {
            this.PreMinimize();
            // WinUI 3 doesn't hide window easily without using Win32 ShowWindow
             IntPtr hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
             NativeMethods.ShowWindow(hWnd, NativeMethods.SW_HIDE);
        }

        public void RestoreWindow()
        {
             IntPtr hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
             NativeMethods.ShowWindow(hWnd, NativeMethods.SW_RESTORE);
             NativeMethods.SetForegroundWindow(hWnd);
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
             // Update incognito watermark in case setting changed
             UpdateIncognitoWatermark();
        }

        public void ForceClose()
        {
            StopService();
            Helpers.Notifier.Dispose();
            Application.Current.Exit();
        }

        private void StopService()
        {
            try
            {
                var processes = System.Diagnostics.Process.GetProcessesByName("DigitalWellbeingService");
                foreach (var proc in processes)
                {
                    try { proc.Kill(); } catch { }
                }
            }
            catch { }
        }

        // ShowAlertUsage removed - replaced by TimeLimitEnforcer logic in AppUsageViewModel

        private void TrayIcon_DoubleTapped(object sender, Microsoft.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
        {
            LogDebug("TrayIcon_DoubleTapped");
            RestoreWindow();
        }

        private void Open_Click(object sender, RoutedEventArgs e)
        {
            LogDebug("Open_Click");
            RestoreWindow();
        }

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            LogDebug("Settings_Click");
            RestoreWindow();
            NavigateToSettings();
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            LogDebug("Exit_Click");
            ForceClose();
            Application.Current.Exit();
        }

        private void LogDebug(string message)
        {
            System.Diagnostics.Debug.WriteLine($"[MainWindow] {message}");
        }

        #endregion
    }
}
