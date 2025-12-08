using DigitalWellbeingWinUI3.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace DigitalWellbeingWinUI3.Views
{
    public sealed partial class HistoryPage : Page
    {
        public HistoryViewModel ViewModel { get; }

        public HistoryPage()
        {
            this.InitializeComponent();
            ViewModel = new HistoryViewModel();
            this.DataContext = ViewModel;
            this.Loaded += HistoryPage_Loaded;
        }

        private void HistoryPage_Loaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            // Inject Pie Chart Safe Mode
            try
            {
                if (HistoryChartContainer.Content == null)
                {
                    var pieChart = new LiveChartsCore.SkiaSharpView.WinUI.PieChart
                    {
                        LegendPosition = LiveChartsCore.Measure.LegendPosition.Right,
                        InitialRotation = -90,
                    };
                    
                    // Use Binding to handle ViewModel replacing the Collection instance
                    var binding = new Microsoft.UI.Xaml.Data.Binding 
                    { 
                        Source = ViewModel, 
                        Path = new Microsoft.UI.Xaml.PropertyPath("ChartSeries"), 
                        Mode = Microsoft.UI.Xaml.Data.BindingMode.OneWay 
                    };
                    pieChart.SetBinding(LiveChartsCore.SkiaSharpView.WinUI.PieChart.SeriesProperty, binding);

                    HistoryChartContainer.Content = pieChart;
                }
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CRITICAL] History Chart Injection Failed: {ex}");
                HistoryChartContainer.Content = new TextBlock { Text = "Chart Error", Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0)) };
            }

            // Auto-Generate if empty
            if (ViewModel.ChartSeries.Count == 0)
            {
                ViewModel.GenerateChart();
            }
        }
    }
}
