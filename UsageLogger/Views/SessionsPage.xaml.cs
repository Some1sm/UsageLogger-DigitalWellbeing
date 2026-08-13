using UsageLogger.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace UsageLogger.Views
{
    public sealed partial class SessionsPage : Page
    {
        public SessionsViewModel ViewModel { get; }

        public SessionsPage()
        {
            this.InitializeComponent();
            this.NavigationCacheMode = Microsoft.UI.Xaml.Navigation.NavigationCacheMode.Required;
            ViewModel = new SessionsViewModel();
        }
        
        private int _lastDetailedDays = -1;
        private bool _lastMergeInterruptions = true;
        private int _lastInterruptionThreshold = -1;
        
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            
            // Handle date parameter from heatmap navigation
            if (e.Parameter is System.DateTime date)
            {
                ViewModel.SelectedDate = date;
            }

            bool settingsChanged = false;

            if (_lastDetailedDays != UsageLogger.Helpers.UserPreferences.DetailedUsageDayCount)
            {
                _lastDetailedDays = UsageLogger.Helpers.UserPreferences.DetailedUsageDayCount;
                settingsChanged = true;
            }

            if (_lastMergeInterruptions != UsageLogger.Helpers.UserPreferences.MergeShortInterruptions ||
                _lastInterruptionThreshold != UsageLogger.Helpers.UserPreferences.InterruptionThresholdSeconds)
            {
                _lastMergeInterruptions = UsageLogger.Helpers.UserPreferences.MergeShortInterruptions;
                _lastInterruptionThreshold = UsageLogger.Helpers.UserPreferences.InterruptionThresholdSeconds;
                settingsChanged = true;
            }

            if (settingsChanged)
            {
                ViewModel.LoadSessions();
            }
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
                    ViewModel.SelectedDate = System.DateTime.Now;
                }
                else
                {
                    ViewModel.SelectedDate = newDate;
                }
                
                // Hide flyout
                CalendarFlyout.Hide();
            }
        }

        private void TimelineContainer_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            ViewModel.TotalAvailableWidth = e.NewSize.Width - 2; // Subtract border
        }

        private void TimelineSearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            {
                var suggestions = ViewModel.GetFilterSuggestions(sender.Text);
                sender.ItemsSource = suggestions;
                ViewModel.FilterQuery = sender.Text;
            }
            else if (args.Reason == AutoSuggestionBoxTextChangeReason.ProgrammaticChange)
            {
                ViewModel.FilterQuery = sender.Text;
            }
        }

        private void TimelineSearchBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
        {
            if (args.SelectedItem is string chosen)
            {
                sender.Text = chosen;
                ViewModel.FilterQuery = chosen;
            }
        }

        private void TimelineSearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            ViewModel.FilterQuery = sender.Text;
        }
    }

    public class DateFormatConverter : Microsoft.UI.Xaml.Data.IValueConverter
    {
        public object Convert(object value, System.Type targetType, object parameter, string language)
        {
            if (value is System.DateTime dt)
            {
                if (dt.Date == System.DateTime.Now.Date) return UsageLogger.Helpers.LocalizationHelper.GetString("History_Period_Today");
                if (dt.Date == System.DateTime.Now.AddDays(-1).Date) return UsageLogger.Helpers.LocalizationHelper.GetString("History_Period_Yesterday");
                if (dt.Date == System.DateTime.Now.AddDays(1).Date) return UsageLogger.Helpers.LocalizationHelper.GetString("History_Period_Tomorrow");
                return dt.ToString("D"); // Long date pattern (System localized)
            }
            return value;
        }

        public object ConvertBack(object value, System.Type targetType, object parameter, string language)
        {
            throw new System.NotImplementedException();
        }
    }
}
