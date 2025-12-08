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
            this.InitializeComponent();
            ViewModel = new AppUsageViewModel();
            this.DataContext = ViewModel;

            // Manual Binding to ensure they are set
            WeeklyChart.Series = ViewModel.WeeklyChartSeries;
            WeeklyChart.XAxes = ViewModel.WeeklyChartXAxes;
            DayPieChart.Series = ViewModel.DayPieChartSeries;
        }

        private void BtnPrev_Click(object sender, RoutedEventArgs e)
        {
            // ViewModel.LoadPreviousDay(); // Need to expose this in ViewModel
            // For now, hack it via index if needed or add method
            // Actually, ViewModel.WeeklyChart_SelectionChanged logic is needed here.
            
            // Logic: LoadedDate - 1 day.
            // But strict "WeeklyChart_SelectionChanged" assumes looking at the loaded week's data array.
            // If I go back past the week, I need to reload the whole week? 
            // Original WPF app: "CanGoPrev" logic is bound to NumberOfDaysToDisplay.
            // It just selects from the array.
            
            // I'll implement LoadPreviousDay/NextDay in ViewModel properly or just use indices.
            // Let's assume ViewModel has LoadPreviousDay/NextDay methods (I didn't add them yet).
            
            // Checking ViewModel...
            // I removed LoadPreviousDay/NextDay during porting or didn't add them.
            // I will add them or just do it here:
            
            /*
            int currentIndex = Array.FindIndex(ViewModel.WeeklyChartLabelDates, d => d.Date == ViewModel.LoadedDate.Date);
            if (currentIndex > 0)
                ViewModel.WeeklyChart_SelectionChanged(currentIndex - 1);
            */
            
            ChangeDay(-1);
        }

        private void BtnNext_Click(object sender, RoutedEventArgs e)
        {
            ChangeDay(1);
        }

        private void ChangeDay(int offset)
        {
             // Find current index
             if (ViewModel.WeeklyChartLabelDates == null) return;
             
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
            // var grid = sender as Grid;
            // var dataContext = grid.DataContext;
        }

        private async void MenuFlyoutItem_SetTimeLimit_Click(object sender, RoutedEventArgs e)
        {
            var item = sender as MenuFlyoutItem;
            string processName = item.Tag as string;

            if (string.IsNullOrEmpty(processName)) return;

            // Create Dialog UI
            StackPanel panel = new StackPanel { Spacing = 10 };
            panel.Children.Add(new TextBlock { Text = "Set the app's time limit (hh:mm). Set 0 to disable.", TextWrapping = TextWrapping.Wrap });

            StackPanel inputPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
            NumberBox nbHours = new NumberBox { Minimum = 0, Maximum = 23, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact, Header = "Hours", Value = 0 };
            NumberBox nbMinutes = new NumberBox { Minimum = 0, Maximum = 59, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact, Header = "Minutes", Value = 0 };
            
            // Load existing
            if (Helpers.UserPreferences.AppTimeLimits.ContainsKey(processName))
            {
                TimeSpan ts = TimeSpan.FromMinutes(Helpers.UserPreferences.AppTimeLimits[processName]);
                nbHours.Value = ts.Hours;
                nbMinutes.Value = ts.Minutes;
            }

            inputPanel.Children.Add(nbHours);
            inputPanel.Children.Add(nbMinutes);
            panel.Children.Add(inputPanel);

            ContentDialog dialog = new ContentDialog
            {
                Title = $"Set Time Limit for {processName}",
                Content = panel,
                PrimaryButtonText = "Apply",
                CloseButtonText = "Cancel",
                XamlRoot = this.XamlRoot
            };

            var result = await dialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                int h = (int)nbHours.Value;
                int m = (int)nbMinutes.Value;
                Helpers.UserPreferences.UpdateAppTimeLimit(processName, new TimeSpan(h, m, 0));
                
                // Refresh list if needed (ViewModel should handle PropertyChanged if logic depends on limits)
                ViewModel.RefreshDayView(); // Requires exposing a refresh method or triggering it
            }
        }

        private async void MenuFlyoutItem_SetAppTag_Click(object sender, RoutedEventArgs e)
        {
            var item = sender as MenuFlyoutItem;
            string processName = item.Tag as string;

            if (string.IsNullOrEmpty(processName)) return;

            // Create Dialog UI
            StackPanel panel = new StackPanel { Spacing = 10 };
            panel.Children.Add(new TextBlock { Text = "Select a category for this app.", TextWrapping = TextWrapping.Wrap });

            ComboBox cbTags = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch };
            
            var tags = Helpers.AppTagHelper.GetComboBoxChoices();
            foreach (var t in tags)
            {
                cbTags.Items.Add(t.Key);
            }

            // Load existing
            DigitalWellbeing.Core.Models.AppTag currentTag = Helpers.AppTagHelper.GetAppTag(processName);
            string currentDisplayName = Helpers.AppTagHelper.GetTagDisplayName(currentTag);
            cbTags.SelectedItem = currentDisplayName;

            panel.Children.Add(cbTags);

            ContentDialog dialog = new ContentDialog
            {
                Title = $"Set Tag for {processName}",
                Content = panel,
                PrimaryButtonText = "Apply",
                CloseButtonText = "Cancel",
                XamlRoot = this.XamlRoot
            };

            var result = await dialog.ShowAsync();

            if (result == ContentDialogResult.Primary && cbTags.SelectedItem != null)
            {
                string selectedName = cbTags.SelectedItem.ToString();
                if (tags.ContainsKey(selectedName))
                {
                    int tagVal = tags[selectedName];
                    Helpers.SettingsManager.UpdateAppTag(processName, (DigitalWellbeing.Core.Models.AppTag)tagVal);
                    
                    // Refresh
                    ViewModel.RefreshDayView();
                }
            }
        }

        private async void MenuFlyoutItem_ExcludeApp_Click(object sender, RoutedEventArgs e)
        {
            var item = sender as MenuFlyoutItem;
            string processName = item.Tag as string;

            if (string.IsNullOrEmpty(processName)) return;

            ContentDialog confirmDialog = new ContentDialog
            {
                Title = "Exclude App?",
                Content = $"Are you sure you want to exclude '{processName}' from being tracked? You can remove it from exclusions in Settings.",
                PrimaryButtonText = "Exclude",
                CloseButtonText = "Cancel",
                XamlRoot = this.XamlRoot
            };

            var result = await confirmDialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                if (!Helpers.UserPreferences.UserExcludedProcesses.Contains(processName))
                {
                    Helpers.UserPreferences.UserExcludedProcesses.Add(processName);
                    Helpers.UserPreferences.Save();
                }
                
                ViewModel.RefreshDayView();
            }
        }
    }
}
