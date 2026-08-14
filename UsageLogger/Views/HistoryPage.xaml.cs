using System;
using System.Linq;
using System.Threading.Tasks;
using UsageLogger.ViewModels;
using UsageLogger.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace UsageLogger.Views
{
    public sealed partial class HistoryPage : Page
    {
        public HistoryViewModel ViewModel { get; }

        public HistoryPage()
        {
            this.InitializeComponent();
            this.NavigationCacheMode = Microsoft.UI.Xaml.Navigation.NavigationCacheMode.Required;
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

        protected override void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            UpdateLocalizedTexts();
            
            // Ensure heatmap and charts redraw when navigating back to cached HistoryPage
            DispatcherQueue?.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
            {
                HeatMapContainer?.InvalidateCanvas();
            });
        }

        private void HistoryPage_Loaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            UpdateLocalizedTexts();


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

        private void AppSearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            // Only update suggestions when user is typing, not when a suggestion is chosen
            if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            {
                sender.ItemsSource = ViewModel.GetSearchSuggestions(sender.Text);
            }
        }

        private void AppSearchBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
        {
            if (args.SelectedItem is string selected)
            {
                sender.Text = selected;
            }
        }

        private void AppSearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            string query = args.ChosenSuggestion as string ?? args.QueryText;
            bool exactMatch = args.ChosenSuggestion != null;
            ViewModel.SearchApp(query, exactMatch);
        }

        private async void BtnVisualReport_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            try
            {
                var dialog = new VisualReportDialog
                {
                    XamlRoot = this.XamlRoot
                };
                
                DateTime refDate = ViewModel.EndDate != default ? ViewModel.EndDate.DateTime : DateTime.Now;
                _ = dialog.InitializeReportAsync(refDate);
                await dialog.ShowAsync();
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[VisualReport] ShowDialog error: {ex.Message}");
            }
        }

        private void UpdateLocalizedTexts()
        {
            if (TxtVisualReport != null)
            {
                var localized = Helpers.LocalizationHelper.GetString("History_VisualReportText.Text");
                if (string.IsNullOrEmpty(localized) || localized == "History_VisualReportText.Text")
                {
                    localized = Helpers.LocalizationHelper.GetString("History_VisualReportText");
                }
                if (!string.IsNullOrEmpty(localized) && localized != "History_VisualReportText" && localized != "History_VisualReportText.Text")
                {
                    TxtVisualReport.Text = localized;
                }
            }
        }

        private async void BtnExportCsv_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            if (ViewModel.CachedSessions == null || ViewModel.CachedSessions.Count == 0) return;
            DateTime start = ViewModel.StartDate != default ? ViewModel.StartDate.DateTime : DateTime.Now.Date;
            DateTime end = ViewModel.EndDate != default ? ViewModel.EndDate.DateTime : DateTime.Now.Date;
            await Helpers.DataExportHelper.ExportToCsvAsync(ViewModel.CachedSessions, start, end);
        }

        private async void BtnExportJson_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            if (ViewModel.CachedSessions == null || ViewModel.CachedSessions.Count == 0) return;
            DateTime start = ViewModel.StartDate != default ? ViewModel.StartDate.DateTime : DateTime.Now.Date;
            DateTime end = ViewModel.EndDate != default ? ViewModel.EndDate.DateTime : DateTime.Now.Date;
            await Helpers.DataExportHelper.ExportToJsonAsync(ViewModel.CachedSessions, start, end);
        }

        private ToolTip _prodBarToolTip = new ToolTip();
        private ToolTip _dayPartBarToolTip = new ToolTip();

        private void ProdBar_PointerMoved(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (ProdBarContainer == null || ViewModel == null) return;
            double actualWidth = ProdBarContainer.ActualWidth;
            if (actualWidth <= 0) return;

            var pt = e.GetCurrentPoint(ProdBarContainer).Position;
            double xRatio = Math.Clamp(pt.X / actualWidth, 0.0, 1.0);

            double prodPct = ViewModel.ProductivePercent;
            double neutPct = ViewModel.NeutralPercent;
            double leisPct = ViewModel.LeisurePercent;
            double totalPct = prodPct + neutPct + leisPct;

            string info;
            if (totalPct <= 0.001)
            {
                info = ViewModel.ProductivityBreakdownText;
            }
            else
            {
                double normProd = prodPct / totalPct;
                double normNeut = neutPct / totalPct;

                if (xRatio < normProd)
                {
                    info = ViewModel.ProductiveTooltip;
                }
                else if (xRatio < normProd + normNeut)
                {
                    info = ViewModel.NeutralTooltip;
                }
                else
                {
                    info = ViewModel.LeisureTooltip;
                }
            }

            if (TxtProdHoverStatus != null) TxtProdHoverStatus.Text = info;
            _prodBarToolTip.Content = info;
            _prodBarToolTip.IsOpen = true;
        }

        private void ProdBar_PointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (TxtProdHoverStatus != null && ViewModel != null)
            {
                TxtProdHoverStatus.Text = ViewModel.ProductivityBreakdownText;
            }
            if (_prodBarToolTip != null)
            {
                _prodBarToolTip.IsOpen = false;
            }
        }

        private void DayPartBar_PointerMoved(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (DayPartBarContainer == null || ViewModel == null) return;
            double actualWidth = DayPartBarContainer.ActualWidth;
            if (actualWidth <= 0) return;

            var pt = e.GetCurrentPoint(DayPartBarContainer).Position;
            double xRatio = Math.Clamp(pt.X / actualWidth, 0.0, 1.0);

            double mornPct = ViewModel.MorningPercent;
            double aftPct = ViewModel.AfternoonPercent;
            double evePct = ViewModel.EveningPercent;
            double nitePct = ViewModel.NightPercent;
            double totalPct = mornPct + aftPct + evePct + nitePct;

            string info;
            if (totalPct <= 0.001)
            {
                info = ViewModel.DayPartBreakdownText;
            }
            else
            {
                double normMorn = mornPct / totalPct;
                double normAft = aftPct / totalPct;
                double normEve = evePct / totalPct;

                if (xRatio < normMorn)
                {
                    info = ViewModel.MorningTooltip;
                }
                else if (xRatio < normMorn + normAft)
                {
                    info = ViewModel.AfternoonTooltip;
                }
                else if (xRatio < normMorn + normAft + normEve)
                {
                    info = ViewModel.EveningTooltip;
                }
                else
                {
                    info = ViewModel.NightTooltip;
                }
            }

            if (TxtDayPartHoverStatus != null) TxtDayPartHoverStatus.Text = info;
            _dayPartBarToolTip.Content = info;
            _dayPartBarToolTip.IsOpen = true;
        }

        private void DayPartBar_PointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (TxtDayPartHoverStatus != null && ViewModel != null)
            {
                TxtDayPartHoverStatus.Text = ViewModel.DayPartBreakdownText;
            }
            if (_dayPartBarToolTip != null)
            {
                _dayPartBarToolTip.IsOpen = false;
            }
        }

        private void LegendProductive_PointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (TxtProdHoverStatus != null && ViewModel != null) TxtProdHoverStatus.Text = ViewModel.ProductiveTooltip;
        }

        private void LegendNeutral_PointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (TxtProdHoverStatus != null && ViewModel != null) TxtProdHoverStatus.Text = ViewModel.NeutralTooltip;
        }

        private void LegendLeisure_PointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (TxtProdHoverStatus != null && ViewModel != null) TxtProdHoverStatus.Text = ViewModel.LeisureTooltip;
        }

        private void LegendMorning_PointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (TxtDayPartHoverStatus != null && ViewModel != null) TxtDayPartHoverStatus.Text = ViewModel.MorningTooltip;
        }

        private void LegendAfternoon_PointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (TxtDayPartHoverStatus != null && ViewModel != null) TxtDayPartHoverStatus.Text = ViewModel.AfternoonTooltip;
        }

        private void LegendEvening_PointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (TxtDayPartHoverStatus != null && ViewModel != null) TxtDayPartHoverStatus.Text = ViewModel.EveningTooltip;
        }

        private void LegendNight_PointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (TxtDayPartHoverStatus != null && ViewModel != null) TxtDayPartHoverStatus.Text = ViewModel.NightTooltip;
        }

        private void AppSwitchesFlyout_Opening(object sender, object e)
        {
            if (sender is Flyout flyout && flyout.Content is FrameworkElement fe)
            {
                var targetTheme = (App.MainWindow?.Content as FrameworkElement)?.ActualTheme ?? this.ActualTheme;
                fe.RequestedTheme = targetTheme;
            }
        }

        private void AppSwitchesFlyout_Opened(object sender, object e)
        {
            if (sender is Flyout flyout && flyout.Content is FrameworkElement fe)
            {
                var targetTheme = (App.MainWindow?.Content as FrameworkElement)?.ActualTheme ?? this.ActualTheme;
                fe.RequestedTheme = targetTheme;
                if (Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(fe) is FrameworkElement presenter)
                {
                    presenter.RequestedTheme = targetTheme;
                }
            }
        }
    }
}
