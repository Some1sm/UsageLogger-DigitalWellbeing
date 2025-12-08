using DigitalWellbeingWinUI3.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading.Tasks;

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
                    var pieChart = new LiveChartsCore.SkiaSharpView.WinUI.PieChart
                    {
                        Series = ViewModel.DayPieChartSeries,
                        LegendPosition = LiveChartsCore.Measure.LegendPosition.Right,
                        InitialRotation = -90,
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

        private void AppListGridView_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (AppListGridView.ItemsPanelRoot is ItemsWrapGrid wrapGrid)
            {
                // MANUAL RESPONSIVE LAYOUT:
                // GridView's default UniformGrid doesn't always stretch items efficiently.
                // We manually calculate the ItemWidth to ensure cards fill the ENTIRE available width, avoiding "Right Gaps".
                
                double availableWidth = e.NewSize.Width;
                double minItemWidth = 400; // Minimum width before creating a new column

                // Calculate number of columns
                int columns = (int)Math.Floor(availableWidth / minItemWidth);
                if (columns < 1) columns = 1;

                // Set ItemWidth to fill space (minus a tiny margin for safety)
                // Note: The GridViewItem Margin (0,0,10,10) is handled inside the ItemContainer or Template
                // ItemsWrapGrid.ItemWidth defines the CELL size.
                double newWidth = (availableWidth / columns);
                
                // Subtracting 1px to avoid rounding errors causing a wrap
                wrapGrid.ItemWidth = newWidth - 1;
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

            if (Enum.TryParse(typeof(DigitalWellbeing.Core.Models.AppTag), tagStr, true, out var tagEnum))
            {
                 DigitalWellbeingWinUI3.Helpers.AppTagHelper.UpdateAppTag(processName, (DigitalWellbeing.Core.Models.AppTag)tagEnum);
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
    }
}
