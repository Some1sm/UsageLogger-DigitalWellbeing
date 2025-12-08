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
                    var isDark = this.ActualTheme == Microsoft.UI.Xaml.ElementTheme.Dark;
                    if (this.ActualTheme == Microsoft.UI.Xaml.ElementTheme.Default)
                    {
                        isDark = Microsoft.UI.Xaml.Application.Current.RequestedTheme == Microsoft.UI.Xaml.ApplicationTheme.Dark;
                    }
                    var legendColor = isDark ? SkiaSharp.SKColors.White : SkiaSharp.SKColors.Black;

                    var pieChart = new LiveChartsCore.SkiaSharpView.WinUI.PieChart
                    {
                        LegendPosition = LiveChartsCore.Measure.LegendPosition.Right,
                        InitialRotation = -90,
                        Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent),
                        LegendBackgroundPaint = null,
                        LegendTextPaint = new LiveChartsCore.SkiaSharpView.Painting.SolidColorPaint(legendColor)
                    };
                    
                    // IMPORTANT:
                    // We must use 'SetBinding' here instead of direct assignment (pieChart.Series = ViewModel.ChartSeries).
                    // REASON: The ViewModel recreates the 'ChartSeries' collection instance (new ObservableCollection) whenever data is refreshed.
                    // If we just assigned it once, this chart would hold onto the OLD (empty) collection and never update.
                    // The Binding ensures it always points to the CURRENT property on the ViewModel.
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
