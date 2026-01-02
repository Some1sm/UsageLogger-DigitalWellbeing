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
    }
}
