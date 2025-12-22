using DigitalWellbeing.Core;
using DigitalWellbeingWinUI3.Helpers;
using DigitalWellbeingWinUI3.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Collections.ObjectModel;
using System.Linq;
using Windows.ApplicationModel;
using DigitalWellbeingWinUI3.Models;

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

        private bool _isLoading = false;
        private bool _isDirty = false;

        private void MarkDirty()
        {
            if (_isLoading) return;
            _isDirty = true;
            UnsavedChangesBanner.Visibility = Visibility.Visible;
        }

        private void MarkClean()
        {
            _isDirty = false;
            UnsavedChangesBanner.Visibility = Visibility.Collapsed;
        }

        private void LoadCurrentSettings()
        {
            _isLoading = true;

            // Run on Startup
            EnableRunOnStartup.IsOn = SettingsManager.IsRunningOnStartup();

            // Minimize on Exit
            ToggleMinimizeOnExit.IsOn = UserPreferences.MinimizeOnExit;
            
            // Incognito
            ToggleIncognitoMode.IsOn = UserPreferences.IncognitoMode;

            // Usage Data
            LoadExcludedProcessItems();
            LoadTags();

            // Display
            DaysToShowTextBox.Value = UserPreferences.DayAmount;
            MinDurationTextBox.Value = UserPreferences.MinumumDuration.TotalSeconds;

            // Refresh
            EnableAutoRefresh.IsOn = UserPreferences.EnableAutoRefresh;
            RefreshInterval.Value = UserPreferences.RefreshIntervalSeconds;
            
            // Detailed Usage Days
            DetailedDaysTextBox.Value = UserPreferences.DetailedUsageDayCount;

             // Timeline Threshold
            TimelineThresholdTextBox.Value = UserPreferences.TimelineMergeThresholdSeconds;
            
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

            // Combined Audio View
            ToggleCombinedAudioView.IsOn = UserPreferences.ShowCombinedAudioView;

            // Log Location
            LoadLogLocation();

            _isLoading = false;
            MarkClean();
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
             MarkDirty();
        }

        private void ToggleMinimizeOnExit_Toggled(object sender, RoutedEventArgs e)
        {
             MarkDirty();
        }

        private void ToggleIncognitoMode_Toggled(object sender, RoutedEventArgs e)
        {
            MarkDirty();
            // Update watermark immediately for visual feedback
            UserPreferences.IncognitoMode = ToggleIncognitoMode.IsOn;
            App.MainWindow?.UpdateIncognitoWatermark();
        }

        private void ExcludedAppList_DoubleTapped(object sender, Microsoft.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
        {
            if (ExcludedAppList.SelectedItem != null)
            {
                string processName = ExcludedAppList.SelectedItem.ToString();
                UserPreferences.UserExcludedProcesses.Remove(processName);
                UserPreferences.Save(); // Keeping immediate save for lists
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

        private void ApplyAll_Click(object sender, RoutedEventArgs e)
        {
            // Gather all values
            UserPreferences.DayAmount = (int)DaysToShowTextBox.Value;
            UserPreferences.DetailedUsageDayCount = (int)DetailedDaysTextBox.Value;
            UserPreferences.MinumumDuration = TimeSpan.FromSeconds(MinDurationTextBox.Value);
            UserPreferences.TimelineMergeThresholdSeconds = (int)TimelineThresholdTextBox.Value;
            
            UserPreferences.MinimizeOnExit = ToggleMinimizeOnExit.IsOn;
            UserPreferences.IncognitoMode = ToggleIncognitoMode.IsOn;
            UserPreferences.EnableAutoRefresh = EnableAutoRefresh.IsOn;
            UserPreferences.RefreshIntervalSeconds = (int)RefreshInterval.Value;

            // Startup
            SettingsManager.SetRunOnStartup(EnableRunOnStartup.IsOn);

            // Theme
            if (CBTheme.SelectedItem is ComboBoxItem item)
            {
                string theme = item.Tag.ToString();
                UserPreferences.ThemeMode = theme;
                
                if (App.Current is App myApp && myApp.m_window is MainWindow window)
                {
                    ElementTheme rTheme = ElementTheme.Default;
                    Enum.TryParse(theme, out rTheme);
                    (window.Content as FrameworkElement).RequestedTheme = rTheme;
                }
            }

            // Save
            UserPreferences.Save();
            
            // Update Runtime Models
            AppUsageViewModel.NumberOfDaysToDisplay = UserPreferences.DayAmount;

            MarkClean();

            // Refresh UX by navigating to Dashboard
            if (App.Current is App myApp2 && myApp2.m_window is MainWindow window2)
            {
                window2.NavigateToDashboard();
            }
        }

        private void Settings_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            MarkDirty();
        }

        private void EnableAutoRefresh_Toggled(object sender, RoutedEventArgs e)
        {
            MarkDirty();
        }

        private void RefreshInterval_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            MarkDirty();
        }

        private void CBTheme_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            MarkDirty();
        }

        private void ToggleCombinedAudioView_Toggled(object sender, RoutedEventArgs e)
        {
            if (_isLoading) return;
            UserPreferences.ShowCombinedAudioView = ToggleCombinedAudioView.IsOn;
            UserPreferences.Save();
        }

        private void ApplyTimelineThreshold_Click(object sender, RoutedEventArgs e)
        {
            // Removed
        }

        private void BtnOpenAppFolder_Click(object sender, RoutedEventArgs e)
        {
             Process.Start(new ProcessStartInfo(ApplicationPath.APP_LOCATION) { UseShellExecute = true });
        }
        public ObservableCollection<CustomAppTag> Tags { get; set; } = new ObservableCollection<CustomAppTag>();

        private void LoadTags()
        {
            Tags.Clear();
            foreach (var tag in UserPreferences.CustomTags)
            {
                Tags.Add(tag);
            }
        }

        private void BtnAddTag_Click(object sender, RoutedEventArgs e)
        {
            int nextId = Tags.Count > 0 ? Tags.Max(t => t.Id) + 1 : 0;
            // Ensure ID doesn't conflict (basic check)
            while (Tags.Any(t => t.Id == nextId)) nextId++;

            var newTag = new CustomAppTag(nextId, "New Tag", "#808080"); // Gray default
            Tags.Add(newTag);
            SaveTags();
        }

        private void BtnDeleteTag_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is int id)
            {
                // Prevent deleting default tags if desired, or allow it.
                // For now allow it, but maybe warn? User wants to "edit existing ones".
                // Let's remove constraint for now.
                var tagToRemove = Tags.FirstOrDefault(t => t.Id == id);
                if (tagToRemove != null)
                {
                    // Clean up assigned apps
                    DigitalWellbeingWinUI3.Helpers.AppTagHelper.RemoveTag(tagToRemove.Id);
                    
                    Tags.Remove(tagToRemove);
                    SaveTags();
                }
            }
        }

        private void TagName_LostFocus(object sender, RoutedEventArgs e)
        {
            // Save when name editing finishes
            SaveTags();
        }

        private void ColorPicker_ColorChanged(ColorPicker sender, ColorChangedEventArgs args)
        {
            if (sender.Tag is int id)
            {
                var tag = Tags.FirstOrDefault(t => t.Id == id);
                if (tag != null)
                {
                    tag.HexColor = args.NewColor.ToString(); // #AARRGGBB
                    SaveTags();
                    
                    // Force refresh list item to update button background immediately if binding doesn't catch it
                    // Binding OneWay/TwoWay on HexColor might rely on PropertyChanged which CustomAppTag doesn't implement yet.
                    // Ideally CustomAppTag should implement INotifyPropertyChanged.
                    // For now, we save. The button background binding might need a converter or direct update.
                    // Actually, let's update the button background directly if we can, or just rely on binding if model notifies.
                    // Since CustomAppTag is POCO, UI won't update automatically.
                    // We can re-assign the list or implement INPC.
                    // Quick fix: Re-load tags? No, that resets scroll.
                    // Better: Implement INPC in CustomAppTag. Or just let it be for now and see if user complains.
                    // Actually, I can optimize this later.
                }
            }
        }
        
        // Alternative: Use Flyout Closed event to save color.
        private void ColorPickerFlyout_Closed(object sender, object e)
        {
             SaveTags();
             // Refresh list to show new color if needed
             var copy = new ObservableCollection<CustomAppTag>(Tags);
             Tags.Clear();
             foreach(var t in copy) Tags.Add(t);
        }

        private void SaveTags()
        {
            UserPreferences.CustomTags = Tags.ToList();
            UserPreferences.Save();
        }

        public Microsoft.UI.Xaml.Media.SolidColorBrush GetBrush(string hex)
        {
            return new Microsoft.UI.Xaml.Media.SolidColorBrush(DigitalWellbeingWinUI3.Helpers.ColorHelper.GetColorFromHex(hex));
        }

        // ===== Log Location =====
        private void LoadLogLocation()
        {
            string customPath = ApplicationPath.GetCustomLogsFolderRaw();
            TxtLogLocation.Text = string.IsNullOrEmpty(customPath) ? ApplicationPath.UsageLogsFolder : customPath;
        }

        private async void BtnBrowseLogLocation_Click(object sender, RoutedEventArgs e)
        {
            var picker = new Windows.Storage.Pickers.FolderPicker();
            picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
            picker.FileTypeFilter.Add("*");
            
            // Initialize with window handle for WinUI 3
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

            var folder = await picker.PickSingleFolderAsync();
            if (folder != null)
            {
                string newPath = folder.Path;
                ApplicationPath.SetCustomLogsFolder(newPath);
                TxtLogLocation.Text = newPath;
                
                // Restart Service
                RestartBackgroundService();
            }
        }

        private void BtnResetLogLocation_Click(object sender, RoutedEventArgs e)
        {
            ApplicationPath.ClearCustomLogsFolder();
            TxtLogLocation.Text = ApplicationPath.UsageLogsFolder;
            
            // Restart Service
            RestartBackgroundService();
        }

        private void RestartBackgroundService()
        {
            try
            {
                // Find and kill existing service process
                var processes = Process.GetProcessesByName("DigitalWellbeingService");
                foreach (var proc in processes)
                {
                    proc.Kill();
                    proc.WaitForExit(3000);
                }

                // Start new instance
                string servicePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Service", "DigitalWellbeingService.exe");
                if (System.IO.File.Exists(servicePath))
                {
                    Process.Start(new ProcessStartInfo(servicePath) { UseShellExecute = true, WindowStyle = ProcessWindowStyle.Hidden });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Service restart failed: {ex.Message}");
            }
        }
    }
}
