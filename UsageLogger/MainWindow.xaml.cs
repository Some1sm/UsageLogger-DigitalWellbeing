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
using UsageLogger.Helpers;
using UsageLogger.Views;
using System.Windows.Input;
using Microsoft.UI.Composition.SystemBackdrops;

namespace UsageLogger
{
    public sealed partial class MainWindow : Window
    {
        public ICommand OpenCommand { get; private set; }
        public ICommand SettingsCommand { get; private set; }
        public ICommand ExitCommand { get; private set; }

        public MainWindow()
        {
            this.InitializeComponent();
            Title = "UsageLogger";
            
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

            // Apply Backdrop (Mica/Acrylic/None)
            ApplyBackdrop();
            
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
                var processes = System.Diagnostics.Process.GetProcessesByName("UsageLoggerService");
                if (processes.Length > 0) return;

                string[] possiblePaths = new string[]
                {
                    System.IO.Path.Combine(AppContext.BaseDirectory, "UsageLoggerService.exe"),
                    System.IO.Path.Combine(AppContext.BaseDirectory, "Service", "UsageLoggerService.exe")
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
                         case "MiscData":
                             ContentFrame.Navigate(typeof(MiscDataPage));
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
            Helpers.Notifier.Init(this);
        }

        private void M_AppWindow_Closing(Microsoft.UI.Windowing.AppWindow sender, Microsoft.UI.Windowing.AppWindowClosingEventArgs args)
        {
            if (Helpers.UserPreferences.MinimizeOnExit)
            {
                // Service stays running with tray - just exit UI to save RAM
                Helpers.Notifier.Dispose();
                Application.Current.Exit();
            }
            else
            {
                // Full exit - stop everything including the Service/tray
                StopService();
                Helpers.Notifier.Dispose();
                Application.Current.Exit();
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
             // Ensure execution on UI thread
             this.DispatcherQueue.TryEnqueue(() => 
             {
                 IntPtr hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                 
                 // 1. Force restore if minimized
                 NativeMethods.ShowWindow(hWnd, NativeMethods.SW_RESTORE);
                 
                 // 2. Try simple SetForegroundWindow
                 NativeMethods.SetForegroundWindow(hWnd);
                 
                 // 3. Try SwitchToThisWindow (often works where SetForeground fails)
                 NativeMethods.SwitchToThisWindow(hWnd, true);
                 
                 this.PostRestore();
             });
        }
        
        // Helper hooks to ensure UI updates if needed
        private void PreMinimize()
        {
            // Pause refresh timer and clear data when minimized to conserve RAM
            if (ContentFrame.Content is DayAppUsagePage page)
            {
                page.ViewModel?.StopTimer();
                page.ViewModel?.ClearData();
            }
        }
        private void PostRestore() 
        {
             if (ContentFrame.Content is DayAppUsagePage page)
             {
                 page.ViewModel?.StartTimer();
                 page.ViewModel?.LoadWeeklyData();
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
                var processes = System.Diagnostics.Process.GetProcessesByName("UsageLoggerService");
                foreach (var proc in processes)
                {
                    try { proc.Kill(); } catch { }
                }
            }
            catch { }
        }

        // ShowAlertUsage removed - replaced by TimeLimitEnforcer logic in AppUsageViewModel

        private void LogDebug(string message)
        {
            System.Diagnostics.Debug.WriteLine($"[MainWindow] {message}");
        }

        #endregion

        #region Backdrop

        /// <summary>
        /// Applies the window backdrop based on UserPreferences.BackdropType.
        /// Options: Mica (default), MicaAlt, Acrylic, None (opaque).
        /// </summary>
        public void ApplyBackdrop()
        {
            try
            {
                string backdropType = UserPreferences.BackdropType ?? "Mica";

                switch (backdropType)
                {
                    case "MicaAlt":
                        this.SystemBackdrop = new Microsoft.UI.Xaml.Media.MicaBackdrop
                        {
                            Kind = MicaKind.BaseAlt
                        };
                        RootGrid.Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
                        break;

                    case "Acrylic":
                        this.SystemBackdrop = new Microsoft.UI.Xaml.Media.DesktopAcrylicBackdrop();
                        RootGrid.Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
                        break;

                    case "None":
                        this.SystemBackdrop = null;
                        // Restore opaque background
                        RootGrid.Background = (SolidColorBrush)Application.Current.Resources["ApplicationPageBackgroundThemeBrush"];
                        break;

                    case "Mica":
                    default:
                        this.SystemBackdrop = new Microsoft.UI.Xaml.Media.MicaBackdrop();
                        RootGrid.Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
                        break;
                }

                System.Diagnostics.Debug.WriteLine($"[MainWindow] Applied backdrop: {backdropType}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainWindow] Failed to apply backdrop: {ex.Message}");
            }
        }

        #endregion
    }
}
