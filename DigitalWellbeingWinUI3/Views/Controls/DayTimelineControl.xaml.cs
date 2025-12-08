using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using DigitalWellbeingWinUI3.ViewModels;

namespace DigitalWellbeingWinUI3.Views.Controls
{
    public sealed partial class DayTimelineControl : UserControl
    {
        public DayTimelineViewModel ViewModel
        {
            get => (DayTimelineViewModel)DataContext;
            set => DataContext = value;
        }

        public DayTimelineControl()
        {
            this.InitializeComponent();
        }

        private void ContainerGrid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (ViewModel != null)
            {
                ViewModel.TimelineWidth = e.NewSize.Width;
            }
        }
    }
}
