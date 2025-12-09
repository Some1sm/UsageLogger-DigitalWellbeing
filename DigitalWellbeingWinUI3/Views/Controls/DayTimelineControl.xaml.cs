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

        // SizeChanged removed to avoid fighting with SessionsViewModel which now controls width.
    }
}
