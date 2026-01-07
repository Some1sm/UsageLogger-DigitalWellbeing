using DigitalWellbeingWinUI3.ViewModels;
using System;
using Microsoft.UI.Xaml;
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
        public static readonly DependencyProperty ResponsiveItemWidthProperty =
            DependencyProperty.Register(nameof(ResponsiveItemWidth), typeof(double), typeof(MiscDataPage), new PropertyMetadata(210.0));

        public double ResponsiveItemWidth
        {
            get => (double)GetValue(ResponsiveItemWidthProperty);
            set => SetValue(ResponsiveItemWidthProperty, value);
        }

        private void RootPage_SizeChanged(object sender, Microsoft.UI.Xaml.SizeChangedEventArgs e)
        {
            ApplyResponsiveLayout(e.NewSize.Width);
        }

        private void ApplyResponsiveLayout(double width)
        {
            if (width <= 0) return;

            // 1. Header Layout
            // Threshold for stacking header elements
            bool narrow = width < 500;
            if (narrow)
            {
                // Vertical Stack
                Grid.SetRow(HeaderTitlePanel, 0);
                Grid.SetColumn(HeaderTitlePanel, 0);
                Grid.SetColumnSpan(HeaderTitlePanel, 2);

                Grid.SetRow(HeaderDatePanel, 1);
                Grid.SetColumn(HeaderDatePanel, 0);
                Grid.SetColumnSpan(HeaderDatePanel, 2);
                
                HeaderDatePanel.HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Left;
                HeaderDatePanel.Margin = new Microsoft.UI.Xaml.Thickness(0, 10, 0, 0);
            }
            else
            {
                // Horizontal (Default)
                Grid.SetRow(HeaderTitlePanel, 0);
                Grid.SetColumn(HeaderTitlePanel, 0);
                Grid.SetColumnSpan(HeaderTitlePanel, 1);

                Grid.SetRow(HeaderDatePanel, 0);
                Grid.SetColumn(HeaderDatePanel, 1);
                Grid.SetColumnSpan(HeaderDatePanel, 1);

                HeaderDatePanel.HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Right;
                HeaderDatePanel.Margin = new Microsoft.UI.Xaml.Thickness(0);
            }

            // 2. Responsive Grid Items
            // Calculate best item width to fill the row
            // Base width ~210.
            // Subtract padding (24 left + 24 right = 48) + Scrollbar approx 12 = 60
            double available = width - 60; 
            if (available < 210)
            {
                ResponsiveItemWidth = Math.Max(100, available); // Stretch single column
            }
            else
            {
                // How many cols fit?
                int cols = (int)(available / 210);
                if (cols < 1) cols = 1;
                
                // Distribute space equally
                // VariableSizedWrapGrid doesn't auto-space, so we increase item width
                ResponsiveItemWidth = available / cols - 6; // -6 for margin safety
            }
        }
    }
}
