using DigitalWellbeing.Core.Models;
using DigitalWellbeingWinUI3.Helpers;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using DigitalWellbeing.Core;
using DigitalWellbeing.Core.Data;
using LiveChartsCore.Defaults;
using LiveChartsCore.Drawing;
using LiveChartsCore.SkiaSharpView.Painting.Effects;

namespace DigitalWellbeingWinUI3.ViewModels
{
    public class HistoryViewModel : INotifyPropertyChanged
    {
        private DateTimeOffset _startDate = DateTime.Now.AddDays(-7);
        public DateTimeOffset StartDate
        {
            get => _startDate;
            set { if (_startDate != value) { _startDate = value; OnPropertyChanged(); } }
        }

        private DateTimeOffset _endDate = DateTime.Now;
        public DateTimeOffset EndDate
        {
            get => _endDate;
            set { if (_endDate != value) { _endDate = value; OnPropertyChanged(); } }
        }

        private bool _isTagView = true;
        public bool IsTagView
        {
            get => _isTagView;
            set { if (_isTagView != value) { _isTagView = value; OnPropertyChanged(); GenerateChart(); } }
        }

        private ObservableCollection<ISeries> _chartSeries;
        public ObservableCollection<ISeries> ChartSeries
        {
            get => _chartSeries;
            set { if (_chartSeries != value) { _chartSeries = value; OnPropertyChanged(); } }
        }

        private ObservableCollection<ISeries> _heatMapSeries;
        public ObservableCollection<ISeries> HeatMapSeries
        {
            get => _heatMapSeries;
            set { if (_heatMapSeries != value) { _heatMapSeries = value; OnPropertyChanged(); } }
        }

        // Heatmap cell details for tooltips and navigation
        private Dictionary<(int day, int hour), HeatmapCellData> _heatmapCellDetails = new();
        
        // Trend chart series (day-by-day bar chart)
        private ObservableCollection<ISeries> _trendSeries;
        public ObservableCollection<ISeries> TrendSeries
        {
            get => _trendSeries;
            set { if (_trendSeries != value) { _trendSeries = value; OnPropertyChanged(); } }
        }
        
        // Trend X-Axis labels (dates)
        private Axis[] _trendXAxes;
        public Axis[] TrendXAxes
        {
            get => _trendXAxes;
            set { if (_trendXAxes != value) { _trendXAxes = value; OnPropertyChanged(); } }
        }
        
        // KPI: Total hours and % change
        private string _totalHoursText;
        public string TotalHoursText
        {
            get => _totalHoursText;
            set { if (_totalHoursText != value) { _totalHoursText = value; OnPropertyChanged(); } }
        }
        
        private string _totalChangeText;
        public string TotalChangeText
        {
            get => _totalChangeText;
            set { if (_totalChangeText != value) { _totalChangeText = value; OnPropertyChanged(); } }
        }
        
        private bool _totalChangePositive;
        public bool TotalChangePositive
        {
            get => _totalChangePositive;
            set { if (_totalChangePositive != value) { _totalChangePositive = value; OnPropertyChanged(); } }
        }
        
        // Previous period average line value (for trend chart)
        private double _previousPeriodAverage;
        public double PreviousPeriodAverage
        {
            get => _previousPeriodAverage;
            set { if (_previousPeriodAverage != value) { _previousPeriodAverage = value; OnPropertyChanged(); } }
        }
        
        // Navigation event for heatmap cell click
        public event Action<DateTime> NavigateToDate;

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set { if (_isLoading != value) { _isLoading = value; OnPropertyChanged(); } }
        }

        public DelegateCommand GenerateChartCommand { get; }

        public HistoryViewModel()
        {
            ChartSeries = new ObservableCollection<ISeries>();
            TrendSeries = new ObservableCollection<ISeries>();
            GenerateChartCommand = new DelegateCommand(GenerateChart);
        }

        public async void GenerateChart()
        {
            if (EndDate < StartDate) 
            {
                Debug.WriteLine("[HistoryViewModel] EndDate < StartDate");
                return;
            }

            IsLoading = true;
            Debug.WriteLine($"[HistoryViewModel] Generating Chart for {StartDate.Date.ToShortDateString()} - {EndDate.Date.ToShortDateString()}");
            try
            {
                // Load current period
                List<AppSession> allSessions = await LoadSessionsForDateRange(StartDate.Date, EndDate.Date);
                Debug.WriteLine($"[HistoryViewModel] Loaded {allSessions.Count} sessions.");

                // Load previous period for comparison
                int dayCount = (EndDate.Date - StartDate.Date).Days + 1;
                DateTime prevStart = StartDate.Date.AddDays(-dayCount);
                DateTime prevEnd = StartDate.Date.AddDays(-1);
                List<AppSession> prevSessions = await LoadSessionsForDateRange(prevStart, prevEnd);

                // Generate Heatmap with cell details
                GenerateHeatMap(allSessions);

                // Generate Trend Chart (day-by-day bars with previous period average line)
                GenerateTrendChart(allSessions, prevSessions, StartDate.Date, EndDate.Date);

                // Aggregate for Pie Charts
                List<AppUsage> aggregatedUsage = AggregateSessions(allSessions);

                if (IsTagView)
                {
                    GenerateTagChart(aggregatedUsage);
                }
                else
                {
                    GenerateAppChart(aggregatedUsage);
                }
            }
            catch (Exception ex) 
            {
                Debug.WriteLine($"[HistoryViewModel] Generation Error: {ex}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task<List<AppSession>> LoadSessionsForDateRange(DateTime start, DateTime end)
        {
            return await Task.Run(() =>
            {
                List<AppSession> total = new List<AppSession>();
                string folder = ApplicationPath.UsageLogsFolder;
                var repo = new AppSessionRepository(folder);

                for (DateTime date = start; date <= end; date = date.AddDays(1))
                {
                    var sessions = repo.GetSessionsForDate(date);
                    total.AddRange(sessions);
                }
                return total;
            });
        }

        private List<AppUsage> AggregateSessions(List<AppSession> sessions)
        {
             var usageMap = new Dictionary<string, AppUsage>();
             
             foreach (var session in sessions)
             {
                 if (!usageMap.ContainsKey(session.ProcessName))
                 {
                     usageMap[session.ProcessName] = new AppUsage(session.ProcessName, session.ProgramName, TimeSpan.Zero);
                 }
                 
                 var appUsage = usageMap[session.ProcessName];
                 appUsage.Duration = appUsage.Duration.Add(session.Duration);
                 
                 if (string.IsNullOrEmpty(appUsage.ProgramName) && !string.IsNullOrEmpty(session.ProgramName))
                 {
                     appUsage.ProgramName = session.ProgramName;
                 }

                 // Aggregate Sub-App / Title breakdown
                 string title = !string.IsNullOrEmpty(session.ProgramName) ? session.ProgramName : session.ProcessName;
                 if (appUsage.ProgramBreakdown.ContainsKey(title))
                 {
                     appUsage.ProgramBreakdown[title] = appUsage.ProgramBreakdown[title].Add(session.Duration);
                 }
                 else
                 {
                     appUsage.ProgramBreakdown[title] = session.Duration;
                 }
             }
             return usageMap.Values.ToList();
        }

        private void GenerateHeatMap(List<AppSession> sessions)
        {
            // Grid: 7 Days (0-6) x 24 Hours (0-23)
            double[,] grid = new double[7, 24];
            var cellApps = new Dictionary<(int day, int hour), Dictionary<string, double>>();
            
            // Initialize cell app dictionaries
            for (int d = 0; d < 7; d++)
                for (int h = 0; h < 24; h++)
                    cellApps[(d, h)] = new Dictionary<string, double>();

            foreach (var s in sessions)
            {
                if (AppUsageViewModel.IsProcessExcluded(s.ProcessName)) continue;
                
                DateTime t = s.StartTime;
                while (t < s.EndTime)
                {
                    int day = (int)t.DayOfWeek;
                    int hour = t.Hour;
                    
                    DateTime slotEnd = t.Date.AddHours(hour + 1);
                    DateTime actualEnd = (s.EndTime < slotEnd) ? s.EndTime : slotEnd;
                    
                    double minutes = (actualEnd - t).TotalMinutes;
                    if (minutes > 0)
                    {
                        grid[day, hour] += minutes;
                        
                        // Track app breakdown per cell
                        var apps = cellApps[(day, hour)];
                        string name = UserPreferences.GetDisplayName(s.ProcessName);
                        if (apps.ContainsKey(name)) apps[name] += minutes;
                        else apps[name] = minutes;
                    }
                    
                    t = actualEnd;
                    if (t >= s.EndTime) break;
                }
            }

            // Store cell details for tooltips
            _heatmapCellDetails.Clear();
            string[] dayNames = { "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday" };
            
            var weightedPoints = new ObservableCollection<WeightedPoint>();
            for (int d = 0; d < 7; d++)
            {
                for (int h = 0; h < 24; h++)
                {
                    double val = grid[d, h];
                    weightedPoints.Add(new WeightedPoint(h, d, (int)val));
                    
                    // Get top app for this cell
                    var topApp = cellApps[(d, h)].OrderByDescending(x => x.Value).FirstOrDefault();
                    _heatmapCellDetails[(d, h)] = new HeatmapCellData
                    {
                        DayName = dayNames[d],
                        Hour = h,
                        TotalMinutes = val,
                        TopAppName = topApp.Key ?? "No activity",
                        TopAppMinutes = topApp.Value
                    };
                }
            }
            
            // Create HeatSeries with custom tooltip
            var series = new HeatSeries<WeightedPoint>
            {
                Values = weightedPoints,
                Name = "Activity",
                HeatMap = new[]
                {
                    new LvcColor(32, 32, 32),
                    new LvcColor(0, 50, 100),
                    new LvcColor(0, 120, 215),
                    new LvcColor(100, 200, 255)
                },
                YToolTipLabelFormatter = (chartPoint) =>
                {
                    // Access the actual WeightedPoint model directly
                    if (chartPoint.Model is WeightedPoint wp)
                    {
                        int hour = (int)(wp.X ?? 0);  // X = hour (0-23)
                        int day = (int)(wp.Y ?? 0);   // Y = day (0-6)
                        
                        if (_heatmapCellDetails.TryGetValue((day, hour), out var cell))
                        {
                            string timeStr = $"{hour:D2}:00";
                            string totalStr = FormatDuration(cell.TotalMinutes);
                            return $"{cell.DayName} at {timeStr} • {totalStr} • Top: {cell.TopAppName}";
                        }
                        return $"{wp.Weight:F0} min";
                    }
                    return "";
                }
            };

            HeatMapSeries = new ObservableCollection<ISeries> { series };
        }

        private void GenerateTrendChart(List<AppSession> currentSessions, List<AppSession> prevSessions, DateTime start, DateTime end)
        {
            // Group current period by date
            var dailyTotals = new Dictionary<DateTime, double>();
            for (var d = start; d <= end; d = d.AddDays(1))
                dailyTotals[d.Date] = 0;

            foreach (var s in currentSessions)
            {
                if (AppUsageViewModel.IsProcessExcluded(s.ProcessName)) continue;
                if (dailyTotals.ContainsKey(s.StartTime.Date))
                    dailyTotals[s.StartTime.Date] += s.Duration.TotalMinutes;
            }

            // Calculate previous period average
            double prevTotal = prevSessions
                .Where(s => !AppUsageViewModel.IsProcessExcluded(s.ProcessName))
                .Sum(s => s.Duration.TotalMinutes);
            int prevDays = (int)(start - start.AddDays(-(end - start).Days - 1)).Days;
            if (prevDays <= 0) prevDays = 1;
            double prevAvg = prevTotal / prevDays;
            PreviousPeriodAverage = prevAvg;

            // Calculate KPIs
            double currentTotal = dailyTotals.Values.Sum();
            TotalHoursText = FormatDuration(currentTotal);
            
            if (prevTotal > 0)
            {
                double changePercent = ((currentTotal - prevTotal) / prevTotal) * 100;
                TotalChangePositive = changePercent >= 0;
                string arrow = TotalChangePositive ? "↑" : "↓";
                TotalChangeText = $"{arrow} {Math.Abs(changePercent):F0}% vs previous {prevDays} days";
            }
            else
            {
                TotalChangeText = "No previous data";
                TotalChangePositive = true;
            }

            // Create bar chart
            var barValues = dailyTotals.OrderBy(x => x.Key).Select(x => x.Value / 60.0).ToList(); // Hours
            var dateLabels = dailyTotals.OrderBy(x => x.Key).Select(x => x.Key.ToString("MM/dd")).ToArray();

            var barSeries = new ColumnSeries<double>
            {
                Values = barValues,
                Name = "Daily Usage",
                Fill = new SolidColorPaint(new SKColor(0, 120, 215)),
                MaxBarWidth = 30
            };

            // Previous period average line
            var avgLine = new LineSeries<double>
            {
                Values = Enumerable.Repeat(prevAvg / 60.0, barValues.Count).ToList(),
                Name = $"Prev Period Avg ({FormatDuration(prevAvg)})",
                Stroke = new SolidColorPaint(new SKColor(255, 180, 0)) { StrokeThickness = 2, PathEffect = new DashEffect(new float[] { 6, 4 }) },
                Fill = null,
                GeometrySize = 0,
                LineSmoothness = 0
            };

            TrendSeries = new ObservableCollection<ISeries> { barSeries, avgLine };
            TrendXAxes = new Axis[]
            {
                new Axis
                {
                    Labels = dateLabels,
                    LabelsRotation = 45,
                    TextSize = 10,
                    LabelsPaint = new SolidColorPaint(SKColors.Gray)
                }
            };
        }
        
        // Method for heatmap cell click -> navigate to that day
        public void OnHeatmapCellClicked(int dayOfWeek, int hour)
        {
            // Find the actual date for this day of week within the selected range
            for (var d = StartDate.Date; d <= EndDate.Date; d = d.AddDays(1))
            {
                if ((int)d.DayOfWeek == dayOfWeek)
                {
                    NavigateToDate?.Invoke(d);
                    return;
                }
            }
        }

        private void GenerateTagChart(List<AppUsage> usage)
        {
            Dictionary<AppTag, double> tagDurations = new Dictionary<AppTag, double>();
            
            foreach (var app in usage)
            {
                if (AppUsageViewModel.IsProcessExcluded(app.ProcessName)) continue;
                
                AppTag parentTag = AppTagHelper.GetAppTag(app.ProcessName);
                double remainingMinutes = app.Duration.TotalMinutes;

                // 1. Process Sub-Apps
                if (app.ProgramBreakdown != null && app.ProgramBreakdown.Count > 0)
                {
                    foreach (var child in app.ProgramBreakdown)
                    {
                        AppTag childTag = AppTagHelper.GetTitleTag(app.ProcessName, child.Key);
                        
                        // Only split if child has a specific tag DIFFERENT from parent (or serves as a sub-category)
                        // If child is Untagged, it inherits parent tag (so we keep it in remainingMinutes)
                        if (childTag != AppTag.Untagged && childTag != parentTag)
                        {
                            double childMinutes = child.Value.TotalMinutes;
                            
                            if (tagDurations.ContainsKey(childTag))
                                tagDurations[childTag] += childMinutes;
                            else
                                tagDurations[childTag] = childMinutes;

                            remainingMinutes -= childMinutes;
                        }
                    }
                }
                
                // 2. Add remaining duration to Parent Tag
                if (remainingMinutes > 0)
                {
                    if (tagDurations.ContainsKey(parentTag))
                    {
                        tagDurations[parentTag] += remainingMinutes;
                    }
                    else
                    {
                        tagDurations[parentTag] = remainingMinutes;
                    }
                }
            }

            var filteredTags = tagDurations.Where(k => k.Value >= 1.0).ToList();
            double totalDuration = filteredTags.Sum(k => k.Value);

            var newSeries = new ObservableCollection<ISeries>();
            foreach (var kvp in filteredTags.OrderByDescending(k => k.Value))
            {
                try
                {
                    var brush = (Microsoft.UI.Xaml.Media.SolidColorBrush)AppTagHelper.GetTagColor(kvp.Key);
                    var skColor = ConvertColor(brush.Color);

                    newSeries.Add(new PieSeries<double>
                    {
                        Values = new ObservableCollection<double> { kvp.Value },
                        Name = AppTagHelper.GetTagDisplayName(kvp.Key),
                        Fill = new SolidColorPaint(skColor),
                        DataLabelsPaint = new SolidColorPaint(SKColors.White),
                        DataLabelsFormatter = (p) => (p.Coordinate.PrimaryValue / totalDuration > 0.05) ? p.Context.Series.Name : "",
                        ToolTipLabelFormatter = (p) => FormatDuration(p.Coordinate.PrimaryValue)
                    });
                }
                catch
                {
                    // Fallback
                    newSeries.Add(new PieSeries<double>
                    {
                        Values = new ObservableCollection<double> { kvp.Value },
                        Name = AppTagHelper.GetTagDisplayName(kvp.Key),
                        DataLabelsPaint = new SolidColorPaint(SKColors.Black),
                        DataLabelsFormatter = (p) => (p.Coordinate.PrimaryValue / totalDuration > 0.05) ? p.Context.Series.Name : "",
                        ToolTipLabelFormatter = (p) => FormatDuration(p.Coordinate.PrimaryValue)
                    });
                }
            }

            if (newSeries.Count == 0) AddNoData(newSeries);
            ChartSeries = newSeries;
        }

        private void GenerateAppChart(List<AppUsage> usage)
        {
            Dictionary<string, double> appDurations = new Dictionary<string, double>();

            foreach (var app in usage)
            {
                if (AppUsageViewModel.IsProcessExcluded(app.ProcessName)) continue;

                if (appDurations.ContainsKey(app.ProcessName))
                    appDurations[app.ProcessName] += app.Duration.TotalMinutes;
                else
                    appDurations[app.ProcessName] = app.Duration.TotalMinutes;
            }

            var visibleApps = appDurations.Where(k => k.Value >= 1.0).OrderByDescending(k => k.Value).Take(15).ToList();
            double totalDuration = visibleApps.Sum(k => k.Value);

            var newSeries = new ObservableCollection<ISeries>();
            foreach (var kvp in visibleApps)
            {
                newSeries.Add(new PieSeries<double>
                {
                    Values = new ObservableCollection<double> { kvp.Value },
                    Name = UserPreferences.GetDisplayName(kvp.Key),
                    DataLabelsPaint = new SolidColorPaint(SKColors.Black),
                    DataLabelsFormatter = (p) => (p.Coordinate.PrimaryValue / totalDuration > 0.05) ? p.Context.Series.Name : "",
                    ToolTipLabelFormatter = (p) => FormatDuration(p.Coordinate.PrimaryValue)
                });
            }

             if (newSeries.Count == 0) AddNoData(newSeries);
             ChartSeries = newSeries;
        }

        private string FormatDuration(double totalMinutes)
        {
            TimeSpan t = TimeSpan.FromMinutes(totalMinutes);
            return $"{(int)t.TotalHours:D2}:{t.Minutes:D2}:{t.Seconds:D2}";
        }

        private void AddNoData(ObservableCollection<ISeries> series)
        {
            series.Add(new PieSeries<double>
            {
                Values = new ObservableCollection<double> { 1 },
                Name = "No Data",
                Fill = new SolidColorPaint(SKColors.LightGray)
            });
        }

        private SKColor ConvertColor(Windows.UI.Color color)
        {
            return new SKColor(color.R, color.G, color.B, color.A);
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    public class DelegateCommand : System.Windows.Input.ICommand
    {
        private readonly Action _action;

        public DelegateCommand(Action action)
        {
            _action = action;
        }

        public bool CanExecute(object parameter)
        {
            return true;
        }

        public void Execute(object parameter)
        {
            _action();
        }

        #pragma warning disable CS0067
        public event EventHandler CanExecuteChanged;
        #pragma warning restore CS0067
    }

    public class HeatmapCellData
    {
        public string DayName { get; set; }
        public int Hour { get; set; }
        public double TotalMinutes { get; set; }
        public string TopAppName { get; set; }
        public double TopAppMinutes { get; set; }
    }
}
