using DigitalWellbeingWinUI3.ViewModels;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace DigitalWellbeingWinUI3.Views
{
    public sealed partial class MiscDataPage : Page
    {
        public MiscDataViewModel ViewModel { get; }

        public MiscDataPage()
        {
            ViewModel = new MiscDataViewModel();
            this.InitializeComponent();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            await ViewModel.LoadDataAsync();
        }

        private void CalendarPicker_SelectedDatesChanged(CalendarView sender, CalendarViewSelectedDatesChangedEventArgs args)
        {
            if (args.AddedDates.Count > 0)
            {
                ViewModel.SelectedDate = args.AddedDates[0];
                CalendarFlyout.Hide();
            }
        }

        private void BtnPrev_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            ViewModel.PreviousDay();
        }

        private void BtnNext_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            ViewModel.NextDay();
        }
    }
}
