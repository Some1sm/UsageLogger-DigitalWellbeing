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
                List<AppUsage> allUsage = await LoadDataForDateRange(StartDate.Date, EndDate.Date);
                Debug.WriteLine($"[HistoryViewModel] Loaded {allUsage.Count} usage records.");

                if (IsTagView)
                {
                    GenerateTagChart(allUsage);
                }
                else
                {
                    GenerateAppChart(allUsage);
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

        private async Task<List<AppUsage>> LoadDataForDateRange(DateTime start, DateTime end)
        {
            List<AppUsage> total = new List<AppUsage>();
            for (DateTime date = start; date <= end; date = date.AddDays(1))
            {
                var daily = await AppUsageViewModel.GetData(date);
                total.AddRange(daily);
            }
            return total;
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

            var newSeries = new ObservableCollection<ISeries>();
            foreach (var kvp in tagDurations.OrderByDescending(k => k.Value))
            {
                if (kvp.Value > 0)
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
                            DataLabelsFormatter = (p) => $"{p.Context.Series.Name}: {TimeSpan.FromMinutes(p.Coordinate.PrimaryValue).TotalHours:F1}h"
                        });
                    }
                    catch
                    {
                        // Fallback
                        newSeries.Add(new PieSeries<double>
                        {
                            Values = new ObservableCollection<double> { kvp.Value },
                            Name = AppTagHelper.GetTagDisplayName(kvp.Key),
                             DataLabelsFormatter = (p) => $"{p.Context.Series.Name}: {TimeSpan.FromMinutes(p.Coordinate.PrimaryValue).TotalHours:F1}h"
                        });
                    }
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

            var newSeries = new ObservableCollection<ISeries>();
            foreach (var kvp in appDurations.OrderByDescending(k => k.Value).Take(15)) // Top 15
            {
                newSeries.Add(new PieSeries<double>
                {
                    Values = new ObservableCollection<double> { kvp.Value },
                    Name = kvp.Key,
                    DataLabelsFormatter = (p) => $"{p.Context.Series.Name}: {TimeSpan.FromMinutes(p.Coordinate.PrimaryValue).TotalHours:F1}h"
                });
            }

             if (newSeries.Count == 0) AddNoData(newSeries);
            ChartSeries = newSeries;
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
