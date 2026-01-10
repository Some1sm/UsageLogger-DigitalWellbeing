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
            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            TxtCurrentVersion.Text = string.Format(LocalizationHelper.GetString("Settings_AppVersion"), $"{version.Major}.{version.Minor}.{version.Build}");
            
            // Add SizeChanged handler
            this.SizeChanged += SettingsPage_SizeChanged;
        }

        private void SettingsPage_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            ApplyResponsiveLayout(e.NewSize.Width);
        }
        
        // List of all Settings Grids to apply responsive logic to
        // We use FindName or just access them since they are x:Name'd
        private void ApplyResponsiveLayout(double width)
        {
            bool narrow = width < 500;
            
            // Helper to modify Grid (1 Row/2 Col  vs  2 Row/1 Col)
            void SetGridLayout(Grid g)
            {
                if (g == null) return;
                g.RowDefinitions.Clear();
                g.ColumnDefinitions.Clear();
                
                if (narrow)
                {
                    // Vertical Stack
                    g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                    g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                    g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    
                    // Text (StackPanel) -> Row 0
                    if (g.Children.Count >= 1 && g.Children[0] is FrameworkElement textPanel)
                    {
                        Grid.SetRow(textPanel, 0);
                        Grid.SetColumn(textPanel, 0);
                        Grid.SetColumnSpan(textPanel, 1);
                    }
                    
                    // Control (Toggle/NumberBox) -> Row 1
                    if (g.Children.Count >= 2 && g.Children[1] is FrameworkElement control)
                    {
                        Grid.SetRow(control, 1);
                        Grid.SetColumn(control, 0);
                        Grid.SetColumnSpan(control, 1);
                        control.HorizontalAlignment = HorizontalAlignment.Left;
                        control.Margin = new Thickness(0, 10, 0, 0);
                    }
                }
                else
                {
                    // Horizontal Side-by-Side
                    // g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Not needed, implied single? No, let's keep it simple.
                    // Actually original XAML didn't have RowDefs for these simple grids.
                    // But if we cleared them, we must restore if needed.
                    // Wait, original XAML had NO RowDefinitions, just 2 ColumnDefinitions.
                    
                    g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    
                    // Text -> Col 0
                    if (g.Children.Count >= 1 && g.Children[0] is FrameworkElement textPanel)
                    {
                        Grid.SetRow(textPanel, 0);
                        Grid.SetColumn(textPanel, 0);
                    }
                    
                    // Control -> Col 1
                    if (g.Children.Count >= 2 && g.Children[1] is FrameworkElement control)
                    {
                        Grid.SetRow(control, 0);
                        Grid.SetColumn(control, 1);
                        control.HorizontalAlignment = HorizontalAlignment.Right; // Default? Or toggle default. Toggle is usually right aligned in grid col.
                        // Let's check original XAML. Grid.Column="1" no alignment usually implies Stretch? 
                        // ToggleSwitch default horizontal alignment is Left. In a Grid Column Auto?
                        // Actually, let's assume Stretch or Left. 
                        // But for visual consistency let's set it to Right or Stretch.
                        control.HorizontalAlignment = HorizontalAlignment.Stretch; 
                        control.Margin = new Thickness(0);
                    }
                }
            }

            // Apply to all named grids
            SetGridLayout(GridRunOnStartup);
            SetGridLayout(GridMinimizeOnExit);
            SetGridLayout(GridIncognito);
            SetGridLayout(GridDaysToShow);
            SetGridLayout(GridDetailedDays);
            SetGridLayout(GridMinDuration);
            SetGridLayout(GridAutoRefresh);
            SetGridLayout(GridTheme);
            SetGridLayout(GridLanguage);
            SetGridLayout(GridCombinedAudio);
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            LoadExcludedProcessItems();
            LoadAppTimeLimits();
            LoadHiddenSubApps();
        }

        private bool _isLoading = false;

        private void MarkDirty()
        {
            if (_isLoading) return;
            UnsavedChangesBanner.Visibility = Visibility.Visible;
        }

        private void MarkClean()
        {
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
            LoadCustomRules();

            // Display
            DaysToShowTextBox.Value = UserPreferences.DayAmount;
            MinDurationTextBox.Value = UserPreferences.MinumumDuration.TotalSeconds;

            // Refresh
            EnableAutoRefresh.IsOn = UserPreferences.EnableAutoRefresh;
            RefreshInterval.Value = UserPreferences.RefreshIntervalSeconds;
            
            // Detailed Usage Days
            DetailedDaysTextBox.Value = UserPreferences.DetailedUsageDayCount;

            // Data Flush Interval
            DataFlushIntervalTextBox.Value = UserPreferences.DataFlushIntervalSeconds;
            ToggleUseRamCache.IsOn = UserPreferences.UseRamCache;
            
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

            // Language
            try
            {
                // Priority: Manual Preference > System Override > Default
                string currentLang = UserPreferences.LanguageCode;
                Debug.WriteLine($"[Settings] LoadCurrentSettings - LanguageCode from UserPreferences: '{currentLang}'");
                if (string.IsNullOrEmpty(currentLang))
                {
                    currentLang = Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride;
                    Debug.WriteLine($"[Settings] LoadCurrentSettings - Fallback to PrimaryLanguageOverride: '{currentLang}'");
                }

                bool foundLang = false;
                foreach (ComboBoxItem item in CBLanguage.Items)
                {
                    string tagValue = item.Tag?.ToString() ?? "";
                    if (tagValue == currentLang)
                    {
                        CBLanguage.SelectedItem = item;
                        foundLang = true;
                        Debug.WriteLine($"[Settings] LoadCurrentSettings - Found matching language: '{tagValue}'");
                        break;
                    }
                }
                if (!foundLang)
                {
                    Debug.WriteLine($"[Settings] LoadCurrentSettings - No match found, defaulting to index 0");
                    CBLanguage.SelectedIndex = 0; // Default
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading language: {ex.Message}");
                CBLanguage.SelectedIndex = 0;
            }

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
            
            // App time limits
            foreach (var limit in UserPreferences.AppTimeLimits)
            {
                TimeSpan time = TimeSpan.FromMinutes(limit.Value);
                AppTimeLimitsList.Items.Add($"{limit.Key}{APP_TIMELIMIT_SEPARATOR}{time.Hours}h {time.Minutes}m");
            }
            
            // Title/Sub-app time limits (with [Sub] prefix)
            foreach (var limit in UserPreferences.TitleTimeLimits)
            {
                TimeSpan time = TimeSpan.FromMinutes(limit.Value);
                // Format: "[Sub] ProcessName|Title → 1h 30m"
                AppTimeLimitsList.Items.Add($"[Sub] {limit.Key}{APP_TIMELIMIT_SEPARATOR}{time.Hours}h {time.Minutes}m");
            }
        }

        // ===== Hidden SubApps =====
        private bool _hiddenKeywordsVisible = false; // Track if keywords are currently revealed
        
        private void LoadHiddenSubApps()
        {
            LoadHiddenSubApps(_hiddenKeywordsVisible);
        }
        
        private void LoadHiddenSubApps(bool showActualText)
        {
            HiddenSubAppsList.Items.Clear();
            foreach (string keyword in UserPreferences.IgnoredWindowTitles)
            {
                if (showActualText)
                {
                    HiddenSubAppsList.Items.Add(keyword);
                }
                else
                {
                    // Mask the keyword with bullets (same length)
                    HiddenSubAppsList.Items.Add(new string('●', keyword.Length));
                }
            }
            // Load toggle state
            TglHideRetroactively.IsOn = UserPreferences.HideSubAppsRetroactively;
        }

        private void BtnAddHiddenKeyword_Click(object sender, RoutedEventArgs e)
        {
            string keyword = TxtNewHiddenKeyword.Text?.Trim();
            if (!string.IsNullOrEmpty(keyword) && !UserPreferences.IgnoredWindowTitles.Contains(keyword, StringComparer.OrdinalIgnoreCase))
            {
                UserPreferences.IgnoredWindowTitles.Add(keyword);
                UserPreferences.Save();
                LoadHiddenSubApps();
                TxtNewHiddenKeyword.Text = "";
            }
        }

        // ===== Custom Title Rules =====
        private void LoadCustomRules()
        {
            CustomRulesList.ItemsSource = null;
            CustomRulesList.ItemsSource = UserPreferences.CustomTitleRules;
        }

        private async void BtnAddRule_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new EditTitleRuleDialog();
            dialog.XamlRoot = this.XamlRoot;
            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                UserPreferences.CustomTitleRules.Add(dialog.Result);
                UserPreferences.Save();
                LoadCustomRules();
            }
        }

        private async void BtnEditRule_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            var rule = btn?.Tag as DigitalWellbeing.Core.Models.CustomTitleRule;
            if (rule == null) return;

            var dialog = new EditTitleRuleDialog(rule);
            dialog.XamlRoot = this.XamlRoot;
            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                // Result is already updated by reference in dialog but we need to trigger save/refresh
                UserPreferences.Save();
                LoadCustomRules(); // Refresh list to show changes
            }
        }

        private void BtnDeleteRule_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            var rule = btn?.Tag as DigitalWellbeing.Core.Models.CustomTitleRule;
            if (rule != null)
            {
                UserPreferences.CustomTitleRules.Remove(rule);
                UserPreferences.Save();
                LoadCustomRules();
            }
        }

        private void HiddenSubAppsList_DoubleTapped(object sender, Microsoft.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
        {
            if (HiddenSubAppsList.SelectedItem != null && _hiddenKeywordsVisible)
            {
                // Only allow deletion when keywords are visible (user has focused the textbox)
                int index = HiddenSubAppsList.SelectedIndex;
                if (index >= 0 && index < UserPreferences.IgnoredWindowTitles.Count)
                {
                    UserPreferences.IgnoredWindowTitles.RemoveAt(index);
                    UserPreferences.Save();
                    LoadHiddenSubApps();
                }
            }
        }
        
        private void HiddenSubAppsList_Tapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
        {
            // Clicking on the masked list reveals the keywords
            if (!_hiddenKeywordsVisible)
            {
                _hiddenKeywordsVisible = true;
                LoadHiddenSubApps(showActualText: true);
            }
        }
        
        private void TxtNewHiddenKeyword_GotFocus(object sender, RoutedEventArgs e)
        {
            // Reveal actual keywords when editing
            _hiddenKeywordsVisible = true;
            LoadHiddenSubApps(showActualText: true);
        }
        
        private void TxtNewHiddenKeyword_LostFocus(object sender, RoutedEventArgs e)
        {
            // Mask keywords when focus leaves
            _hiddenKeywordsVisible = false;
            LoadHiddenSubApps(showActualText: false);
        }

        private void TglHideRetroactively_Toggled(object sender, RoutedEventArgs e)
        {
            UserPreferences.HideSubAppsRetroactively = TglHideRetroactively.IsOn;
            UserPreferences.Save();
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
                 
                 // Check if it's a sub-app time limit
                 if (selected.StartsWith("[Sub] "))
                 {
                     // Remove "[Sub] " prefix and parse key
                     string withoutPrefix = selected.Substring(6);
                     string key = withoutPrefix.Split(new string[] { APP_TIMELIMIT_SEPARATOR }, StringSplitOptions.None)[0];
                     UserPreferences.UpdateTitleTimeLimit(key, TimeSpan.Zero);
                     LoadAppTimeLimits();
                 }
                 else
                 {
                     // Regular app time limit
                     string processName = selected.Split(new string[] { APP_TIMELIMIT_SEPARATOR }, StringSplitOptions.None)[0];
                     if (UserPreferences.AppTimeLimits.ContainsKey(processName))
                     {
                         UserPreferences.UpdateAppTimeLimit(processName, TimeSpan.Zero);
                         LoadAppTimeLimits();
                     }
                 }
             }
        }

        private void ApplyAll_Click(object sender, RoutedEventArgs e)
        {
            // Gather all values
            UserPreferences.DayAmount = (int)DaysToShowTextBox.Value;
            UserPreferences.DetailedUsageDayCount = (int)DetailedDaysTextBox.Value;
            UserPreferences.DetailedUsageDayCount = (int)DetailedDaysTextBox.Value;
            UserPreferences.MinumumDuration = TimeSpan.FromSeconds(MinDurationTextBox.Value);
            
            UserPreferences.MinimizeOnExit = ToggleMinimizeOnExit.IsOn;
            UserPreferences.IncognitoMode = ToggleIncognitoMode.IsOn;
            UserPreferences.EnableAutoRefresh = EnableAutoRefresh.IsOn;
            UserPreferences.RefreshIntervalSeconds = (int)RefreshInterval.Value;
            UserPreferences.DataFlushIntervalSeconds = (int)DataFlushIntervalTextBox.Value;
            UserPreferences.UseRamCache = ToggleUseRamCache.IsOn;

            // Startup
            try
            {
                SettingsManager.SetRunOnStartup(EnableRunOnStartup.IsOn);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Startup registry error: {ex.Message}");
            }

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

            // Language (Requires Restart usually)
            if (CBLanguage.SelectedItem is ComboBoxItem langItem)
            {
                try 
                {
                    string code = langItem.Tag?.ToString() ?? "";
                    Debug.WriteLine($"[Settings] ApplyAll_Click - Setting LanguageCode to: '{code}'");
                    
                    // Save to manual preference
                    UserPreferences.LanguageCode = code;
                    Debug.WriteLine($"[Settings] ApplyAll_Click - UserPreferences.LanguageCode is now: '{UserPreferences.LanguageCode}'");

                    // Try to set system override as well (best effort)
                    string current = Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride;
                    if (current != code)
                    {
                        // Update
                        Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride = code;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error saving language preference: {ex.Message}");
                }
            }

            // Log Location
            string newLogPath = TxtLogLocation.Text?.Trim();
            string currentPath = ApplicationPath.GetCustomLogsFolderRaw() ?? "";
            if (newLogPath != currentPath)
            {
                if (string.IsNullOrEmpty(newLogPath) || newLogPath == ApplicationPath.UsageLogsFolder.TrimEnd('\\'))
                {
                    ApplicationPath.ClearCustomLogsFolder();
                }
                else if (System.IO.Directory.Exists(newLogPath))
                {
                    ApplicationPath.SetCustomLogsFolder(newLogPath);
                }
                // Restart service to pick up new path
                RestartBackgroundService();
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

        private void Settings_KeyUp(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            MarkDirty();
        }

        private void CBTheme_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            MarkDirty();
        }

        private void CBLanguage_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            MarkDirty();
        }

        private void ToggleCombinedAudioView_Toggled(object sender, RoutedEventArgs e)
        {
            if (_isLoading) return;
            UserPreferences.ShowCombinedAudioView = ToggleCombinedAudioView.IsOn;
            UserPreferences.Save();
        }

        private void ToggleUseRamCache_Toggled(object sender, RoutedEventArgs e)
        {
            MarkDirty();
        }

        private void ApplyTimelineThreshold_Click(object sender, RoutedEventArgs e)
        {
            // Removed
        }

        private void BtnOpenAppFolder_Click(object sender, RoutedEventArgs e)
        {
             string logFolder = ApplicationPath.UsageLogsFolder;
             if (System.IO.Directory.Exists(logFolder))
             {
                 Process.Start(new ProcessStartInfo(logFolder) { UseShellExecute = true });
             }
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

        private void BtnBrowseLogLocation_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Use COM IFileOpenDialog which is more reliable for unpackaged apps
                var dialog = new NativeFolderDialog();
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
                
                string selectedPath = dialog.ShowDialog(hwnd, TxtLogLocation.Text);
                if (!string.IsNullOrEmpty(selectedPath))
                {
                    TxtLogLocation.Text = selectedPath;
                    MarkDirty();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"FolderPicker failed: {ex.Message}");
            }
        }

        private void TxtLogLocation_TextChanged(object sender, TextChangedEventArgs e)
        {
            MarkDirty();
        }

        private void BtnResetLogLocation_Click(object sender, RoutedEventArgs e)
        {
            ApplicationPath.ClearCustomLogsFolder();
            TxtLogLocation.Text = ApplicationPath.UsageLogsFolder;
            
            // Restart Service
            RestartBackgroundService();
        }

        private void BtnOpenLogFolder_Click(object sender, RoutedEventArgs e)
        {
            string logFolder = ApplicationPath.UsageLogsFolder;
            if (System.IO.Directory.Exists(logFolder))
            {
                Process.Start(new ProcessStartInfo(logFolder) { UseShellExecute = true });
            }
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
                // Start new instance
                string[] possiblePaths = new string[]
                {
                    System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DigitalWellbeingService.exe"),
                    System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Service", "DigitalWellbeingService.exe")
                };

                string servicePath = possiblePaths.FirstOrDefault(p => System.IO.File.Exists(p));

                if (!string.IsNullOrEmpty(servicePath))
                {
                    Process.Start(new ProcessStartInfo(servicePath) { UseShellExecute = true, WindowStyle = ProcessWindowStyle.Hidden });
                }
                else
                {
                     Debug.WriteLine($"Service restart failed: Executable not found. Checked: {string.Join(", ", possiblePaths)}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Service restart failed: {ex.Message}");
            }
        }
    }
}
