using UsageLogger.Core;
using UsageLogger.Helpers;
using UsageLogger.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Collections.ObjectModel;
using System.Linq;
using Windows.ApplicationModel;
using UsageLogger.Models;
using System.Threading.Tasks;

namespace UsageLogger.Views
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
            SetGridLayout(GridBackdrop);
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            LoadExcludedProcessItems();
            LoadAppTimeLimits();
            LoadHiddenSubApps();
        }

        private bool _isLoading = false;
        private string _originalLanguageCode = ""; // Track original language to detect changes
        
        // Original values for smart dirty detection
        private string _origStartupMode;
        private bool _origMinimizeOnExit;
        private bool _origIncognitoMode;
        private int _origDayAmount;
        private double _origMinDuration;
        private bool _origAutoRefresh;
        private int _origRefreshInterval;
        private int _origDetailedDays;
        private int _origDataFlushInterval;
        private bool _origUseRamCache;
        private int _origIdleThreshold;
        private int _origAvgWatts;
        private double _origKwhPrice;
        private string _origCurrency;
        private string _origTheme;
        private string _origLogLocation;
        private string _origBackdrop;
        private int _origDayStartHour;
        private int _origDayStartMinute;

        /// <summary>
        /// Compares current UI values against original loaded values.
        /// Shows/hides the Apply banner based on whether there are actual changes.
        /// </summary>
        private void CheckForChanges()
        {
            if (_isLoading) return;
            
            try
            {
                bool hasChanges = false;
                
                // Startup Mode
                if (StartupModeComboBox.SelectedItem is ComboBoxItem startupItem)
                    hasChanges |= (startupItem.Tag?.ToString() ?? "") != (_origStartupMode ?? "");
                
                // Toggles
                hasChanges |= ToggleMinimizeOnExit.IsOn != _origMinimizeOnExit;
                hasChanges |= ToggleIncognitoMode.IsOn != _origIncognitoMode;
                hasChanges |= EnableAutoRefresh.IsOn != _origAutoRefresh;
                hasChanges |= (BtnRamCache.IsChecked == true) != _origUseRamCache;
                
                // Number Boxes (check for NaN)
                if (!double.IsNaN(DaysToShowTextBox.Value))
                    hasChanges |= (int)DaysToShowTextBox.Value != _origDayAmount;
                if (!double.IsNaN(MinDurationTextBox.Value))
                    hasChanges |= MinDurationTextBox.Value != _origMinDuration;
                if (!double.IsNaN(RefreshInterval.Value))
                    hasChanges |= (int)RefreshInterval.Value != _origRefreshInterval;
                if (!double.IsNaN(DetailedDaysTextBox.Value))
                    hasChanges |= (int)DetailedDaysTextBox.Value != _origDetailedDays;
                if (!double.IsNaN(DataFlushIntervalTextBox.Value))
                    hasChanges |= (int)DataFlushIntervalTextBox.Value != _origDataFlushInterval;
                if (!double.IsNaN(IdleThresholdTextBox.Value))
                    hasChanges |= (int)IdleThresholdTextBox.Value != _origIdleThreshold;
                // Day Start Text
                if (ParseDayStartTime(TxtDayStartTime.Text, out int dsh, out int dsm))
                {
                    hasChanges |= dsh != _origDayStartHour;
                    hasChanges |= dsm != _origDayStartMinute;
                }
                
                // Power tracking text fields
                if (int.TryParse(TxtAvgWatts.Text, out int watts)) hasChanges |= watts != _origAvgWatts;
                if (double.TryParse(TxtKwhPrice.Text, out double price)) hasChanges |= Math.Abs(price - _origKwhPrice) > 0.001;
                hasChanges |= (TxtCurrency.Text ?? "") != (_origCurrency ?? "");
                
                // Theme
                if (CBTheme.SelectedItem is ComboBoxItem themeItem)
                    hasChanges |= (themeItem.Tag?.ToString() ?? "") != (_origTheme ?? "");
                
                // Backdrop
                if (CBBackdrop.SelectedItem is ComboBoxItem backdropItem)
                    hasChanges |= (backdropItem.Tag?.ToString() ?? "") != (_origBackdrop ?? "");
                
                // Language
                if (CBLanguage.SelectedItem is ComboBoxItem langItem)
                    hasChanges |= (langItem.Tag?.ToString() ?? "") != (_originalLanguageCode ?? "");
                
                // Log Location
                hasChanges |= (TxtLogLocation.Text ?? "") != (_origLogLocation ?? "");
                
                UnsavedChangesBanner.Visibility = hasChanges ? Visibility.Visible : Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Settings] CheckForChanges error: {ex.Message}");
            }
        }

        private void MarkClean()
        {
            UnsavedChangesBanner.Visibility = Visibility.Collapsed;
        }

        private void LoadCurrentSettings()
        {
            _isLoading = true;

            // Run on Startup (ComboBox mode)
            var currentMode = StartupManager.GetStartupMode();
            foreach (ComboBoxItem item in StartupModeComboBox.Items)
            {
                if (item.Tag?.ToString() == currentMode.ToString())
                {
                    StartupModeComboBox.SelectedItem = item;
                    break;
                }
            }

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
            MinDurationTextBox.Value = UserPreferences.MinimumDuration.TotalSeconds;

            // Load History Stats (Async)
            _ = LoadHistoryStatsAsync();

            // Refresh
            EnableAutoRefresh.IsOn = UserPreferences.EnableAutoRefresh;
            RefreshInterval.Value = UserPreferences.RefreshIntervalSeconds;
            
            // Detailed Usage Days
            DetailedDaysTextBox.Value = UserPreferences.DetailedUsageDayCount;

            // Data Flush Interval
            DataFlushIntervalTextBox.Value = UserPreferences.DataFlushIntervalSeconds;
            bool useRam = UserPreferences.UseRamCache;
            BtnRamCache.IsChecked = useRam;
            BtnDirectWrite.IsChecked = !useRam;
            FlushIntervalRow.Visibility = useRam ? Visibility.Visible : Visibility.Collapsed;
            
            // Idle Threshold (AFK Detection)
            IdleThresholdTextBox.Value = UserPreferences.IdleThresholdSeconds;
            
            // Day Start Time
            int loadedMinutes = UserPreferences.DayStartMinutes;
            int loadedH = loadedMinutes / 60;
            int loadedM = loadedMinutes % 60;
            TxtDayStartTime.Text = $"{loadedH:D2}:{loadedM:D2}";
            
            // Power Tracking
            TxtAvgWatts.Text = UserPreferences.EstimatedPowerUsageWatts.ToString();
            TxtKwhPrice.Text = UserPreferences.KwhPrice.ToString("0.##"); // Format properly
            TxtCurrency.Text = UserPreferences.CurrencySymbol;

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

            // Backdrop
            string backdrop = UserPreferences.BackdropType ?? "Mica";
            foreach (ComboBoxItem item in CBBackdrop.Items)
            {
                if (item.Tag.ToString() == backdrop)
                {
                    CBBackdrop.SelectedItem = item;
                    break;
                }
            }
            if (CBBackdrop.SelectedItem == null) CBBackdrop.SelectedIndex = 0; // Default Mica

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
                
                // Store original language for change detection
                _originalLanguageCode = currentLang ?? "";

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

            // Store original values for smart dirty detection
            _origStartupMode = StartupManager.GetStartupMode().ToString();
            _origMinimizeOnExit = UserPreferences.MinimizeOnExit;
            _origIncognitoMode = UserPreferences.IncognitoMode;
            _origDayAmount = UserPreferences.DayAmount;
            _origMinDuration = UserPreferences.MinimumDuration.TotalSeconds;
            _origAutoRefresh = UserPreferences.EnableAutoRefresh;
            _origRefreshInterval = UserPreferences.RefreshIntervalSeconds;
            _origDetailedDays = UserPreferences.DetailedUsageDayCount;
            _origDataFlushInterval = UserPreferences.DataFlushIntervalSeconds;
            _origUseRamCache = UserPreferences.UseRamCache;
            _origIdleThreshold = UserPreferences.IdleThresholdSeconds;
            _origAvgWatts = UserPreferences.EstimatedPowerUsageWatts;
            _origKwhPrice = UserPreferences.KwhPrice;
            _origCurrency = UserPreferences.CurrencySymbol ?? "";
            _origTheme = UserPreferences.ThemeMode ?? "";
            _origBackdrop = UserPreferences.BackdropType ?? "Mica";
            _origLogLocation = TxtLogLocation.Text ?? "";
            _origDayStartHour = UserPreferences.DayStartMinutes / 60;
            _origDayStartMinute = UserPreferences.DayStartMinutes % 60;

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
            SubAppRulesHelper.LoadHiddenSubApps(HiddenSubAppsList, TglHideRetroactively, _hiddenKeywordsVisible);
        }
        
        private void LoadHiddenSubApps(bool showActualText)
        {
            SubAppRulesHelper.LoadHiddenSubApps(HiddenSubAppsList, TglHideRetroactively, showActualText);
        }

        private void BtnAddHiddenKeyword_Click(object sender, RoutedEventArgs e)
        {
            SubAppRulesHelper.AddHiddenKeyword(TxtNewHiddenKeyword, LoadHiddenSubApps);
        }

        // ===== Custom Title Rules =====
        private void LoadCustomRules()
        {
            SubAppRulesHelper.LoadCustomRules(CustomRulesList);
        }

        private async void BtnAddRule_Click(object sender, RoutedEventArgs e)
        {
            SubAppRulesHelper.AddRule(this.XamlRoot, LoadCustomRules);
        }

        private async void BtnEditRule_Click(object sender, RoutedEventArgs e)
        {
            SubAppRulesHelper.EditRule(sender, this.XamlRoot, LoadCustomRules);
        }

        private void BtnDeleteRule_Click(object sender, RoutedEventArgs e)
        {
            SubAppRulesHelper.DeleteRule(sender, LoadCustomRules);
        }

        private void HiddenSubAppsList_DoubleTapped(object sender, Microsoft.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
        {
            SubAppRulesHelper.RemoveHiddenKeyword(HiddenSubAppsList, _hiddenKeywordsVisible, LoadHiddenSubApps);
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

        private void StartupModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
             if (!_isLoading) CheckForChanges();
        }

        private void ToggleMinimizeOnExit_Toggled(object sender, RoutedEventArgs e)
        {
             if (_isLoading) return;
             CheckForChanges();
        }

        private void ToggleIncognitoMode_Toggled(object sender, RoutedEventArgs e)
        {
            if (_isLoading) return;
            CheckForChanges();
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
            UserPreferences.MinimumDuration = TimeSpan.FromSeconds(MinDurationTextBox.Value);
            
            UserPreferences.MinimizeOnExit = ToggleMinimizeOnExit.IsOn;
            UserPreferences.IncognitoMode = ToggleIncognitoMode.IsOn;
            UserPreferences.EnableAutoRefresh = EnableAutoRefresh.IsOn;
            UserPreferences.RefreshIntervalSeconds = (int)RefreshInterval.Value;
            
            UserPreferences.DataFlushIntervalSeconds = (int)DataFlushIntervalTextBox.Value;
            UserPreferences.UseRamCache = BtnRamCache.IsChecked == true;
            UserPreferences.IdleThresholdSeconds = (int)IdleThresholdTextBox.Value;

            // Day Start Time - parse from text box
            int parsedH = 0, parsedM = 0;
            ParseDayStartTime(TxtDayStartTime.Text, out parsedH, out parsedM);
            UserPreferences.DayStartMinutes = (parsedH * 60) + parsedM;
            UsageLogger.Core.Helpers.DateHelper.DayStartMinutes = UserPreferences.DayStartMinutes;

            // Power Tracking
            if (int.TryParse(TxtAvgWatts.Text, out int watts)) UserPreferences.EstimatedPowerUsageWatts = watts;
            if (double.TryParse(TxtKwhPrice.Text, out double price)) UserPreferences.KwhPrice = price;
            if (!string.IsNullOrWhiteSpace(TxtCurrency.Text)) UserPreferences.CurrencySymbol = TxtCurrency.Text.Trim();

            // Startup
            try
            {
                if (StartupModeComboBox.SelectedItem is ComboBoxItem selectedItem)
                {
                    string modeTag = selectedItem.Tag?.ToString() ?? "None";
                    if (Enum.TryParse<StartupManager.StartupMode>(modeTag, out var mode))
                    {
                        StartupManager.SetStartupMode(mode);
                    }
                }
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

            // Backdrop
            if (CBBackdrop.SelectedItem is ComboBoxItem backdropItem)
            {
                string backdrop = backdropItem.Tag.ToString();
                UserPreferences.BackdropType = backdrop;
                
                if (App.Current is App myBackdropApp && myBackdropApp.m_window is MainWindow backdropWindow)
                {
                    backdropWindow.ApplyBackdrop();
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

            // Check if language changed - requires restart
            if (CBLanguage.SelectedItem is ComboBoxItem selectedLangItem)
            {
                string newLang = selectedLangItem.Tag?.ToString() ?? "";
                if (newLang != _originalLanguageCode)
                {
                    Debug.WriteLine($"[Settings] Language changed from '{_originalLanguageCode}' to '{newLang}' - Restarting app");
                    RestartApplication();
                    return; // Don't navigate, we're restarting
                }
            }

            // Refresh UX by navigating to Dashboard
            if (App.Current is App myApp2 && myApp2.m_window is MainWindow window2)
            {
                window2.NavigateToDashboard();
            }
        }

        /// <summary>
        /// Restarts the application to apply language changes.
        /// </summary>
        private void RestartApplication()
        {
            try
            {
                string exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(exePath)
                {
                    UseShellExecute = true
                });
                Application.Current.Exit();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Settings] Failed to restart: {ex.Message}");
            }
        }

        private void Settings_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            if (_isLoading) return;
            CheckForChanges();
        }

        private void EnableAutoRefresh_Toggled(object sender, RoutedEventArgs e)
        {
            if (_isLoading) return;
            CheckForChanges();
        }

        private void RefreshInterval_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            if (_isLoading) return;
            CheckForChanges();
        }

        private void Settings_KeyUp(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            if (_isLoading) return;
            CheckForChanges();
        }

        private void CBTheme_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoading) return;
            CheckForChanges();
        }

        private void CBLanguage_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoading) return;
            CheckForChanges();
        }

        private void CBBackdrop_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoading) return;
            CheckForChanges();
        }

        private void ToggleCombinedAudioView_Toggled(object sender, RoutedEventArgs e)
        {
            if (_isLoading) return;
            UserPreferences.ShowCombinedAudioView = ToggleCombinedAudioView.IsOn;
            UserPreferences.Save();
        }

        // ===== RAM Cache segmented selector =====
        private void BtnRamCache_Checked(object sender, RoutedEventArgs e)
        {
            if (_isLoading || BtnDirectWrite == null || FlushIntervalRow == null) return;
            BtnDirectWrite.IsChecked = false;
            FlushIntervalRow.Visibility = Visibility.Visible;
            CheckForChanges();
        }
        private void BtnRamCache_Unchecked(object sender, RoutedEventArgs e)
        {
            if (_isLoading || BtnDirectWrite == null || FlushIntervalRow == null) return;
            if (BtnDirectWrite.IsChecked != true) { BtnRamCache.IsChecked = true; return; }
            FlushIntervalRow.Visibility = Visibility.Collapsed;
        }
        private void BtnDirectWrite_Checked(object sender, RoutedEventArgs e)
        {
            if (_isLoading || BtnRamCache == null || FlushIntervalRow == null) return;
            BtnRamCache.IsChecked = false;
            FlushIntervalRow.Visibility = Visibility.Collapsed;
            CheckForChanges();
        }
        private void BtnDirectWrite_Unchecked(object sender, RoutedEventArgs e)
        {
            if (_isLoading || BtnRamCache == null || FlushIntervalRow == null) return;
            if (BtnRamCache.IsChecked != true) { BtnDirectWrite.IsChecked = true; return; }
            FlushIntervalRow.Visibility = Visibility.Visible;
        }

        // ===== Day Start Time text parser =====
        private static bool ParseDayStartTime(string input, out int hour, out int minute)
        {
            hour = 0; minute = 0;
            if (string.IsNullOrWhiteSpace(input)) return false;

            input = input.Trim().ToLowerInvariant();
            bool isPm = input.Contains("pm");
            bool isAm = input.Contains("am");
            input = input.Replace("pm", "").Replace("am", "").Trim();

            // Accept H:MM or HH:MM
            var parts = input.Split(':');
            if (parts.Length != 2) return false;
            if (!int.TryParse(parts[0].Trim(), out hour)) return false;
            if (!int.TryParse(parts[1].Trim(), out minute)) return false;

            // 12-hour conversion
            if (isPm && hour < 12) hour += 12;
            if (isAm && hour == 12) hour = 0;

            hour   = Math.Clamp(hour,   0, 23);
            minute = Math.Clamp(minute, 0, 59);
            return true;
        }

        private void TxtDayStartTime_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_isLoading) return;
            if (ParseDayStartTime(TxtDayStartTime.Text, out int h, out int m))
                TxtDayStartTime.Text = $"{h:D2}:{m:D2}";
            CheckForChanges();
        }

        private void TxtDayStartTime_KeyUp(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            if (_isLoading) return;
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                if (ParseDayStartTime(TxtDayStartTime.Text, out int h, out int m))
                    TxtDayStartTime.Text = $"{h:D2}:{m:D2}";
            }
            CheckForChanges();
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
            TagSettingsHelper.LoadTags(Tags);
        }

        private void BtnAddTag_Click(object sender, RoutedEventArgs e)
        {
            TagSettingsHelper.AddTag(Tags);
        }

        private void BtnDeleteTag_Click(object sender, RoutedEventArgs e)
        {
            TagSettingsHelper.DeleteTag(Tags, sender);
        }

        private void TagName_LostFocus(object sender, RoutedEventArgs e)
        {
            TagSettingsHelper.SaveTags(Tags);
        }

        private void ColorPicker_ColorChanged(ColorPicker sender, ColorChangedEventArgs args)
        {
            TagSettingsHelper.OnColorChanged(Tags, sender, args);
        }
        
        // Alternative: Use Flyout Closed event to save color.
        private void ColorPickerFlyout_Closed(object sender, object e)
        {
            TagSettingsHelper.OnColorPickerFlyoutClosed(Tags);
        }

        private void SaveTags()
        {
            TagSettingsHelper.SaveTags(Tags);
        }

        public Microsoft.UI.Xaml.Media.SolidColorBrush GetBrush(string hex)
        {
            return TagSettingsHelper.GetBrush(hex);
        }

        // ===== Log Location =====
        private void LoadLogLocation()
        {
            LogLocationHelper.LoadLogLocation(TxtLogLocation);
        }

        private void BtnBrowseLogLocation_Click(object sender, RoutedEventArgs e)
        {
            LogLocationHelper.BrowseLogLocation(TxtLogLocation, App.MainWindow, CheckForChanges);
        }

        private void TxtLogLocation_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isLoading) return;
            CheckForChanges();
        }

        private void BtnResetLogLocation_Click(object sender, RoutedEventArgs e)
        {
            LogLocationHelper.ResetLogLocation(TxtLogLocation, RestartBackgroundService);
        }

        private void BtnOpenLogFolder_Click(object sender, RoutedEventArgs e)
        {
            LogLocationHelper.OpenLogFolder();
        }

        private void RestartBackgroundService()
        {
            try
            {
                // Find and kill existing service process
                var processes = Process.GetProcessesByName("UsageLoggerService");
                foreach (var proc in processes)
                {
                    proc.Kill();
                    proc.WaitForExit(3000);
                }

                // Start new instance
                // Start new instance
                string[] possiblePaths = new string[]
                {
                    System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "UsageLoggerService.exe"),
                    System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Service", "UsageLoggerService.exe")
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
        private async Task LoadHistoryStatsAsync()
        {
            try
            {
                var repo = new UsageLogger.Core.Data.AppSessionRepository(UsageLogger.Core.ApplicationPath.UsageLogsFolder);
                int totalDays = await repo.GetTotalDaysCountAsync();

                this.DispatcherQueue.TryEnqueue(() =>
                {
                    DaysToShowTextBox.Maximum = totalDays;
                    DaysAvailableHint.Text = $"(Max: {totalDays})";
                    DaysAvailableHint.Visibility = Visibility.Visible;

                    if (DaysToShowTextBox.Value > totalDays)
                    {
                        DaysToShowTextBox.Value = totalDays;
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Settings] LoadHistoryStats Error: {ex.Message}");
            }
        }

        private void TxtAvgWatts_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isLoading) return;
            CheckForChanges();
        }

        private void TxtKwhPrice_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isLoading) return;
            CheckForChanges();
        }

        private void TxtCurrency_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isLoading) return;
            CheckForChanges();
        }
    }
}
