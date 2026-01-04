using DigitalWellbeingWinUI3.ViewModels;
using DigitalWellbeingWinUI3.Controls;
using Microsoft.UI.Xaml.Controls;

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


            // Inject Trend Chart Removed (Handled in XAML)

            // Inject Custom Treemap (Unchanged)
            try
            {
                if (HistoryChartContainer.Content == null)
                {
                    var treemap = new CustomTreemap();
                    
                    var binding = new Microsoft.UI.Xaml.Data.Binding 
                    { 
                        Source = ViewModel, 
                        Path = new Microsoft.UI.Xaml.PropertyPath("TreemapData"), 
                        Mode = Microsoft.UI.Xaml.Data.BindingMode.OneWay 
                    };
                    treemap.SetBinding(CustomTreemap.ItemsSourceProperty, binding);

                    HistoryChartContainer.Content = treemap;
                }
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CRITICAL] Treemap Injection Failed: {ex}");
                HistoryChartContainer.Content = new TextBlock { Text = "Chart Error", Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0)) };
            }

            // Inject Heatmap Chart Removed (Handled in XAML)
            
            /* 
            // Wire up Heatmap Click Event (can be done via XAML EventBinding or here if exposed)
            // Commented out until Win2DHeatmap.CellClicked is verified/exposed and control is restored.
            HeatMapContainer.CellClicked += (day, hour) =>
            {
                 if (App.MainWindow?.RootFrame != null)
                 {
                      // Logic to convert day/hour to date
                      // This is tricky without knowing the StartDate context here if not using ViewModel.NavigateToDate.
                      // But ViewModel has OnHeatmapCellClicked!
                      ViewModel.OnHeatmapCellClicked(day, hour);
                 }
            };
            */

            // Auto-Generate if empty
            if (ViewModel.TrendData.Count == 0)
            {
                ViewModel.GenerateChart();
            }
        }
    }
}
