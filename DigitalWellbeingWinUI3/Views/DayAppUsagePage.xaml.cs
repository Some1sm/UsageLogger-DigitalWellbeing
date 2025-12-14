using DigitalWellbeingWinUI3.ViewModels;
using DigitalWellbeingWinUI3.Models;
using DigitalWellbeingWinUI3.Helpers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading.Tasks;
using System.Linq; // Added for FirstOrDefault

namespace DigitalWellbeingWinUI3.Views
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

            // -----------------------------------------------------------------------------------------
            // CRITICAL STABILITY LOGIC:
            // LiveCharts2 components can sometimes fail to render or initialize depending on the environment.
            // Putting them in try-catch blocks ensures the rest of the dashboard (App List) still loads even if charts fail.
            // -----------------------------------------------------------------------------------------

            // 1. Inject Bar Chart (CartesianChart)
            try
            {
                if (BarChartContainer.Content == null)
                {
                    var barChart = new LiveChartsCore.SkiaSharpView.WinUI.CartesianChart
                    {
                        Series = ViewModel.WeeklyChartSeries,
                        XAxes = ViewModel.WeeklyChartXAxes,
                        DataPointerDownCommand = ViewModel.ChartClickCommand,
                        TooltipPosition = LiveChartsCore.Measure.TooltipPosition.Top,
                        Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent),
                        HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Stretch,
                        VerticalAlignment = Microsoft.UI.Xaml.VerticalAlignment.Stretch
                    };
                    BarChartContainer.Content = barChart;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CRITICAL] Bar Chart Injection Failed: {ex}");
                BarChartContainer.Content = new TextBlock { Text = "Chart Error", Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0)) };
            }

            // 2. Inject Pie Chart (PieChart)
            try
            {
                if (PieChartContainer.Content == null)
                {
                    // THEME DETECTION FOR CHART TEXT
                    var isDark = this.ActualTheme == Microsoft.UI.Xaml.ElementTheme.Dark;
                    if (this.ActualTheme == Microsoft.UI.Xaml.ElementTheme.Default)
                    {
                        isDark = Microsoft.UI.Xaml.Application.Current.RequestedTheme == Microsoft.UI.Xaml.ApplicationTheme.Dark;
                    }
                    var legendColor = isDark ? SkiaSharp.SKColors.White : SkiaSharp.SKColors.Black;

                    var pieChart = new LiveChartsCore.SkiaSharpView.WinUI.PieChart
                    {
                        Series = ViewModel.DayPieChartSeries,
                        LegendPosition = LiveChartsCore.Measure.LegendPosition.Right,
                        InitialRotation = -90,
                        Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent),
                        LegendBackgroundPaint = null,
                        LegendTextPaint = new LiveChartsCore.SkiaSharpView.Painting.SolidColorPaint(legendColor),
                        HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Stretch,
                        VerticalAlignment = Microsoft.UI.Xaml.VerticalAlignment.Stretch
                    };
                    PieChartContainer.Content = pieChart;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CRITICAL] Pie Chart Injection Failed: {ex}");
                PieChartContainer.Content = new TextBlock { Text = "Chart Error", Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0)) };
            }

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
                    ViewModel.LoadedDate = System.DateTime.Now;
                }
                else
                {
                    ViewModel.LoadedDate = newDate;
                }
                
                ViewModel.RefreshDayView();
                
                // Hide flyout
                CalendarFlyout.Hide();
            }
        }

        private void ChangeDay(int offset)
        {
             // Find current index
             if (ViewModel == null || ViewModel.WeeklyChartLabelDates == null) return;
             
             int currentIndex = -1;
             for(int i=0; i<ViewModel.WeeklyChartLabelDates.Length; i++)
             {
                 if (ViewModel.WeeklyChartLabelDates[i].Date == ViewModel.LoadedDate.Date)
                 {
                     currentIndex = i;
                     break;
                 }
             }
             
             if (currentIndex != -1)
             {
                 int newIndex = currentIndex + offset;
                 if (newIndex >= 0 && newIndex < ViewModel.WeeklyChartLabelDates.Length)
                 {
                     ViewModel.WeeklyChart_SelectionChanged(newIndex);
                 }
             }
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
            if (sender is MenuFlyout flyout && flyout.Target is FrameworkElement target && target.DataContext is DigitalWellbeingWinUI3.Models.AppUsageListItem appItem)
            {
                var categoryItem = flyout.Items.OfType<MenuFlyoutSubItem>().FirstOrDefault(i => i.Text == "Set Category");
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
                PlaceholderText = "Minutes (e.g., 60)", 
                InputScope = new Microsoft.UI.Xaml.Input.InputScope { Names = { new Microsoft.UI.Xaml.Input.InputScopeName(Microsoft.UI.Xaml.Input.InputScopeNameValue.Number) } } 
            };
            
            // Should check if existing limit exists? For now, just overwrite
            if (DigitalWellbeingWinUI3.Helpers.UserPreferences.AppTimeLimits.ContainsKey(processName))
            {
               inputTextBox.Text = DigitalWellbeingWinUI3.Helpers.UserPreferences.AppTimeLimits[processName].ToString();
            }

            var dialog = new ContentDialog
            {
                Title = $"Set Time Limit for {processName}",
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
                    DigitalWellbeingWinUI3.Helpers.UserPreferences.UpdateAppTimeLimit(processName, TimeSpan.FromMinutes(minutes));
                }
                else
                {
                    // Remove limit if empty or 0
                    DigitalWellbeingWinUI3.Helpers.UserPreferences.UpdateAppTimeLimit(processName, TimeSpan.Zero);
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
                DigitalWellbeingWinUI3.Helpers.AppTagHelper.UpdateAppTag(processName, (DigitalWellbeing.Core.Models.AppTag)customTag.Id);
                // Refresh
                ViewModel.LoadWeeklyData();
            }
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
                if (!DigitalWellbeingWinUI3.Helpers.UserPreferences.UserExcludedProcesses.Contains(processName))
                {
                    DigitalWellbeingWinUI3.Helpers.UserPreferences.UserExcludedProcesses.Add(processName);
                    DigitalWellbeingWinUI3.Helpers.UserPreferences.Save();
                    
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
            string currentDisplayName = DigitalWellbeingWinUI3.Helpers.UserPreferences.GetDisplayName(processName);
            bool hasCustomName = currentDisplayName != processName;

            var inputTextBox = new TextBox 
            { 
                PlaceholderText = $"Enter display name for {processName}", 
                Text = hasCustomName ? currentDisplayName : ""
            };
            
            var dialog = new ContentDialog
            {
                Title = $"Set Display Name for {processName}",
                Content = new StackPanel
                {
                    Children =
                    {
                        new TextBlock 
                        { 
                            Text = "Set a custom display name for this process. The original process name will still be used for tracking.",
                            TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap,
                            Margin = new Microsoft.UI.Xaml.Thickness(0, 0, 0, 12)
                        },
                        inputTextBox
                    }
                },
                PrimaryButtonText = "Save",
                SecondaryButtonText = hasCustomName ? "Remove" : "Cancel",
                CloseButtonText = "Cancel",
                XamlRoot = this.XamlRoot
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                // Save the new display name
                string newDisplayName = inputTextBox.Text.Trim();
                if (!string.IsNullOrWhiteSpace(newDisplayName) && newDisplayName != processName)
                {
                    DigitalWellbeingWinUI3.Helpers.UserPreferences.SetDisplayName(processName, newDisplayName);
                }
                else
                {
                    // If empty or same as process name, remove the custom display name
                    DigitalWellbeingWinUI3.Helpers.UserPreferences.RemoveDisplayName(processName);
                }
                
                // Refresh the view
                ViewModel.RefreshDayView();
            }
            else if (result == ContentDialogResult.Secondary && hasCustomName)
            {
                // Remove the display name
                DigitalWellbeingWinUI3.Helpers.UserPreferences.RemoveDisplayName(processName);
                ViewModel.RefreshDayView();
            }
        }

        private async void MenuFlyoutItem_SetCustomIcon_Click(object sender, RoutedEventArgs e)
        {
            var item = sender as MenuFlyoutItem;
            string processName = item.Tag as string;
            if (string.IsNullOrEmpty(processName)) return;

            // Check if already has a custom icon
            string currentCustomIconPath = DigitalWellbeingWinUI3.Helpers.UserPreferences.GetCustomIconPath(processName);
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
                    DigitalWellbeingWinUI3.Helpers.IconManager.DeleteCustomIcon(processName);
                    DigitalWellbeingWinUI3.Helpers.UserPreferences.RemoveCustomIconPath(processName);
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
                    string newIconPath = DigitalWellbeingWinUI3.Helpers.IconManager.CopyCustomIcon(file.Path, processName);
                    
                    if (!string.IsNullOrEmpty(newIconPath))
                    {
                        // Save to preferences
                        DigitalWellbeingWinUI3.Helpers.UserPreferences.SetCustomIconPath(processName, newIconPath);
                        
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

        public void SubItem_RightTapped(object sender, Microsoft.UI.Xaml.Input.RightTappedRoutedEventArgs e)
        {
            var grid = sender as Grid;
            var subItem = grid?.DataContext as AppUsageSubItem;

            if (subItem != null)
            {
                var flyout = new MenuFlyout();
                var tagItem = new MenuFlyoutItem { Text = "Set Category (Tag)", Icon = new SymbolIcon(Symbol.Tag) };
                tagItem.Click += async (s, args) => await OpenTitleTagDialog(subItem);
                flyout.Items.Add(tagItem);
                flyout.ShowAt(grid, e.GetPosition(grid));
                e.Handled = true;
            }
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

            var choices = DigitalWellbeingWinUI3.Helpers.AppTagHelper.GetComboBoxChoices();
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
                    DigitalWellbeingWinUI3.Helpers.AppTagHelper.UpdateTitleTag(item.ParentProcessName, keyword, tagId);
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

        private void ViewModeToggle_Click(object sender, RoutedEventArgs e)
        {
            bool showCombined = ViewModeToggle.IsChecked == true;
            UpdateViewMode(showCombined);
            UserPreferences.ShowCombinedAudioView = showCombined;
            UserPreferences.Save();
        }

        private void UpdateViewMode(bool showCombined)
        {
            if (showCombined)
            {
                TabViewMode.Visibility = Visibility.Collapsed;
                CombinedViewMode.Visibility = Visibility.Visible;
                ViewModeToggle.IsChecked = true;
                ViewModeText.Text = "Combined";
            }
            else
            {
                TabViewMode.Visibility = Visibility.Visible;
                CombinedViewMode.Visibility = Visibility.Collapsed;
                ViewModeToggle.IsChecked = false;
                ViewModeText.Text = "Tabs";
            }
        }
    }
}
