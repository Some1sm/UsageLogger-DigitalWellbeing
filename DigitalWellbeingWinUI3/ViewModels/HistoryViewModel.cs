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
                List<AppSession> allSessions = await LoadSessionsForDateRange(StartDate.Date, EndDate.Date);
                Debug.WriteLine($"[HistoryViewModel] Loaded {allSessions.Count} sessions.");

                // Generate Heatmap
                GenerateHeatMap(allSessions);

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
                 
                 usageMap[session.ProcessName].Duration = usageMap[session.ProcessName].Duration.Add(session.Duration);
                 
                 if (string.IsNullOrEmpty(usageMap[session.ProcessName].ProgramName) && !string.IsNullOrEmpty(session.ProgramName))
                 {
                     usageMap[session.ProcessName].ProgramName = session.ProgramName;
                 }
             }
             return usageMap.Values.ToList();
        }

        private void GenerateHeatMap(List<AppSession> sessions)
        {
            // Grid: 7 Days (0-6) x 24 Hours (0-23)
            // Y Axis: Days. X Axis: Hours.
            double[,] grid = new double[7, 24];
            
            // Map DayOfWeek (Sun=0...Sat=6) which matches standard Grid (0-6).
            // Y Axis: 0 (Top) -> Sun, 6 (Bottom) -> Sat? Or reversed?
            // Heatmap coordinate (x, y) = (hour, day).

            foreach (var s in sessions)
            {
                if (AppUsageViewModel.IsProcessExcluded(s.ProcessName)) continue;
                
                DateTime t = s.StartTime;
                while (t < s.EndTime)
                {
                    int day = (int)t.DayOfWeek; // 0=Sun, 6=Sat
                    int hour = t.Hour; // 0-23
                    
                    DateTime slotEnd = t.Date.AddHours(hour + 1);
                    DateTime actualEnd = (s.EndTime < slotEnd) ? s.EndTime : slotEnd;
                    
                    double minutes = (actualEnd - t).TotalMinutes;
                    if (minutes > 0)
                    {
                        grid[day, hour] += minutes;
                    }
                    
                    t = actualEnd;
                    if (t >= s.EndTime) break;
                }
            }

            var weightedPoints = new ObservableCollection<WeightedPoint>();
            // Flatten grid
            for (int d = 0; d < 7; d++)
            {
                for (int h = 0; h < 24; h++)
                {
                    double val = grid[d, h];
                    // Always add point to fill grid structure
                    weightedPoints.Add(new WeightedPoint(h, d, (int)val));
                }
            }
            
            // Create HeatSeries
            var series = new HeatSeries<WeightedPoint>
            {
                Values = weightedPoints,
                Name = "Activity",
                HeatMap = new []
                {
                    new LvcColor(240, 248, 255), // AliceBlue
                    new LvcColor(100, 149, 237), // CornflowerBlue
                    new LvcColor(0, 0, 255),     // Blue
                    new LvcColor(0, 0, 139)      // DarkBlue
                }
            };

            HeatMapSeries = new ObservableCollection<ISeries> { series };
        }

        private void GenerateTagChart(List<AppUsage> usage)
        {
            Dictionary<AppTag, double> tagDurations = new Dictionary<AppTag, double>();
            // Initialize with 0 for all known tags (optional, but good for consistent colors if we want to show empty ones)
            // Better: Just aggregate what we have.
            
            foreach (var app in usage)
            {
                if (AppUsageViewModel.IsProcessExcluded(app.ProcessName)) continue;
                
                AppTag tag = AppTagHelper.GetAppTag(app.ProcessName);
                if (tagDurations.ContainsKey(tag))
                {
                    tagDurations[tag] += app.Duration.TotalMinutes;
                }
                else
                {
                    tagDurations[tag] = app.Duration.TotalMinutes;
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
                    Name = kvp.Key,
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
}
