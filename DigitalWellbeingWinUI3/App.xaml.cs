using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using System.Diagnostics;
using System.Threading.Tasks;

using WinUI3Localizer;

namespace DigitalWellbeingWinUI3
{
    public partial class App : Application
    {
        public App()
        {
            this.InitializeComponent();
            this.UnhandledException += App_UnhandledException;


        }

        private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            LogCrash(e.Exception);
            e.Handled = true;
        }

        protected override async void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            try
            {
                // Register for Toast Notifications (Required for Unpackaged)
                try
                {
                    Microsoft.Windows.AppNotifications.AppNotificationManager.Default.Register();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[App] Failed to register notifications: {ex.Message}");
                }

                // Load user preferences first
                Helpers.UserPreferences.Load();

                // Initialize WinUI3Localizer BEFORE creating any windows
                await InitializeLocalizerAsync();

                m_window = new MainWindow();
                MainWindow = (MainWindow)m_window;
                m_window.Activate();
                
                // Start a one-shot timer to show Focus reminder after UI is ready
                StartupFocusReminderTimer();
            }
            catch (Exception ex)
            {
                LogCrash(ex);
            }
        }
        
        private void StartupFocusReminderTimer()
        {
            var timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(2);
            timer.Tick += (s, e) => 
            {
                timer.Stop(); // One-shot
                
                // Set XamlRoot and show reminder
                if (m_window?.Content is FrameworkElement fe && fe.XamlRoot != null)
                {
                    Helpers.FocusManager.Instance.XamlRoot = fe.XamlRoot;
                    
                    if (Helpers.FocusManager.Instance.IsMonitoring)
                    {
                        Debug.WriteLine("[App] Showing Startup Focus Reminder");
                        Helpers.FocusManager.Instance.ShowStartupReminder();
                    }
                }
            };
            timer.Start();
        }

        /// <summary>
        /// Initializes WinUI3Localizer with the Strings folder and user's preferred language.
        /// </summary>
        private async Task InitializeLocalizerAsync()
        {
            try
            {
                // Path to the Strings folder (copied to output directory)
                string stringsFolderPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Strings");

                // Get user's preferred language
                // Empty string means "System Default" - use system language
                string preferredLanguage = Helpers.UserPreferences.LanguageCode;
                
                if (string.IsNullOrEmpty(preferredLanguage))
                {
                    // Get the system's primary language
                    var languages = Windows.System.UserProfile.GlobalizationPreferences.Languages;
                    if (languages != null && languages.Count > 0)
                    {
                        preferredLanguage = languages[0]; // e.g., "en-US", "es-ES"
                    }
                    else
                    {
                        preferredLanguage = "en-US"; // Final fallback
                    }
                    Debug.WriteLine($"[App] Using system language: {preferredLanguage}");
                }
                else
                {
                    Debug.WriteLine($"[App] Using user-selected language: {preferredLanguage}");
                }

                Debug.WriteLine($"[App] Strings folder: {stringsFolderPath}");

                // Build the localizer
                ILocalizer localizer = await new LocalizerBuilder()
                    .AddStringResourcesFolderForLanguageDictionaries(stringsFolderPath)
                    .SetOptions(options =>
                    {
                        options.DefaultLanguage = "en-US"; // Fallback for missing translations
                    })
                    .Build();

                // Set the user's preferred language
                await localizer.SetLanguage(preferredLanguage);

                Debug.WriteLine($"[App] WinUI3Localizer initialized with language: {preferredLanguage}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[App] WinUI3Localizer initialization error: {ex.Message}");
                // Continue without localization if it fails
            }
        }

        private void LogCrash(Exception ex)
        {
            try
            {
                string path = System.IO.Path.Combine(AppContext.BaseDirectory, "startup_crash.txt");
                string message = $"[{DateTime.Now}] CRASH: {ex.ToString()}\n\nSTACK: {ex.StackTrace}\n\nINNER: {ex.InnerException?.ToString()}";
                System.IO.File.AppendAllText(path, message);
            }
            catch { }
        }

        public static MainWindow MainWindow { get; private set; }
        public Window m_window;
    }
}
