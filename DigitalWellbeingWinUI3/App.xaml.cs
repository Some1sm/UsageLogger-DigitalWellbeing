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

using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;

namespace DigitalWellbeingWinUI3
{
    public partial class App : Application
    {
        public App()
        {
            this.InitializeComponent();
            this.UnhandledException += App_UnhandledException; // Handle unhandled exceptions

            LiveCharts.Configure(config =>
                config
                    .AddSkiaSharp()
                    .AddDefaultMappers()
                    .AddDarkTheme()
            );
        }

        private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            LogCrash(e.Exception);
            e.Handled = true; // Try to keep alive to show message? No, just log.
        }

        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            try
            {
                // Load user preferences before initializing UI
                Helpers.UserPreferences.Load();
                
                m_window = new MainWindow();
                m_window.Activate();
            }
            catch (Exception ex)
            {
                LogCrash(ex);
            }
        }

        private void LogCrash(Exception ex)
        {
            try
            {
                string path = System.IO.Path.Combine(AppContext.BaseDirectory, "startup_crash.txt");
                string message = $"[{DateTime.Now}] CRASH: {ex.ToString()}\n\nSTACK: {ex.StackTrace}\n\nINNER: {ex.InnerException?.ToString()}";
                System.IO.File.AppendAllText(path, message);
                
                // Also try to show a message box if possible (native)
                // or just rely on file.
            }
            catch { }
        }

        public Window m_window;
    }
}
