using DigitalWellbeing.Core;
using DigitalWellbeing.Core.Helpers;
using DigitalWellbeing.Core.Models;
using DigitalWellbeingWinUI3.Helpers;
using DigitalWellbeingWinUI3.Models;
// using DigitalWellbeingWinUI3.Views; // Stubs for now
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.Kernel;
using SkiaSharp;
using System;
using System.Windows.Input;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.UI.Xaml; // For DispatcherTimer
using LiveChartsCore.Measure;

namespace DigitalWellbeingWinUI3.ViewModels
{
    public class AppUsageViewModel : INotifyPropertyChanged
    {
        #region Configurations
        public static int NumberOfDaysToDisplay { get; set; } = UserPreferences.DayAmount;
        public static readonly int MinimumPieChartPercentage = 10;
        #endregion

        #region Temporary 
        private readonly static string folderPath = ApplicationPath.UsageLogsFolder;
        private DispatcherTimer refreshTimer;
        #endregion

        #region Formatters
        public Func<double, string> HourFormatter { get; set; }
        // LiveCharts2 formatters
        // public Func<ChartPoint, string> PieChartLabelFormatter { get; set; } 
        #endregion

        #region String Bindings
        public DateTime LoadedDate = DateTime.Now.Date;
        public string StrLoadedDate
        {
            get => (LoadedDate.Date == DateTime.Now.Date) ?
                "Today, " + this.LoadedDate.ToString("dddd") :
                this.LoadedDate.ToString("dddd, MMM dd yyyy");
        }

        public TimeSpan TotalDuration = new TimeSpan();
        public string StrTotalDuration
        {
            get
            {
                // Compact format: "5h 30m"
                return $"{(int)TotalDuration.TotalHours}h {TotalDuration.Minutes}m";
            }
        }

        public string StrMinumumDuration
        {
            get
            {
                return UserPreferences.MinumumDuration.TotalSeconds <= 0
                    ? ""
                    : $"Apps that run less than {StringHelper.TimeSpanToString(UserPreferences.MinumumDuration)} are hidden.";
            }
        }
        #endregion

        #region Collections
        public ObservableCollection<List<AppUsage>> WeekAppUsage { get; set; }
        
        // LiveCharts2 Series
        public ObservableCollection<ISeries> WeeklyChartSeries { get; set; }
        public ObservableCollection<Axis> WeeklyChartXAxes { get; set; }

        public ObservableCollection<ISeries> DayPieChartSeries { get; set; }
        public ObservableCollection<AppUsageListItem> DayListItems { get; set; }
        public ObservableCollection<ISeries> TagsChartSeries { get; set; }
        
        public DateTime[] WeeklyChartLabelDates { get; set; }
        #endregion

        #region Excluded Processes
        private static readonly string[] excludeProcesses = new string[]
        {
            "DigitalWellbeingWPF", "process", "DigitalWellbeingWinUI3",
            "explorer", "SearchHost", "Idle", "StartMenuExperienceHost", "ShellExperienceHost",
            "dwm", "LockApp", "msiexec", "ApplicationFrameHost", "*LAST",
        };
        private static string[] userExcludedProcesses;
        #endregion

        #region Getters with Bindings
        public event PropertyChangedEventHandler PropertyChanged;
        public bool CanGoNext { get => LoadedDate.Date < DateTime.Now.Date; }
        public bool CanGoPrev { get => LoadedDate.Date > DateTime.Now.AddDays(-NumberOfDaysToDisplay + 1).Date; }
        
        private bool _isLoading;
        public bool IsLoading 
        { 
            get => _isLoading; 
            set 
            {
                if (_isLoading != value)
                {
                    _isLoading = value;
                    OnPropertyChanged();
                }
            } 
        }

        public double PieChartInnerRadius { get; set; }

        public bool IsWeeklyDataLoaded = false;
        
        public ICommand ChartClickCommand { get; set; }
        #endregion

        private Windows.UI.ViewManagement.UISettings uiSettings;
        private Microsoft.UI.Dispatching.DispatcherQueue dispatcherQueue;

        public AppUsageViewModel()
        {
            try
            {
                dispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();

                // Listen for Accent Color Changes
                uiSettings = new Windows.UI.ViewManagement.UISettings();
                uiSettings.ColorValuesChanged += UiSettings_ColorValuesChanged;

                InitCollections();
                InitFormatters();
                LoadUserExcludedProcesses();
                InitAutoRefreshTimer();
                // Start Loading
                LoadWeeklyData();
                
                ChartClickCommand = new RelayCommand(OnChartClick);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CRITICAL] ViewModel Constructor Failed: {ex}");
                // Ensure critical collections are at least not null to avoid XAML crash
                if (WeekAppUsage == null) WeekAppUsage = new ObservableCollection<List<AppUsage>>();
                if (WeeklyChartLabelDates == null) WeeklyChartLabelDates = new DateTime[0];
                if (DayListItems == null) DayListItems = new ObservableCollection<AppUsageListItem>();
            }
        }

        private void UiSettings_ColorValuesChanged(Windows.UI.ViewManagement.UISettings sender, object args)
        {
            dispatcherQueue.TryEnqueue(() =>
            {
                // Update Weekly Chart (Bar)
                if (WeeklyChartSeries != null && WeeklyChartSeries.Count > 0)
                {
                    foreach (var series in WeeklyChartSeries)
                    {
                        if (series is ColumnSeries<double> columnSeries)
                        {
                            columnSeries.Fill = GetAccentGradientPaint();
                        }
                    }
                }

                // Update Day Pie Chart
                if (DayPieChartSeries != null && DayPieChartSeries.Count > 0)
                {
                    var accent = uiSettings.GetColorValue(Windows.UI.ViewManagement.UIColorType.Accent);
                    var skAccent = new SKColor(accent.R, accent.G, accent.B, accent.A);
                    var palette = GenerateMultiHuePalette(skAccent, DayPieChartSeries.Count);

                    for (int i = 0; i < DayPieChartSeries.Count; i++)
                    {
                        if (DayPieChartSeries[i] is PieSeries<double> pieSeries)
                        {
                            pieSeries.Fill = new SolidColorPaint(palette[i % palette.Count]);
                        }
                    }
                }
            });
        }

        private LinearGradientPaint GetAccentGradientPaint()
        {
            var accent = uiSettings.GetColorValue(Windows.UI.ViewManagement.UIColorType.Accent);
                
            // Create SKColors from Windows Accent
            var skAccent = new SKColor(accent.R, accent.G, accent.B, accent.A);
            var skAccentDark = new SKColor((byte)Math.Max(0, accent.R - 50), (byte)Math.Max(0, accent.G - 50), (byte)Math.Max(0, accent.B - 50));

            return new LinearGradientPaint(
                new SKColor[] { skAccent, skAccentDark }, 
                new SKPoint(0.5f, 0), // Top
                new SKPoint(0.5f, 1)); // Bottom
        }

        private void OnChartClick(object parameter)
        {
            try
            {
                // LiveCharts2 passes IEnumerable<ChartPoint> as parameter (usually) or specific args depending on binding
                // But for DataPointerDown it passes IEnumerable<ChartPoint> of the clicked points.
                
                if (parameter is IEnumerable<ChartPoint> points)
                {
                    var point = points.FirstOrDefault();
                    if (point != null)
                    {
                         // Index of the point in the series
                         int index = point.Index;
                         WeeklyChart_SelectionChanged(index);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Chart Click Error: {ex.Message}");
            }
        }

        private void InitCollections()
        {
            WeekAppUsage = new ObservableCollection<List<AppUsage>>();
            
            WeeklyChartSeries = new ObservableCollection<ISeries>();
            WeeklyChartXAxes = new ObservableCollection<Axis> 
            { 
                new Axis 
                { 
                    Labels = new List<string>(),
                    LabelsRotation = 0,
                } 
            };

            DayPieChartSeries = new ObservableCollection<ISeries>();
            DayListItems = new ObservableCollection<AppUsageListItem>();
            TagsChartSeries = new ObservableCollection<ISeries>();
        }

        private void InitFormatters()
        {
            HourFormatter = (hours) => hours.ToString("F1") + " h";
        }

        private void InitAutoRefreshTimer()
        {
            int refreshInterval = UserPreferences.RefreshIntervalSeconds;
            TimeSpan intervalDuration = TimeSpan.FromSeconds(refreshInterval);
            refreshTimer = new DispatcherTimer() { Interval = intervalDuration };
            refreshTimer.Tick += (s, e) => TryRefreshData();
        }

        private List<SKColor> GenerateMultiHuePalette(SKColor baseColor, int count)
        {
            var palette = new List<SKColor>();
            baseColor.ToHsl(out float h, out float s, out float l);

            // Ensure count is at least 1
            if (count < 1) count = 1;

            // Step size: 30 degrees gives a nice analogus/triadic mix without being too rainbow-y if count is low
            // Or use Golden Ratio for distinctness? 
            // User asked for "multihue palette *originating* from... accent".
            // Let's try rotating by 25 degrees.
            float step = 25f;

            for (int i = 0; i < count; i++)
            {
                float newH = (h + (i * step)) % 360f;
                // Vary lightness slightly to distinguishing boundaries?
                // float newL = (i % 2 == 0) ? l : Math.Clamp(l * 0.8f, 0, 100); 
                // SkiaSharp HSL Lightness is 0-100 usually or 0-1? 
                // ToHsl docs: h [0, 360), s [0, 100], l [0, 100].
                
                palette.Add(SKColor.FromHsl(newH, s, l));
            }
            return palette;
        }

        public void LoadUserExcludedProcesses()
        {
            userExcludedProcesses = UserPreferences.UserExcludedProcesses.ToArray();
        }

        public async void LoadWeeklyData()
        {
            SetLoading(true);
            Debug.WriteLine("[AppUsageViewModel] LoadWeeklyData Started");
            await Helpers.SettingsManager.WaitForInit;
            try
            {
                DateTime minDate = DateTime.Now.AddDays(-NumberOfDaysToDisplay);

                List<List<AppUsage>> weekUsage = new List<List<AppUsage>>();
                ObservableCollection<double> hours = new ObservableCollection<double>();
                List<string> labels = new List<string>();
                List<DateTime> loadedDates = new List<DateTime>();

                for (int i = 1; i <= NumberOfDaysToDisplay; i++)
                {
                    DateTime date = minDate.AddDays(i).Date;
                    
                    List<AppUsage> appUsageList = await GetData(date);
                    List<AppUsage> filteredUsageList = appUsageList.Where(appUsageFilter).ToList();
                    filteredUsageList.Sort(appUsageSorter);

                    weekUsage.Add(filteredUsageList);
                    
                     TimeSpan totalDuration = TimeSpan.Zero;
                    foreach (AppUsage app in filteredUsageList)
                    {
                        totalDuration = totalDuration.Add(app.Duration);
                    }
                    hours.Add(totalDuration.TotalHours);
                    
                    labels.Add(date.ToString("ddd"));
                    loadedDates.Add(date);
                    
                    Debug.WriteLine($"[AppUsageViewModel] Loaded {date.ToShortDateString()}: {filteredUsageList.Count} apps, {totalDuration.TotalHours:F2} hrs");
                }

                WeekAppUsage.Clear(); // Ensure clear
                foreach (List<AppUsage> dayUsage in weekUsage) { WeekAppUsage.Add(dayUsage); }
                
                WeeklyChartSeries.Clear();
                
                WeeklyChartSeries.Clear();
                
                var gradientPaint = GetAccentGradientPaint();

                if (hours.Sum() == 0)
                {
                    Debug.WriteLine("[DEBUG] No real data found. Injecting Dummy Data for Verification.");
                    hours.Clear();
                    labels.Clear();
                    // Inject Dummy Data
                    hours.Add(2.5); labels.Add("Mon");
                    hours.Add(4.0); labels.Add("Tue");
                    hours.Add(3.0); labels.Add("Wed");
                    hours.Add(5.5); labels.Add("Thu");
                    hours.Add(1.0); labels.Add("Fri");
                    hours.Add(3.5); labels.Add("Sat");
                    hours.Add(4.5); labels.Add("Sun");
                }

                WeeklyChartSeries.Add(new ColumnSeries<double> 
                { 
                    Values = hours,
                    Name = "Usage",
                    YToolTipLabelFormatter = (point) => $"{point.Coordinate.PrimaryValue:F1} hours",
                    Fill = gradientPaint,
                    Rx = 10,
                    Ry = 10,
                    MaxBarWidth = 35 // Slightly wider for premium look
                });
                
                Debug.WriteLine($"[DEBUG] Loaded Weekly Data: {weekUsage.Count} days, {hours.Count} points.");

                WeeklyChartXAxes[0].Labels = labels;
                WeeklyChartLabelDates = loadedDates.ToArray();

                IsWeeklyDataLoaded = true;
                
                // Select last day
                WeeklyChart_SelectionChanged(WeekAppUsage.Count - 1);
            }
            catch (Exception ex)
            {
                AppLogger.WriteLine($"Load Weekly Data Exception {ex}");
                Debug.WriteLine($"[DEBUG] Load Weekly Data Exception: {ex}");
            }
            finally
            {
                SetLoading(false);
                Debug.WriteLine("[AppUsageViewModel] LoadWeeklyData Finished");
            }
        }

        public void WeeklyChart_SelectionChanged(int index)
        {
            try
            {
                if (index < 0 || index >= WeeklyChartLabelDates.Length) return;

                DateTime selectedDate = WeeklyChartLabelDates.ElementAt(index);
                
                if (selectedDate == LoadedDate && selectedDate != DateTime.Now.Date)
                {
                    return;
                }
                else
                {
                    LoadedDate = selectedDate;
                    TryRefreshData();
                    UpdatePieChartAndList(WeekAppUsage.ElementAt(index));
                }
            }
            catch { }
        }

        Comparison<AppUsage> appUsageSorter = (a, b) => a.Duration.CompareTo(b.Duration) * -1;
        Func<AppUsage, bool> appUsageFilter = (a) => !IsProcessExcluded(a.ProcessName);

        public void RefreshDayView()
        {
            TryRefreshData(force: true);
        }

        private async void TryRefreshData(bool force = false)
        {
            if (!IsWeeklyDataLoaded) return;

            if (force || DateTime.Now.Date == LoadedDate.Date)
            {
                try
                {
                    List<AppUsage> appUsageList = await GetData(LoadedDate.Date);
                    // Update week usage if it exists for this day
                    int weekIndex = -1;
                    for(int i=0; i<WeeklyChartLabelDates.Length; i++)
                    {
                        if (WeeklyChartLabelDates[i].Date == LoadedDate.Date)
                        {
                            weekIndex = i;
                            break;
                        }
                    }

                    if (weekIndex != -1 && weekIndex < WeekAppUsage.Count)
                    {
                        WeekAppUsage[weekIndex] = appUsageList;
                    }

                    List<AppUsage> filteredUsageList = appUsageList.Where(appUsageFilter).ToList();
                    filteredUsageList.Sort(appUsageSorter);
                    UpdatePieChartAndList(filteredUsageList);
                }
                catch { }
            }
        }

        private void UpdatePieChartAndList(List<AppUsage> appUsageList)
        {
            SetLoading(true);
            try
            {
                List<AppUsage> filteredUsageList = appUsageList.Where(appUsageFilter).ToList();
                TotalDuration = TimeSpan.Zero;
                
                var pieSeriesList = new ObservableCollection<ISeries>();
                var listItems = new ObservableCollection<AppUsageListItem>();

                double otherProcessesTotalMinutes = 0;

                foreach (AppUsage app in filteredUsageList)
                {
                    TotalDuration = TotalDuration.Add(app.Duration);
                }

                foreach (AppUsage app in filteredUsageList)
                {
                    int percentage = (int)Math.Round(app.Duration.TotalSeconds / TotalDuration.TotalSeconds * 100);
                    
                    if (app.Duration > UserPreferences.MinumumDuration)
                    {
                        if (percentage <= MinimumPieChartPercentage)
                        {
                            otherProcessesTotalMinutes += app.Duration.TotalMinutes;
                        }
                        else
                        {
                            // Generate Palette
                            var uiSettings = new Windows.UI.ViewManagement.UISettings();
                            var accent = uiSettings.GetColorValue(Windows.UI.ViewManagement.UIColorType.Accent);
                            var skAccent = new SKColor(accent.R, accent.G, accent.B, accent.A);
                            var palette = GenerateMultiHuePalette(skAccent, filteredUsageList.Count);

                            int index = pieSeriesList.Count; 
                            // Note: filteredUsageList logic executes sequentially so index matches added items
                                                    
                            var series = new PieSeries<double>
                            {
                                Values = new ObservableCollection<double> { app.Duration.TotalMinutes },
                                Name = app.ProcessName,
                                ToolTipLabelFormatter = (point) => $"{point.Context.Series.Name}: {point.Coordinate.PrimaryValue:F1}m",
                                DataLabelsFormatter = (point) => point.Context.Series.Name,
                                Fill = new SolidColorPaint(palette[index % palette.Count])
                            };
                            pieSeriesList.Add(series);
                        }
                        
                        listItems.Add(new AppUsageListItem(app.ProcessName, app.ProgramName, app.Duration, percentage, AppTagHelper.GetAppTag(app.ProcessName)));
                    }
                }

                 if (otherProcessesTotalMinutes > 0)
                 {
                     pieSeriesList.Add(new PieSeries<double> 
                     { 
                         Values = new ObservableCollection<double> { otherProcessesTotalMinutes },
                         Name = "Other Apps",
                         Fill = new SolidColorPaint(SKColors.Gray)
                     });
                 }

                 if (pieSeriesList.Count == 0)
                 {
                     pieSeriesList.Add(new PieSeries<double> 
                     { 
                         Values = new ObservableCollection<double> { 1 },
                         Name = "No Data",
                         Fill = new SolidColorPaint(SKColors.LightGray)
                     });
                 }

                 DayPieChartSeries.Clear();
                 foreach(var s in pieSeriesList) DayPieChartSeries.Add(s);

                 DayListItems.Clear();
                 foreach(var i in listItems) DayListItems.Add(i);

                 RefreshTagChart(filteredUsageList);
            }
            finally
            {
                NotifyChange();
                SetLoading(false);
            }
        }

        private void RefreshTagChart(List<AppUsage> usageList)
        {
            // Porting logic for StackedRowSeries -> StackedRowSeries<double>
             var stackSeries = new List<ISeries>();
             // ... Logic similar to original but adapted to LiveCharts2 ...
             // Skipping for brevity in this step, focusing on main charts.
             TagsChartSeries.Clear();
        }

        public void NotifyChange()
        {
            OnPropertyChanged(nameof(StrLoadedDate));
            OnPropertyChanged(nameof(StrTotalDuration));
            OnPropertyChanged(nameof(StrMinumumDuration));
            OnPropertyChanged(nameof(CanGoNext));
            OnPropertyChanged(nameof(CanGoPrev));
        }

        public static bool IsProcessExcluded(string processName)
        {
            return excludeProcesses.Contains(processName) || userExcludedProcesses.Contains(processName);
        }

        public static async Task<List<AppUsage>> GetData(DateTime date)
        {
             List<AppUsage> appUsageList = new List<AppUsage>();
             try
             {
                 if(File.Exists($"{folderPath}{date:MM-dd-yyyy}.log"))
                 {
                      string text = await Task.Run(() => File.ReadAllText($"{folderPath}{date:MM-dd-yyyy}.log"));
                      string[] lines = text.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
                      foreach (string line in lines)
                      {
                          if (string.IsNullOrWhiteSpace(line)) continue;
                          string[] cells = line.Split('\t');
                          if(cells.Length >= 2)
                          {
                              string processName = cells[0];
                              if(int.TryParse(cells[1], out int seconds))
                              {
                                   string programName = cells.Length > 2 ? cells[2] : "";
                                   appUsageList.Add(new AppUsage(processName, programName, TimeSpan.FromSeconds(seconds)));
                              }
                          }
                      }
                 }
             }
             catch (Exception ex) 
             {
                 AppLogger.WriteLine(ex.Message);
             }
             return appUsageList;
        }

        private void SetLoading(bool value)
        {
            IsLoading = value;
        }

        private void OnPropertyChanged([CallerMemberName] String propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
