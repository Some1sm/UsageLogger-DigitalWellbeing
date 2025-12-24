using DigitalWellbeingWinUI3.ViewModels;
using Microsoft.UI.Xaml.Controls;
using LiveChartsCore.Kernel.Sketches;
using System.Linq;

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
            
            // Subscribe to navigation event for heatmap cell clicks
            ViewModel.NavigateToDate += OnNavigateToDate;
        }

        private void OnNavigateToDate(System.DateTime date)
        {
            // Navigate to Sessions page with the selected date
            // This would require access to the main navigation frame
            // For now, we'll use the main window's navigation
            if (App.MainWindow?.RootFrame != null)
            {
                // Navigate to Sessions page - it will load the specified date
                App.MainWindow.NavigateToSessionsWithDate(date);
            }
        }

        private void HistoryPage_Loaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            var isDark = this.ActualTheme == Microsoft.UI.Xaml.ElementTheme.Dark;
            if (this.ActualTheme == Microsoft.UI.Xaml.ElementTheme.Default)
            {
                isDark = Microsoft.UI.Xaml.Application.Current.RequestedTheme == Microsoft.UI.Xaml.ApplicationTheme.Dark;
            }
            var legendColor = isDark ? SkiaSharp.SKColors.White : SkiaSharp.SKColors.Black;
            var labelPaint = new LiveChartsCore.SkiaSharpView.Painting.SolidColorPaint(isDark ? SkiaSharp.SKColors.Gray : SkiaSharp.SKColors.DarkGray);

            // Inject Trend Chart
            try
            {
                if (TrendChartContainer.Content == null)
                {
                    var trendChart = new LiveChartsCore.SkiaSharpView.WinUI.CartesianChart
                    {
                        TooltipPosition = LiveChartsCore.Measure.TooltipPosition.Top,
                        Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent),
                        LegendPosition = LiveChartsCore.Measure.LegendPosition.Top,
                        LegendTextPaint = new LiveChartsCore.SkiaSharpView.Painting.SolidColorPaint(legendColor),
                        YAxes = new[]
                        {
                            new LiveChartsCore.SkiaSharpView.Axis
                            {
                                Name = "Hours",
                                NamePaint = labelPaint,
                                LabelsPaint = labelPaint,
                                TextSize = 10
                            }
                        }
                    };

                    // Bind Series
                    var seriesBinding = new Microsoft.UI.Xaml.Data.Binding
                    {
                        Source = ViewModel,
                        Path = new Microsoft.UI.Xaml.PropertyPath("TrendSeries"),
                        Mode = Microsoft.UI.Xaml.Data.BindingMode.OneWay
                    };
                    trendChart.SetBinding(LiveChartsCore.SkiaSharpView.WinUI.CartesianChart.SeriesProperty, seriesBinding);

                    // Bind X Axes
                    var xAxesBinding = new Microsoft.UI.Xaml.Data.Binding
                    {
                        Source = ViewModel,
                        Path = new Microsoft.UI.Xaml.PropertyPath("TrendXAxes"),
                        Mode = Microsoft.UI.Xaml.Data.BindingMode.OneWay
                    };
                    trendChart.SetBinding(LiveChartsCore.SkiaSharpView.WinUI.CartesianChart.XAxesProperty, xAxesBinding);

                    TrendChartContainer.Content = trendChart;
                }
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CRITICAL] Trend Chart Injection Failed: {ex}");
                TrendChartContainer.Content = new TextBlock { Text = "Chart Error", Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0)) };
            }

            // Inject Pie Chart
            try
            {
                if (HistoryChartContainer.Content == null)
                {
                    var pieChart = new LiveChartsCore.SkiaSharpView.WinUI.PieChart
                    {
                        LegendPosition = LiveChartsCore.Measure.LegendPosition.Right,
                        InitialRotation = -90,
                        Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent),
                        LegendBackgroundPaint = null,
                        LegendTextPaint = new LiveChartsCore.SkiaSharpView.Painting.SolidColorPaint(legendColor),
                        LegendTextSize = 11
                    };
                    
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

            // Inject Heatmap Chart with click handler
            try
            {
                if (HeatMapContainer.Content == null)
                {
                    var heatChart = new LiveChartsCore.SkiaSharpView.WinUI.CartesianChart
                    {
                        XAxes = new [] 
                        { 
                            new LiveChartsCore.SkiaSharpView.Axis 
                            { 
                                Labels = new [] { "0", "1", "2", "3", "4", "5", "6", "7", "8", "9", "10", "11", "12", "13", "14", "15", "16", "17", "18", "19", "20", "21", "22", "23" },
                                LabelsRotation = 0,
                                LabelsPaint = labelPaint,
                                TextSize = 10
                            } 
                        },
                        YAxes = new [] 
                        { 
                            new LiveChartsCore.SkiaSharpView.Axis 
                            { 
                                Labels = new [] { "Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat" },
                                LabelsPaint = labelPaint,
                                TextSize = 10,
                                MinStep = 1,
                                ForceStepToMin = true
                            } 
                        },
                        TooltipPosition = LiveChartsCore.Measure.TooltipPosition.Top,
                        Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent)
                    };
                    
                    var binding = new Microsoft.UI.Xaml.Data.Binding 
                    { 
                        Source = ViewModel, 
                        Path = new Microsoft.UI.Xaml.PropertyPath("HeatMapSeries"), 
                        Mode = Microsoft.UI.Xaml.Data.BindingMode.OneWay 
                    };
                    heatChart.SetBinding(LiveChartsCore.SkiaSharpView.WinUI.CartesianChart.SeriesProperty, binding);

                    HeatMapContainer.Content = heatChart;
                }
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CRITICAL] Heatmap Injection Failed: {ex}");
                HeatMapContainer.Content = new TextBlock { Text = "Chart Error", Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0)) };
            }

            // Auto-Generate if empty
            if (ViewModel.ChartSeries.Count == 0)
            {
                ViewModel.GenerateChart();
            }
        }
    }
}
