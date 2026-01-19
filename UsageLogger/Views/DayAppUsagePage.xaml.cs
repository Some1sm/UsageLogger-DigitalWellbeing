using UsageLogger.ViewModels;
using UsageLogger.Models;
using UsageLogger.Helpers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading.Tasks;
using System.Linq; // Added for FirstOrDefault

namespace UsageLogger.Views
{
    public sealed partial class DayAppUsagePage : Page
    {
        public AppUsageViewModel ViewModel { get; }
        


        public DayAppUsagePage()
        {
            // Initialize ViewModel BEFORE InitializeComponent to ensure x:Bind targets are valid.
            try
            {
                ViewModel = new AppUsageViewModel();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ViewModel Init Error: {ex}");
            }
            
            this.InitializeComponent();
            
            this.DataContext = ViewModel;
            this.Loaded += DayAppUsagePage_Loaded;
        }

        private void DayAppUsagePage_Loaded(object sender, RoutedEventArgs e)
        {
            if (ViewModel == null) return;
            
            // Set XamlRoot for dialogs
            ViewModel.XamlRoot = this.XamlRoot;
            
            // Initialize view mode from saved preference
            UpdateViewMode(UserPreferences.ShowCombinedAudioView);

            // 1. Chart Injection Removed (Handled in XAML)
            
            // 2. Chart Injection Removed (Handled in XAML)

            // 3. Ensure Data is Loaded
            try
            {
                if (!ViewModel.IsWeeklyDataLoaded)
                {
                    ViewModel.LoadWeeklyData();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CRITICAL] Data Load Failed: {ex}");
            }
        }



        private void BtnPrev_Click(object sender, RoutedEventArgs e)
        {
            ChangeDay(-1);
        }

        private void BtnNext_Click(object sender, RoutedEventArgs e)
        {
            ChangeDay(1);
        }

        private void CalendarPicker_SelectedDatesChanged(CalendarView sender, CalendarViewSelectedDatesChangedEventArgs args)
        {
            if (args.AddedDates.Count > 0)
            {
                var date = args.AddedDates[0];
                var newDate = date.DateTime;
                
                if (newDate > System.DateTime.Now)
                {
                    // If future, reset to Today
                    sender.SelectedDates.Clear();
                    newDate = System.DateTime.Now.Date;
                }

                // Rolling Window: Always reload chart centered on the selected date
                ViewModel.LoadWeeklyData(newDate);
                
                // Hide flyout
                CalendarFlyout.Hide();
            }
        }

        private void ChangeDay(int offset)
        {
            if (ViewModel == null) return;
            
            DateTime newDate = ViewModel.LoadedDate.AddDays(offset);
            
            // Prevent navigating to future dates
            if (newDate > DateTime.Now.Date) return;
            
            // Rolling Window: Always reload the chart centered on the new date
            ViewModel.LoadWeeklyData(newDate);
        }

        private void Grid_RightTapped(object sender, Microsoft.UI.Xaml.Input.RightTappedRoutedEventArgs e)
        {
            // Optional: Select the item on right click
        }
        
        // --- Context Menu Handlers (Placeholders or Real) ---
        // For Phase 2, I'll keep them simple or assume they work if Logic Phase passed.
        // I will include the logic just in case user tries to right click.
        
        private void MenuFlyout_Opening(object sender, object e)
        {
            if (sender is MenuFlyout flyout && flyout.Target is FrameworkElement target && target.DataContext is UsageLogger.Models.AppUsageListItem appItem)
            {
                var categoryItem = flyout.Items.OfType<MenuFlyoutSubItem>().FirstOrDefault(i => (i.Tag as string) == "SetCategory_SubItem");
                if (categoryItem != null)
                {
                    categoryItem.Items.Clear();
                    foreach (var tag in Helpers.UserPreferences.CustomTags)
                    {
                        var menuItem = new MenuFlyoutItem { Text = tag.Name, Tag = appItem.ProcessName };
                        menuItem.Click += MenuFlyoutItem_SetAppTag_Click;
                        
                        // Add color circle icon using FontIcon? 
                        // Simplified: Just use text for now to fix the core issue.
                        
                        categoryItem.Items.Add(menuItem);
                    }
                }
            }
        }

        private async void MenuFlyoutItem_SetTimeLimit_Click(object sender, RoutedEventArgs e)
        {
            var item = sender as MenuFlyoutItem;
            string processName = item.Tag as string;
            if (string.IsNullOrEmpty(processName)) return;

            // Simple Content Dialog for Time Limit
            var inputTextBox = new TextBox 
            { 
                PlaceholderText = LocalizationHelper.GetString("Dialog_SetTimeLimit_Placeholder"), 
                InputScope = new Microsoft.UI.Xaml.Input.InputScope { Names = { new Microsoft.UI.Xaml.Input.InputScopeName(Microsoft.UI.Xaml.Input.InputScopeNameValue.Number) } } 
            };
            
            // Should check if existing limit exists? For now, just overwrite
            if (UsageLogger.Helpers.UserPreferences.AppTimeLimits.ContainsKey(processName))
            {
               inputTextBox.Text = UsageLogger.Helpers.UserPreferences.AppTimeLimits[processName].ToString();
            }

            var dialog = new ContentDialog
            {
                Title = string.Format(LocalizationHelper.GetString("Dialog_SetTimeLimit_Title"), processName),
                Content = inputTextBox,
                PrimaryButtonText = LocalizationHelper.GetString("Dialog_Save"),
                SecondaryButtonText = LocalizationHelper.GetString("Dialog_Cancel"),
                XamlRoot = this.XamlRoot
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                if (int.TryParse(inputTextBox.Text, out int minutes) && minutes > 0)
                {
                    UsageLogger.Helpers.UserPreferences.UpdateAppTimeLimit(processName, TimeSpan.FromMinutes(minutes));
                }
                else
                {
                    // Remove limit if empty or 0
                    UsageLogger.Helpers.UserPreferences.UpdateAppTimeLimit(processName, TimeSpan.Zero);
                }
                
                // Refresh
                ViewModel.LoadWeeklyData();
            }
        }

        private void MenuFlyoutItem_SetAppTag_Click(object sender, RoutedEventArgs e)
        {
            var item = sender as MenuFlyoutItem;
            string processName = item.Tag as string;
            
            // Extract Tag from Menu Item Text (Productivity, Game, etc.)
            string tagStr = item.Text; 
            
            if (string.IsNullOrEmpty(processName) || string.IsNullOrEmpty(tagStr)) return;

            // Find Tag by Name
            var customTag = Helpers.UserPreferences.CustomTags.FirstOrDefault(t => t.Name == tagStr);
            if (customTag != null)
            {
                var newTag = (UsageLogger.Core.Models.AppTag)customTag.Id;
                UsageLogger.Helpers.AppTagHelper.UpdateAppTag(processName, newTag);
                
                // Update the item in-place instead of full reload
                UpdateItemTag(processName, newTag);
            }
        }
        
        private void UpdateItemTag(string processName, UsageLogger.Core.Models.AppTag newTag)
        {
            // Find in DayListItems
            var dayItem = ViewModel.DayListItems.FirstOrDefault(x => x.ProcessName == processName);
            if (dayItem != null) dayItem._AppTag = newTag;
            
            // Find in Column1Items
            var col1Item = ViewModel.Column1Items.FirstOrDefault(x => x.ProcessName == processName);
            if (col1Item != null) col1Item._AppTag = newTag;
            
            // Find in Column2Items
            var col2Item = ViewModel.Column2Items.FirstOrDefault(x => x.ProcessName == processName);
            if (col2Item != null) col2Item._AppTag = newTag;
            
            // Find in Column3Items
            var col3Item = ViewModel.Column3Items.FirstOrDefault(x => x.ProcessName == processName);
            if (col3Item != null) col3Item._AppTag = newTag;
        }

        private async void MenuFlyoutItem_ExcludeApp_Click(object sender, RoutedEventArgs e)
        {
            var item = sender as MenuFlyoutItem;
            string processName = item.Tag as string;
            if (string.IsNullOrEmpty(processName)) return;
            
            var dialog = new ContentDialog
            {
                Title = "Exclude App?",
                Content = $"Are you sure you want to hide '{processName}' from the dashboard? You can manage excluded apps in Settings.",
                PrimaryButtonText = "Exclude",
                SecondaryButtonText = "Cancel",
                XamlRoot = this.XamlRoot
            };
            
            var result = await dialog.ShowAsync();
            
            if (result == ContentDialogResult.Primary)
            {
                // Add to Excluded List
                if (!UsageLogger.Helpers.UserPreferences.UserExcludedProcesses.Contains(processName))
                {
                    UsageLogger.Helpers.UserPreferences.UserExcludedProcesses.Add(processName);
                    UsageLogger.Helpers.UserPreferences.Save();
                    
                    // Force a full refresh which applies exclusion
                    ViewModel.LoadUserExcludedProcesses(); // Reload global exclusion list into ViewModel static
                    ViewModel.LoadWeeklyData();
                }
            }
        }

        private async void MenuFlyoutItem_SetDisplayName_Click(object sender, RoutedEventArgs e)
        {
            var item = sender as MenuFlyoutItem;
            string processName = item.Tag as string;
            if (string.IsNullOrEmpty(processName)) return;

            // Get current display name if one exists
            string currentDisplayName = UsageLogger.Helpers.UserPreferences.GetDisplayName(processName);
            bool hasCustomName = currentDisplayName != processName;

            var inputTextBox = new TextBox 
            { 
                PlaceholderText = string.Format(LocalizationHelper.GetString("Dialog_SetDisplayName_Placeholder"), processName), 
                Text = hasCustomName ? currentDisplayName : ""
            };
            
            var dialog = new ContentDialog
            {
                Title = string.Format(LocalizationHelper.GetString("Dialog_SetDisplayName_Title"), processName),
                Content = new StackPanel
                {
                    Children =
                    {
                        new TextBlock 
                        { 
                            Text = LocalizationHelper.GetString("Dialog_SetDisplayName_Desc"),
                            TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap,
                            Margin = new Microsoft.UI.Xaml.Thickness(0, 0, 0, 12)
                        },
                        inputTextBox
                    }
                },
                PrimaryButtonText = LocalizationHelper.GetString("Dialog_Save"),
                SecondaryButtonText = hasCustomName ? LocalizationHelper.GetString("Dialog_Remove") : LocalizationHelper.GetString("Dialog_Cancel"),
                CloseButtonText = LocalizationHelper.GetString("Dialog_Cancel"),
                XamlRoot = this.XamlRoot
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                // Save the new display name
                string newDisplayName = inputTextBox.Text.Trim();
                if (!string.IsNullOrWhiteSpace(newDisplayName) && newDisplayName != processName)
                {
                    UsageLogger.Helpers.UserPreferences.SetDisplayName(processName, newDisplayName);
                }
                else
                {
                    // If empty or same as process name, remove the custom display name
                    UsageLogger.Helpers.UserPreferences.RemoveDisplayName(processName);
                }
                
                // Refresh the view
                ViewModel.RefreshDayView();
            }
            else if (result == ContentDialogResult.Secondary && hasCustomName)
            {
                // Remove the display name
                UsageLogger.Helpers.UserPreferences.RemoveDisplayName(processName);
                ViewModel.RefreshDayView();
            }
        }

        private async void MenuFlyoutItem_SetCustomIcon_Click(object sender, RoutedEventArgs e)
        {
            var item = sender as MenuFlyoutItem;
            string processName = item.Tag as string;
            if (string.IsNullOrEmpty(processName)) return;

            // Check if already has a custom icon
            string currentCustomIconPath = UsageLogger.Helpers.UserPreferences.GetCustomIconPath(processName);
            bool hasCustomIcon = !string.IsNullOrEmpty(currentCustomIconPath);

            // Create file picker
            var picker = new Windows.Storage.Pickers.FileOpenPicker();
            
            // Get the window handle for the picker
            var window = (Application.Current as App)?.m_window;
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

            // Configure picker
            picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.PicturesLibrary;
            picker.FileTypeFilter.Add(".png");
            picker.FileTypeFilter.Add(".jpg");
            picker.FileTypeFilter.Add(".jpeg");
            picker.FileTypeFilter.Add(".ico");
            picker.FileTypeFilter.Add(".bmp");

            // Show dialog with option to remove if custom icon exists
            if (hasCustomIcon)
            {
                var confirmDialog = new ContentDialog
                {
                    Title = $"Custom Icon for {processName}",
                    Content = "This process already has a custom icon. What would you like to do?",
                    PrimaryButtonText = "Change Icon",
                    SecondaryButtonText = "Remove Custom Icon",
                    CloseButtonText = "Cancel",
                    XamlRoot = this.XamlRoot
                };

                var result = await confirmDialog.ShowAsync();
                
                if (result == ContentDialogResult.Secondary)
                {
                    // Remove custom icon
                    UsageLogger.Helpers.IconManager.DeleteCustomIcon(processName);
                    UsageLogger.Helpers.UserPreferences.RemoveCustomIconPath(processName);
                    ViewModel.RefreshDayView();
                    return;
                }
                else if (result != ContentDialogResult.Primary)
                {
                    return; // Cancelled
                }
            }

            // Pick file
            var file = await picker.PickSingleFileAsync();
            if (file != null)
            {
                try
                {
                    // Copy icon to custom icons folder
                    string newIconPath = UsageLogger.Helpers.IconManager.CopyCustomIcon(file.Path, processName);
                    
                    if (!string.IsNullOrEmpty(newIconPath))
                    {
                        // Save to preferences
                        UsageLogger.Helpers.UserPreferences.SetCustomIconPath(processName, newIconPath);
                        
                        // Refresh UI
                        ViewModel.RefreshDayView();
                    }
                    else
                    {
                        // Show error
                        var errorDialog = new ContentDialog
                        {
                            Title = "Error",
                            Content = "Failed to copy icon file. Please try a different image.",
                            CloseButtonText = "OK",
                            XamlRoot = this.XamlRoot
                        };
                        await errorDialog.ShowAsync();
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Custom icon upload error: {ex}");
                    var errorDialog = new ContentDialog
                    {
                        Title = "Error",
                        Content = $"Failed to set custom icon: {ex.Message}",
                        CloseButtonText = "OK",
                        XamlRoot = this.XamlRoot
                    };
                    await errorDialog.ShowAsync();
                }
            }
        }
        // Field to store the current sub-item for context menu operations
        private AppUsageSubItem _currentSubItem;

        private void SubItemMenuFlyout_Opening(object sender, object e)
        {
            if (sender is MenuFlyout flyout && flyout.Target is FrameworkElement target && target.DataContext is AppUsageSubItem subItem)
            {
                _currentSubItem = subItem;
                
                // Write to file for debugging
                try
                {
                    string logPath = UsageLogger.Core.ApplicationPath.TitleTagDebugFile;
                    System.IO.File.AppendAllText(logPath, $"{DateTime.Now}: SubItem menu opened - ParentProcess={subItem.ParentProcessName}, Title={subItem.Title}\n");
                }
                catch { }
            }
        }

        private async void SubItemMenuFlyoutItem_SetTag_Click(object sender, RoutedEventArgs e)
        {
            if (_currentSubItem != null)
            {
                await OpenTitleTagDialog(_currentSubItem);
            }
        }

        private async void SubItemMenuFlyoutItem_SetDisplayName_Click(object sender, RoutedEventArgs e)
        {
            if (_currentSubItem == null) return;
            
            string title = _currentSubItem.Title;
            string processName = _currentSubItem.ParentProcessName;
            
            // Check if a rule already exists for this exact title
            // We assume a rule created by this UI matches: Regex Escaped Title
            string matchPattern = $"^{System.Text.RegularExpressions.Regex.Escape(title)}$";
            
            var existingRule = UsageLogger.Helpers.UserPreferences.CustomTitleRules?
                .FirstOrDefault(r => r.ProcessName == processName && r.MatchPattern == matchPattern);

            string currentDisplayName = existingRule != null ? existingRule.Replacement : title;
            bool hasCustomRule = existingRule != null;
            
            var inputTextBox = new TextBox 
            { 
                PlaceholderText = $"Enter display name for '{title}'", 
                Text = hasCustomRule ? currentDisplayName : ""
            };
            
            var dialog = new ContentDialog
            {
                Title = "Set Display Name for Sub-App",
                Content = new StackPanel
                {
                    Children =
                    {
                        new TextBlock 
                        { 
                            Text = $"Original: {title}",
                            TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap,
                            Margin = new Microsoft.UI.Xaml.Thickness(0, 0, 0, 12),
                            Opacity = 0.7
                        },
                        inputTextBox,
                        
                        // Hint about advanced rules
                        new TextBlock
                        {
                            Text = "This will create a custom title rule. For more advanced matching (like 'Contains' or 'Regex'), use Settings > Custom Title Rules.",
                            FontSize = 11,
                            Opacity = 0.5,
                            Margin = new Microsoft.UI.Xaml.Thickness(0, 10, 0, 0),
                            TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap
                        }
                    }
                },
                PrimaryButtonText = "Save",
                SecondaryButtonText = hasCustomRule ? "Remove Rule" : "Cancel",
                CloseButtonText = "Cancel",
                XamlRoot = this.XamlRoot
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                string newDisplayName = inputTextBox.Text.Trim();
                if (!string.IsNullOrWhiteSpace(newDisplayName) && newDisplayName != title)
                {
                    // Create (or Update) Rule
                    // Remove existing if any to avoid duplicates logic
                    if (existingRule != null) 
                    {
                        UserPreferences.RemoveCustomTitleRule(processName, matchPattern);
                    }

                    var newRule = new UsageLogger.Core.Models.CustomTitleRule
                    {
                        ProcessName = processName,
                        MatchPattern = matchPattern,
                        Replacement = newDisplayName,
                        IsRegex = true
                    };
                    
                    UserPreferences.AddCustomTitleRule(newRule);
                }
                else
                {
                    // If empty, remove rule
                    UserPreferences.RemoveCustomTitleRule(processName, matchPattern);
                }
                ViewModel.RefreshDayView();
            }
            else if (result == ContentDialogResult.Secondary && hasCustomRule)
            {
                UserPreferences.RemoveCustomTitleRule(processName, matchPattern);
                ViewModel.RefreshDayView();
            }
        }

        private async void SubItemMenuFlyoutItem_SetTimeLimit_Click(object sender, RoutedEventArgs e)
        {
            if (_currentSubItem == null) return;
            
            string title = _currentSubItem.Title;
            string key = $"{_currentSubItem.ParentProcessName}|{title}";
            
            var inputTextBox = new TextBox 
            { 
                PlaceholderText = "Minutes (e.g., 60)", 
                InputScope = new Microsoft.UI.Xaml.Input.InputScope { Names = { new Microsoft.UI.Xaml.Input.InputScopeName(Microsoft.UI.Xaml.Input.InputScopeNameValue.Number) } } 
            };
            
            // Check if existing limit exists
            if (UserPreferences.TitleTimeLimits.ContainsKey(key))
            {
               inputTextBox.Text = UserPreferences.TitleTimeLimits[key].ToString();
            }

            var dialog = new ContentDialog
            {
                Title = $"Set Time Limit for '{title}'",
                Content = inputTextBox,
                PrimaryButtonText = "Save",
                SecondaryButtonText = "Cancel",
                XamlRoot = this.XamlRoot
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                if (int.TryParse(inputTextBox.Text, out int minutes) && minutes > 0)
                {
                    UserPreferences.UpdateTitleTimeLimit(key, TimeSpan.FromMinutes(minutes));
                }
                else
                {
                    UserPreferences.UpdateTitleTimeLimit(key, TimeSpan.Zero);
                }
            }
        }

        private async void SubItemMenuFlyoutItem_Exclude_Click(object sender, RoutedEventArgs e)
        {
            if (_currentSubItem == null) return;
            
            string title = _currentSubItem.Title;
            string key = $"{_currentSubItem.ParentProcessName}|{title}";
            
            var dialog = new ContentDialog
            {
                Title = "Exclude Sub-App?",
                Content = $"Are you sure you want to hide '{title}' from the dashboard? You can manage excluded items in Settings.",
                PrimaryButtonText = "Exclude",
                SecondaryButtonText = "Cancel",
                XamlRoot = this.XamlRoot
            };
            
            var result = await dialog.ShowAsync();
            
            if (result == ContentDialogResult.Primary)
            {
                if (!UserPreferences.ExcludedTitles.Contains(key))
                {
                    UserPreferences.ExcludedTitles.Add(key);
                    UserPreferences.Save();
                    ViewModel.RefreshDayView();
                }
            }
        }

        // Keep old handler for backwards compatibility but it shouldn't be used anymore
        public void SubItem_RightTapped(object sender, Microsoft.UI.Xaml.Input.RightTappedRoutedEventArgs e)
        {
            // This should no longer be called since we're using ContextFlyout instead
            e.Handled = true;
        }

        private async Task OpenTitleTagDialog(AppUsageSubItem item)
        {
            StackPanel content = new StackPanel { Spacing = 10 };
            
            TextBox keywordBox = new TextBox 
            { 
                Header = "Window Title Keyword", 
                Text = item.Title, 
                Description = "If the window title contains this text, it will be tagged specially." 
            };
            
            ComboBox tagCombo = new ComboBox 
            { 
                Header = "Select Category", 
                HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Stretch,
                DisplayMemberPath = "Key",
                SelectedValuePath = "Value",
            };

            var choices = UsageLogger.Helpers.AppTagHelper.GetComboBoxChoices();
            tagCombo.ItemsSource = choices;
            tagCombo.SelectedValue = 0;

            content.Children.Add(keywordBox);
            content.Children.Add(tagCombo);

            ContentDialog dialog = new ContentDialog
            {
                Title = "Tag Window Title",
                Content = content,
                PrimaryButtonText = "Save",
                CloseButtonText = "Cancel",
                XamlRoot = this.XamlRoot
            };

            var result = await dialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                string keyword = keywordBox.Text.Trim();
                if (string.IsNullOrEmpty(keyword)) return;
                
                if (tagCombo.SelectedValue is int tagId)
                {
                    // DEBUG: Show what we're about to save
                    var debugMsg = $"Saving Title Tag:\n\nParentProcess: {item.ParentProcessName}\nKeyword: {keyword}\nTagId: {tagId}";
                    System.Diagnostics.Debug.WriteLine($"[TitleTag] {debugMsg}");
                    
                    // Write to file for debugging
                    try
                    {
                        string logPath = UsageLogger.Core.ApplicationPath.TitleTagDebugFile;
                        System.IO.File.AppendAllText(logPath, $"{DateTime.Now}: {debugMsg.Replace("\n", " | ")}\n");
                    }
                    catch { }
                    
                    // Actually save as title tag (NOT app tag)
                    UsageLogger.Helpers.AppTagHelper.UpdateTitleTag(item.ParentProcessName, keyword, tagId);
                    
                    // Update the sub-item's brushes directly for immediate UI feedback
                    if (tagId != 0)
                    {
                        var newTag = (UsageLogger.Core.Models.AppTag)tagId;
                        var brush = UsageLogger.Helpers.AppTagHelper.GetTagColor(newTag) as Microsoft.UI.Xaml.Media.SolidColorBrush;
                        if (brush != null)
                        {
                            item.TagIndicatorBrush = brush;
                            item.TagTextBrush = brush;
                            var bgColor = brush.Color;
                            bgColor.A = 128;
                            item.BackgroundBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(bgColor);
                        }
                    }
                    else
                    {
                        item.TagIndicatorBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);
                        item.TagTextBrush = null;
                        item.BackgroundBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);
                    }
                    
                    // Trigger property change notifications on the parent
                    ViewModel.RefreshDayView();
                }
            }
        }

        public Microsoft.UI.Xaml.Media.Brush GetTrendBrush(bool isGood)
        {
            // Green for Good (Usage Down), Red for Bad (Usage Up)
            if (isGood)
            {
                // Hex: #00CC6A
                return new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 204, 106));
            }
            else
            {
                // Hex: #FF4D4F
                return new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 77, 79));
            }
        }

        // Helper method for background audio empty state
        public Visibility IsBackgroundAudioEmpty(int count)
        {
            return count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        // Helper method for AFK section visibility
        public Visibility IsAfkVisible(TimeSpan afkDuration, TimeSpan lockDuration)
        {
            return (afkDuration > TimeSpan.Zero || lockDuration > TimeSpan.Zero) 
                ? Visibility.Visible 
                : Visibility.Collapsed;
        }

        // Helper method for AFK empty state visibility
        public Visibility IsAfkEmpty(TimeSpan afkDuration, TimeSpan lockDuration)
        {
            return (afkDuration <= TimeSpan.Zero && lockDuration <= TimeSpan.Zero) 
                ? Visibility.Visible 
                : Visibility.Collapsed;
        }

        private void UpdateViewMode(bool showCombined)
        {
            if (showCombined)
            {
                TabViewMode.Visibility = Visibility.Collapsed;
                CombinedViewMode.Visibility = Visibility.Visible;
            }
            else
            {
                TabViewMode.Visibility = Visibility.Visible;
                CombinedViewMode.Visibility = Visibility.Collapsed;
            }
        }

        // === Spinable Pie Chart Easter Egg: Event Handlers ===







    }
}
