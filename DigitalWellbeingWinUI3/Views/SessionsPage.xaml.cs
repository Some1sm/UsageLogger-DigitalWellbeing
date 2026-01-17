using DigitalWellbeingWinUI3.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace DigitalWellbeingWinUI3.Views
{
    public sealed partial class SessionsPage : Page
    {
        public SessionsViewModel ViewModel { get; }

        public SessionsPage()
        {
            this.InitializeComponent();
            ViewModel = new SessionsViewModel();
        }
        
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            
            // Handle date parameter from heatmap navigation
            if (e.Parameter is System.DateTime date)
            {
                ViewModel.SelectedDate = date;
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

    }

    public class DateFormatConverter : Microsoft.UI.Xaml.Data.IValueConverter
    {
        public object Convert(object value, System.Type targetType, object parameter, string language)
        {
            if (value is System.DateTime dt)
            {
                if (dt.Date == System.DateTime.Now.Date) return DigitalWellbeingWinUI3.Helpers.LocalizationHelper.GetString("History_Period_Today");
                if (dt.Date == System.DateTime.Now.AddDays(-1).Date) return DigitalWellbeingWinUI3.Helpers.LocalizationHelper.GetString("History_Period_Yesterday");
                if (dt.Date == System.DateTime.Now.AddDays(1).Date) return DigitalWellbeingWinUI3.Helpers.LocalizationHelper.GetString("History_Period_Tomorrow");
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
