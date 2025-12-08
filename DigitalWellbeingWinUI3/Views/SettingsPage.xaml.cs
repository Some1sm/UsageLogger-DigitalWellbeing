using DigitalWellbeing.Core;
using DigitalWellbeingWinUI3.Helpers;
using DigitalWellbeingWinUI3.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Windows.ApplicationModel;

namespace DigitalWellbeingWinUI3.Views
{
    public sealed partial class SettingsPage : Page
    {
        private const string APP_TIMELIMIT_SEPARATOR = "    /    ";

        public SettingsPage()
        {
            this.InitializeComponent();
            LoadCurrentSettings();
            
            // Version
            // var version = Package.Current.Id.Version; 
            // Package API fails in unpackaged apps.
            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            TxtCurrentVersion.Text = $"App Version {version.Major}.{version.Minor}.{version.Build}";
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            LoadExcludedProcessItems();
            LoadAppTimeLimits();
        }

        private void LoadCurrentSettings()
        {
            // Run on Startup
            EnableRunOnStartup.IsOn = SettingsManager.IsRunningOnStartup();

            // Minimize on Exit
            ToggleMinimizeOnExit.IsOn = UserPreferences.MinimizeOnExit;

            // Usage Data
            LoadExcludedProcessItems();

            // Display
            DaysToShowTextBox.Value = UserPreferences.DayAmount;
            MinDurationTextBox.Value = UserPreferences.MinumumDuration.TotalSeconds;

            // Refresh
            EnableAutoRefresh.IsOn = UserPreferences.EnableAutoRefresh;
            RefreshInterval.Value = UserPreferences.RefreshIntervalSeconds;

            // Theme
            string theme = UserPreferences.ThemeMode;
            foreach (ComboBoxItem item in CBTheme.Items)
            {
                if (item.Tag.ToString() == theme)
                {
                    CBTheme.SelectedItem = item;
                    break;
                }
            }
            if (CBTheme.SelectedItem == null) CBTheme.SelectedIndex = 0; // Default System
        }


        private void LoadExcludedProcessItems()
        {
            ExcludedAppList.Items.Clear();
            foreach (string processName in UserPreferences.UserExcludedProcesses)
            {
                ExcludedAppList.Items.Add(processName);
            }
        }

        private void LoadAppTimeLimits()
        {
            AppTimeLimitsList.Items.Clear();
            // AppTimeLimitsList.Items.Clear();
            foreach (var limit in UserPreferences.AppTimeLimits)
            {
                TimeSpan time = TimeSpan.FromMinutes(limit.Value);
                AppTimeLimitsList.Items.Add($"{limit.Key}{APP_TIMELIMIT_SEPARATOR}{time.Hours}h {time.Minutes}m");
            }
        }

        // EVENTS

        private void EnableRunOnStartup_Toggled(object sender, RoutedEventArgs e)
        {
             SettingsManager.SetRunOnStartup(EnableRunOnStartup.IsOn);
        }

        private void ToggleMinimizeOnExit_Toggled(object sender, RoutedEventArgs e)
        {
             UserPreferences.MinimizeOnExit = ToggleMinimizeOnExit.IsOn;
             UserPreferences.Save();
        }

        private void ExcludedAppList_DoubleTapped(object sender, Microsoft.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
        {
            if (ExcludedAppList.SelectedItem != null)
            {
                string processName = ExcludedAppList.SelectedItem.ToString();
                UserPreferences.UserExcludedProcesses.Remove(processName);
                UserPreferences.Save();
                LoadExcludedProcessItems();
            }
        }

        private void AppTimeLimitsList_DoubleTapped(object sender, Microsoft.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
        {
             if (AppTimeLimitsList.SelectedItem != null)
             {
                 string selected = AppTimeLimitsList.SelectedItem.ToString();
                 // Parse process name (everything before separator)
                 string processName = selected.Split(new string[] { APP_TIMELIMIT_SEPARATOR }, StringSplitOptions.None)[0];
                 
                 if (UserPreferences.AppTimeLimits.ContainsKey(processName))
                 {
                     UserPreferences.UpdateAppTimeLimit(processName, TimeSpan.Zero); // Removes it
                     LoadAppTimeLimits();
                 }
             }
        }

        private void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            UserPreferences.DayAmount = (int)DaysToShowTextBox.Value;
            UserPreferences.Save();

            AppUsageViewModel.NumberOfDaysToDisplay = UserPreferences.DayAmount;

            // Reload Usage Page (Navigate away and back or just reload model?)
            // Simplest is to navigate to Dashboard
            if (App.Current is App myApp && myApp.m_window is MainWindow window)
            {
                window.NavigateToDashboard();
            }
        }

        private void ApplyMinDuration_Click(object sender, RoutedEventArgs e)
        {
            UserPreferences.MinumumDuration = TimeSpan.FromSeconds(MinDurationTextBox.Value);
            UserPreferences.Save();
            
            // Reload
             if (App.Current is App myApp && myApp.m_window is MainWindow window)
            {
                window.NavigateToDashboard();
            }
        }

        private void EnableAutoRefresh_Toggled(object sender, RoutedEventArgs e)
        {
            UserPreferences.EnableAutoRefresh = EnableAutoRefresh.IsOn;
            UserPreferences.Save();
        }

        private void RefreshInterval_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            if (Math.Abs(sender.Value - UserPreferences.RefreshIntervalSeconds) > 0.1)
            {
                 UserPreferences.RefreshIntervalSeconds = (int)sender.Value;
                 UserPreferences.Save();
            }
        }

        private void CBTheme_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CBTheme.SelectedItem is ComboBoxItem item)
            {
                string theme = item.Tag.ToString();
                UserPreferences.ThemeMode = theme;
                UserPreferences.Save();

                if (App.Current is App myApp && myApp.m_window is MainWindow window)
                {
                    ElementTheme rTheme = ElementTheme.Default;
                    Enum.TryParse(theme, out rTheme);
                    (window.Content as FrameworkElement).RequestedTheme = rTheme;
                }
            }
        }

        private void BtnOpenAppFolder_Click(object sender, RoutedEventArgs e)
        {
             Process.Start(new ProcessStartInfo(ApplicationPath.APP_LOCATION) { UseShellExecute = true });
        }
    }
}
