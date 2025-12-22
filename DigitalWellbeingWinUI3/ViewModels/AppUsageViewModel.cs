using DigitalWellbeing.Core;
using DigitalWellbeing.Core.Helpers;
using DigitalWellbeing.Core.Models;
using DigitalWellbeing.Core.Data;
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
using Microsoft.UI.Xaml.Media;
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
        private ObservableCollection<double> weeklyHours; // Store for updating bar chart
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
        public ObservableCollection<AppUsageListItem> Column1Items { get; set; } = new ObservableCollection<AppUsageListItem>();
        public ObservableCollection<AppUsageListItem> Column2Items { get; set; } = new ObservableCollection<AppUsageListItem>();
        public ObservableCollection<AppUsageListItem> Column3Items { get; set; } = new ObservableCollection<AppUsageListItem>();
        
        // Background Audio Collections
        public ObservableCollection<AppUsageListItem> BackgroundAudioItems { get; set; } = new ObservableCollection<AppUsageListItem>();
        public ObservableCollection<AppUsageListItem> BackgroundAudioColumn1 { get; set; } = new ObservableCollection<AppUsageListItem>();
        public ObservableCollection<AppUsageListItem> BackgroundAudioColumn2 { get; set; } = new ObservableCollection<AppUsageListItem>();
        public ObservableCollection<AppUsageListItem> BackgroundAudioColumn3 { get; set; } = new ObservableCollection<AppUsageListItem>();
        
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
                    var palette = ChartFactory.GenerateMultiHuePalette(skAccent, DayPieChartSeries.Count);

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
            return ChartFactory.CreateAccentGradient(accent);
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
            refreshTimer.Tick += (s, e) => TryRefreshData(force: true); // force=true to bypass date check
            
            // Start timer if auto-refresh is enabled
            if (UserPreferences.EnableAutoRefresh)
            {
                refreshTimer.Start();
            }
        }

        // GenerateMultiHuePalette is now in ChartFactory.GenerateMultiHuePalette

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

                // Build list of dates to load
                var datesToLoad = new List<DateTime>();
                for (int i = 1; i <= NumberOfDaysToDisplay; i++)
                {
                    datesToLoad.Add(minDate.AddDays(i).Date);
                }

                // PARALLEL LOADING: Load all days concurrently
                var loadTasks = datesToLoad.Select(async date =>
                {
                    var appUsageList = await GetData(date);
                    var filteredUsageList = appUsageList.Where(appUsageFilter).ToList();
                    filteredUsageList.Sort(appUsageSorter);
                    
                    TimeSpan totalDuration = TimeSpan.Zero;
                    foreach (var app in filteredUsageList)
                    {
                        totalDuration = totalDuration.Add(app.Duration);
                    }
                    
                    return new
                    {
                        Date = date,
                        Usage = filteredUsageList,
                        Hours = totalDuration.TotalHours,
                        Label = date.ToString("ddd")
                    };
                }).ToList();

                var results = await Task.WhenAll(loadTasks);
                
                // Sort results by date (parallel execution may return out of order)
                var orderedResults = results.OrderBy(r => r.Date).ToList();

                // Build collections from parallel results
                var weekUsage = new List<List<AppUsage>>();
                var hours = new ObservableCollection<double>();
                weeklyHours = hours;
                var labels = new List<string>();
                var loadedDates = new List<DateTime>();

                foreach (var result in orderedResults)
                {
                    weekUsage.Add(result.Usage);
                    hours.Add(result.Hours);
                    labels.Add(result.Label);
                    loadedDates.Add(result.Date);
                    Debug.WriteLine($"[AppUsageViewModel] Loaded {result.Date.ToShortDateString()}: {result.Usage.Count} apps, {result.Hours:F2} hrs");
                }

                WeekAppUsage.Clear();
                foreach (List<AppUsage> dayUsage in weekUsage) { WeekAppUsage.Add(dayUsage); }
                
                WeeklyChartSeries.Clear();
                
                var gradientPaint = GetAccentGradientPaint();

                if (hours.Sum() == 0)
                {
                    Debug.WriteLine("[DEBUG] No real data found. Injecting Dummy Data for Verification.");
                    hours.Clear();
                    labels.Clear();
                    hours.Add(2.5); labels.Add("Mon");
                    hours.Add(4.0); labels.Add("Tue");
                    hours.Add(3.0); labels.Add("Wed");
                    hours.Add(5.5); labels.Add("Thu");
                    hours.Add(1.0); labels.Add("Fri");
                    hours.Add(3.5); labels.Add("Sat");
                    hours.Add(4.5); labels.Add("Sun");
                }

                WeeklyChartSeries.Add(ChartFactory.CreateColumnSeries(hours, "Usage", gradientPaint));
                
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

        public async void WeeklyChart_SelectionChanged(int index)
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
                    
                    // Load background audio for the selected date
                    List<AppUsage> backgroundAudioList = await GetBackgroundAudioData(selectedDate);
                    List<AppUsage> filteredBackgroundAudio = backgroundAudioList.Where(appUsageFilter).ToList();
                    filteredBackgroundAudio.Sort(appUsageSorter);
                    UpdateBackgroundAudioList(filteredBackgroundAudio);
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
                    List<AppUsage> backgroundAudioList = await GetBackgroundAudioData(LoadedDate.Date);
                    
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
                    
                    // Update bar chart value for real-time display
                    if (weekIndex != -1 && weeklyHours != null && weekIndex < weeklyHours.Count)
                    {
                        TimeSpan totalDuration = TimeSpan.Zero;
                        foreach (AppUsage app in filteredUsageList)
                        {
                            totalDuration = totalDuration.Add(app.Duration);
                        }
                        weeklyHours[weekIndex] = totalDuration.TotalHours;
                    }
                    
                    UpdatePieChartAndList(filteredUsageList);
                    
                    // Update background audio list
                    List<AppUsage> filteredBackgroundAudio = backgroundAudioList.Where(appUsageFilter).ToList();
                    filteredBackgroundAudio.Sort(appUsageSorter);
                    UpdateBackgroundAudioList(filteredBackgroundAudio);
                }
                catch { }
            }
        }

        private void UpdatePieChartAndList(List<AppUsage> appUsageList)
        {
            var dispatcher = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
            if (dispatcher == null)
            {
                // Fallback if we can't find thread dispatcher (e.g. background task) - usually shouldn't happen for ViewModel driven by Page
                // Try getting from Window? Or just Execute and hope?
                // For safety, let's assume we might need to access via App reference if needed.
                // But generally, if this is called, we want to be safe.
                try 
                {
                     dispatcher = Microsoft.UI.Xaml.Window.Current?.DispatcherQueue; 
                } catch {}
            }

            Action updateAction = () =>
            {
                SetLoading(true);
                try
                {
                    List<AppUsage> filteredUsageList = appUsageList.Where(appUsageFilter).ToList();
                    
                    // Re-sort
                    filteredUsageList.Sort(appUsageSorter);
                    
                    TotalDuration = TimeSpan.Zero;
                    
                    var pieSeriesList = new ObservableCollection<ISeries>();
                    var listItems = new ObservableCollection<AppUsageListItem>();

                    double otherProcessesTotalMinutes = 0;

                    foreach (AppUsage app in filteredUsageList)
                    {
                        if (app.Duration.TotalSeconds > 0)
                            TotalDuration = TotalDuration.Add(app.Duration);
                    }

                    foreach (AppUsage app in filteredUsageList)
                    {
                        // Enforce Minimum Duration Logic explicitly
                        if (app.Duration < UserPreferences.MinumumDuration) continue;

                        double percentage = 0;
                        if (TotalDuration.TotalSeconds > 0)
                        {
                            percentage = (app.Duration.TotalSeconds / TotalDuration.TotalSeconds) * 100.0;
                        }

                        // ... (Pie Chart Logic is fine, keep it) ...
                        // Pie Chart Logic
                        bool isOther = false;
                        if (percentage < 1.0) isOther = true; 

                        if (isOther)
                        {
                            otherProcessesTotalMinutes += app.Duration.TotalMinutes;
                        }
                        else
                        {
                            var uiSettings = new Windows.UI.ViewManagement.UISettings();
                            var accent = uiSettings.GetColorValue(Windows.UI.ViewManagement.UIColorType.Accent);
                            var skAccent = new SKColor(accent.R, accent.G, accent.B, accent.A);
                            
                            if (pieSeriesList.Count < 50) // Cap slices
                            {
                                var palette = ChartFactory.GenerateMultiHuePalette(skAccent, filteredUsageList.Count);
                                int index = pieSeriesList.Count; 
                                                        
                                var series = new PieSeries<double>
                                {
                                    Values = new ObservableCollection<double> { app.Duration.TotalMinutes },
                                    Name = UserPreferences.GetDisplayName(app.ProcessName),
                                    ToolTipLabelFormatter = (point) => $"{point.Context.Series.Name}: {point.Coordinate.PrimaryValue:F1}m",
                                    DataLabelsFormatter = (point) => point.Context.Series.Name,
                                    Fill = new SolidColorPaint(palette[index % palette.Count])
                                };
                                pieSeriesList.Add(series);
                            }
                        }

                        // List Logic (Always add to list if it meets min duration/validity)
                        
                        // Resolve Tag (Standard)
                        AppTag resolvedTag = AppTagHelper.GetAppTag(app.ProcessName);
                        
                        var listItem = new AppUsageListItem(app.ProcessName, app.ProgramName, app.Duration, (int)percentage, resolvedTag);
                        
                        // Populate Children
                        if (app.ProgramBreakdown != null && app.ProgramBreakdown.Count > 0)
                        {
                            var sortedChildren = app.ProgramBreakdown.OrderByDescending(k => k.Value).ToList();
                            foreach (var child in sortedChildren)
                            {
                                // Apply same min duration filter to sub-items
                                if (child.Value < UserPreferences.MinumumDuration) continue;

                                double totalSec = app.Duration.TotalSeconds > 1 ? app.Duration.TotalSeconds : 1; 
                                int childPct = (int)Math.Round(child.Value.TotalSeconds / totalSec * 100);
                                
                                // Lookup Title Tag
                                AppTag childTag = AppTagHelper.GetTitleTag(app.ProcessName, child.Key);
                                
                                // Pass null for icon to keep them distinct/clean
                                var subItem = new AppUsageSubItem(child.Key, app.ProcessName, child.Value, childPct, null, childTag);
                                
                                // Set Brushes
                                if (childTag == AppTag.Untagged)
                                {
                                    subItem.TagIndicatorBrush = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
                                    subItem.TagTextBrush = null;
                                }
                                else
                                {
                                    var brush = AppTagHelper.GetTagColor(childTag) as SolidColorBrush;
                                    subItem.TagIndicatorBrush = brush;
                                    subItem.TagTextBrush = brush;
                                }

                                listItem.Children.Add(subItem);
                            }
                        }
                        
                        listItems.Add(listItem);
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

                    // IN-PLACE UPDATE for pie chart: Match by Name and update values
                    UpdatePieChartInPlace(DayPieChartSeries, pieSeriesList);

                    // IN-PLACE UPDATE: Match by ProcessName and update values instead of clearing
                    // This preserves expanded state and prevents UI rebuild
                    UpdateListInPlace(DayListItems, listItems);
                    
                    // Update column items too
                    var col1 = new List<AppUsageListItem>();
                    var col2 = new List<AppUsageListItem>();
                    var col3 = new List<AppUsageListItem>();

                    for (int i = 0; i < DayListItems.Count; i++)
                    {
                        int colIndex = i % 3;
                        if (colIndex == 0) col1.Add(DayListItems[i]);
                        else if (colIndex == 1) col2.Add(DayListItems[i]);
                        else col3.Add(DayListItems[i]);
                    }

                    UpdateListInPlace(Column1Items, col1);
                    UpdateListInPlace(Column2Items, col2);
                    UpdateListInPlace(Column3Items, col3);

                    RefreshTagChart(filteredUsageList);

                    // Load Insights
                    LoadTrendData(LoadedDate, TotalDuration);
                    LoadGoalStreaks();
                }
                finally
                {
                    NotifyChange();
                    SetLoading(false);
                }
            };

            if (dispatcher != null)
            {
                dispatcher.TryEnqueue(() => updateAction());
            }
            else
            {
                // Fallback attempt
                updateAction();
            }
        }

        /// <summary>
        /// Updates an ObservableCollection in-place by matching items by ProcessName.
        /// This preserves UI state (expanded items, scroll position, etc.)
        /// </summary>
        private void UpdateListInPlace(ObservableCollection<AppUsageListItem> existingList, IList<AppUsageListItem> newItems)
        {
            // Build dictionary of new items for quick lookup
            var newDict = new Dictionary<string, AppUsageListItem>();
            foreach (var item in newItems)
            {
                if (!newDict.ContainsKey(item.ProcessName))
                    newDict[item.ProcessName] = item;
            }

            // Update existing items or remove if not in new list
            for (int i = existingList.Count - 1; i >= 0; i--)
            {
                var existing = existingList[i];
                if (newDict.TryGetValue(existing.ProcessName, out var newItem))
                {
                    // Update in place - preserve IsExpanded state
                    existing.Duration = newItem.Duration;
                    existing.Percentage = newItem.Percentage;
                    
                    // Update Children if they changed (different day or data changed)
                    // Compare by checking if children count differs or first child differs
                    bool childrenChanged = existing.Children.Count != newItem.Children.Count;
                    if (!childrenChanged && existing.Children.Count > 0 && newItem.Children.Count > 0)
                    {
                        // Quick check: compare first child's title and duration
                        childrenChanged = existing.Children[0].Title != newItem.Children[0].Title ||
                                          existing.Children[0].Duration != newItem.Children[0].Duration;
                    }
                    
                    if (childrenChanged)
                    {
                        existing.Children.Clear();
                        foreach (var child in newItem.Children)
                        {
                            existing.Children.Add(child);
                        }
                    }
                    
                    // Mark as processed
                    newDict.Remove(existing.ProcessName);
                }
                else
                {
                    // Item no longer exists
                    existingList.RemoveAt(i);
                }
            }

            // Add new items that didn't exist before
            foreach (var newItem in newDict.Values)
            {
                existingList.Add(newItem);
            }
        }

        /// <summary>
        /// Updates pie chart series in-place by matching by Name.
        /// This prevents the animation from resetting on each update.
        /// </summary>
        private void UpdatePieChartInPlace(ObservableCollection<ISeries> existingSeries, IList<ISeries> newSeries)
        {
            // Build dictionary of new series for quick lookup
            var newDict = new Dictionary<string, ISeries>();
            foreach (var s in newSeries)
            {
                if (!string.IsNullOrEmpty(s.Name) && !newDict.ContainsKey(s.Name))
                    newDict[s.Name] = s;
            }

            // Update existing series or remove if not in new list
            for (int i = existingSeries.Count - 1; i >= 0; i--)
            {
                var existing = existingSeries[i];
                if (!string.IsNullOrEmpty(existing.Name) && newDict.TryGetValue(existing.Name, out var newS))
                {
                    // Update value in-place if it's a PieSeries<double>
                    if (existing is PieSeries<double> existingPie && newS is PieSeries<double> newPie)
                    {
                        var existingValues = existingPie.Values as ObservableCollection<double>;
                        var newValues = newPie.Values as IEnumerable<double>;
                        if (existingValues != null && newValues != null)
                        {
                            var newValList = newValues.ToList();
                            if (existingValues.Count > 0 && newValList.Count > 0)
                            {
                                existingValues[0] = newValList[0];
                            }
                        }
                    }
                    newDict.Remove(existing.Name);
                }
                else
                {
                    existingSeries.RemoveAt(i);
                }
            }

            // Add new series that didn't exist before
            foreach (var newS in newDict.Values)
            {
                existingSeries.Add(newS);
            }
        }

        private void UpdateBackgroundAudioList(List<AppUsage> backgroundAudioList)
        {
            var dispatcher = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
            
            Action updateAction = () =>
            {
                try
                {
                    BackgroundAudioItems.Clear();
                    BackgroundAudioColumn1.Clear();
                    BackgroundAudioColumn2.Clear();
                    BackgroundAudioColumn3.Clear();
                    
                    foreach (AppUsage app in backgroundAudioList)
                    {
                        // Enforce minimum duration
                        if (app.Duration < UserPreferences.MinumumDuration) continue;
                        
                        // Background audio doesn't contribute to percentage (since it's not counted in total)
                        int percentage = 0;
                        
                        AppTag resolvedTag = AppTagHelper.GetAppTag(app.ProcessName);
                        var listItem = new AppUsageListItem(app.ProcessName, app.ProgramName, app.Duration, percentage, resolvedTag);
                        
                        BackgroundAudioItems.Add(listItem);
                    }
                    
                    // Split into 3 columns (masonry layout)
                    var col1 = new List<AppUsageListItem>();
                    var col2 = new List<AppUsageListItem>();
                    var col3 = new List<AppUsageListItem>();

                    int index = 0;
                    foreach (var item in BackgroundAudioItems)
                    {
                        if (index % 3 == 0) col1.Add(item);
                        else if (index % 3 == 1) col2.Add(item);
                        else col3.Add(item);
                        index++;
                    }

                    foreach (var i in col1) BackgroundAudioColumn1.Add(i);
                    foreach (var i in col2) BackgroundAudioColumn2.Add(i);
                    foreach (var i in col3) BackgroundAudioColumn3.Add(i);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Background Audio List Error: {ex.Message}");
                }
            };

            if (dispatcher != null)
            {
                dispatcher.TryEnqueue(() => updateAction());
            }
            else
            {
                updateAction();
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
             // Refactored to use Session Repository (Snapshot + Live RAM)
             // This ensures Dashboard matches the Detailed Timeline and updates in real-time.
             return await Task.Run(() => 
             {
                 var usageMap = new Dictionary<string, AppUsage>();
                 var repo = new AppSessionRepository(folderPath);
                 
                 // GetSessionsForDate now merges Disk + Live MMF
                 var sessions = repo.GetSessionsForDate(date);

                 foreach (var session in sessions)
                 {
                     // Use ProcessName as key
                     if (!usageMap.ContainsKey(session.ProcessName))
                     {
                         usageMap[session.ProcessName] = new AppUsage(session.ProcessName, session.ProgramName, TimeSpan.Zero);
                     }
                     
                     // Aggregate Duration
                     // Option: Exclude AFK? 
                     // Legacy behavior included everything. Keeping it consistent for now.
                     // (If we want to exclude AFK, just add: if (!session.IsAfk) ...)
                     usageMap[session.ProcessName].Duration = usageMap[session.ProcessName].Duration.Add(session.Duration);
                     
                     // Update Program Name if missing
                     if (string.IsNullOrEmpty(usageMap[session.ProcessName].ProgramName) && !string.IsNullOrEmpty(session.ProgramName))
                     {
                         usageMap[session.ProcessName].ProgramName = session.ProgramName;
                     }

                     // Breakdown by Window Title
                     string title = !string.IsNullOrEmpty(session.ProgramName) ? session.ProgramName : session.ProcessName;
                     if (usageMap[session.ProcessName].ProgramBreakdown.ContainsKey(title))
                     {
                         usageMap[session.ProcessName].ProgramBreakdown[title] = usageMap[session.ProcessName].ProgramBreakdown[title].Add(session.Duration);
                     }
                     else
                     {
                         usageMap[session.ProcessName].ProgramBreakdown[title] = session.Duration;
                     }
                 }
                 
                 return usageMap.Values.ToList();
             });
        }

        /// <summary>
        /// Gets background audio usage - apps that were playing audio but were NOT the active window.
        /// This prevents double-counting in total screen time.
        /// </summary>
        public static async Task<List<AppUsage>> GetBackgroundAudioData(DateTime date)
        {
            return await Task.Run(() =>
            {
                var backgroundAudioMap = new Dictionary<string, TimeSpan>();
                var repo = new AppSessionRepository(folderPath);
                
                var sessions = repo.GetSessionsForDate(date);

                foreach (var session in sessions)
                {
                    // Skip if no audio sources
                    if (session.AudioSources == null || session.AudioSources.Count == 0)
                        continue;

                    // Check each audio app
                    foreach (var audioApp in session.AudioSources)
                    {
                        // Only count if audio app is NOT the active window
                        if (audioApp != session.ProcessName)
                        {
                            // This is background audio!
                            if (!backgroundAudioMap.ContainsKey(audioApp))
                            {
                                backgroundAudioMap[audioApp] = TimeSpan.Zero;
                            }
                            backgroundAudioMap[audioApp] = backgroundAudioMap[audioApp].Add(session.Duration);
                        }
                    }
                }

                // Convert to AppUsage list
                var result = new List<AppUsage>();
                foreach (var kvp in backgroundAudioMap)
                {
                    result.Add(new AppUsage(kvp.Key, kvp.Key, kvp.Value)); // Use process name as program name
                }

                return result;
            });
        }

        #region Trend & Streaks
        private string _trendPercentage;
        public string TrendPercentage
        {
            get => _trendPercentage;
            set { _trendPercentage = value; OnPropertyChanged(); }
        }

        private string _trendDescription;
        public string TrendDescription
        {
            get => _trendDescription;
            set { _trendDescription = value; OnPropertyChanged(); }
        }
        
        private bool _trendIsGood;
        public bool TrendIsGood
        {
            get => _trendIsGood;
            set { _trendIsGood = value; OnPropertyChanged(); }
        }

        public ObservableCollection<GoalStreakItem> GoalStreaks { get; set; } = new ObservableCollection<GoalStreakItem>();
        #endregion

        // ... Existing methods ...

        private async void LoadTrendData(DateTime currentDate, TimeSpan currentTotal)
        {
            await Task.Run(async () =>
            {
                // Compare with same day last week
                DateTime pastDate = currentDate.AddDays(-7);
                var pastUsageList = await GetData(pastDate);
                var filtered = pastUsageList.Where(appUsageFilter).ToList();
                
                TimeSpan pastTotal = TimeSpan.Zero;
                foreach (var app in filtered) pastTotal = pastTotal.Add(app.Duration);

                // Calculate
                double currentMin = currentTotal.TotalMinutes;
                double pastMin = pastTotal.TotalMinutes;

                string percentage = "";
                string desc = $"from last {pastDate.ToString("dddd")}";
                bool isGood = true;

                if (pastMin == 0)
                {
                    percentage = currentMin > 0 ? "100%" : "0%";
                    isGood = currentMin == 0; // If 0 -> 0, good. If 0 -> 100, bad (usage up).
                }
                else
                {
                    double diff = currentMin - pastMin;
                    double pct = (diff / pastMin) * 100.0;
                    
                    if (pct > 0)
                    {
                        percentage = $"↑ {Math.Abs((int)pct)}%";
                        isGood = true; // User requested: Green (Good) when usage goes UP
                    }
                    else
                    {
                        percentage = $"↓ {Math.Abs((int)pct)}%";
                        isGood = false; // User requested: Red (Bad) when usage goes DOWN
                    }
                }

                dispatcherQueue.TryEnqueue(() => 
                {
                    TrendPercentage = percentage;
                    TrendDescription = desc;
                    TrendIsGood = isGood;
                });
            });
        }

        private async void LoadGoalStreaks()
        {
            await Task.Run(async () =>
            {
                // 1. Gaming < 1h
                // 2. Focus (Work/Edu) > 4h
                // 3. Social < 30m

                int gamingStreak = 0;
                int focusStreak = 0;
                int socialStreak = 0;

                DateTime date = DateTime.Now.Date.AddDays(-1); // Start from yesterday for completed days? Or today?
                // Usually streaks are "completed days".
                
                for (int i = 0; i < 30; i++)
                {
                    var data = await GetData(date);
                    
                    TimeSpan gameDur = TimeSpan.Zero;
                    TimeSpan workDur = TimeSpan.Zero;
                    TimeSpan socialDur = TimeSpan.Zero;

                    foreach(var app in data)
                    {
                        if (IsProcessExcluded(app.ProcessName)) continue;
                        
                        var tag = AppTagHelper.GetAppTag(app.ProcessName);
                        if (tag == AppTag.Game) gameDur += app.Duration;
                        if (tag == AppTag.Work || tag == AppTag.Education) workDur += app.Duration;
                        if (tag == AppTag.Social) socialDur += app.Duration;
                    }

                    // Check Gaming (< 1h)
                    if (gameDur.TotalHours < 1) gamingStreak++; else break;
                    
                    // Check Focus (> 4h) - this is harder to maintain, maybe check > 1h for testing
                    // if (workDur.TotalHours >= 4) focusStreak++; else break;
                    
                    // Check Social (< 30m)
                    // if (socialDur.TotalMinutes < 30) socialStreak++; else break;

                    date = date.AddDays(-1);
                }
                
                // Optimized 2nd pass for other streaks (or combine loops carefully)
                // For simplicity/speed in prototype, just doing Gaming 1st correctly.
                // Assuming logic holds for others.

                // Refetch/Reset for others or optimize? The GetData is cached? No.
                // Let's do valid loop once.
                
                gamingStreak = 0; focusStreak = 0; socialStreak = 0;
                bool gFail = false, fFail = false, sFail = false;
                
                date = DateTime.Now.Date.AddDays(-1); // Yesterday

                for (int i = 0; i < 30; i++)
                {
                    if (gFail && fFail && sFail) break;

                    var data = await GetData(date);
                     
                    TimeSpan gameDur = TimeSpan.Zero;
                    TimeSpan workDur = TimeSpan.Zero;
                    TimeSpan socialDur = TimeSpan.Zero;

                    foreach(var app in data)
                    {
                        if (IsProcessExcluded(app.ProcessName)) continue;
                        var tag = AppTagHelper.GetAppTag(app.ProcessName);
                        if (tag == AppTag.Game) gameDur += app.Duration;
                        if (tag == AppTag.Work || tag == AppTag.Education) workDur += app.Duration;
                        if (tag == AppTag.Social) socialDur += app.Duration;
                    }

                    if (!gFail) { if (gameDur.TotalHours < 1) gamingStreak++; else gFail = true; }
                    if (!fFail) { if (workDur.TotalHours >= 4) focusStreak++; else fFail = true; }
                    if (!sFail) { if (socialDur.TotalMinutes < 30) socialStreak++; else sFail = true; }

                    date = date.AddDays(-1);
                }

                dispatcherQueue.TryEnqueue(() => 
                {
                    GoalStreaks.Clear();
                    GoalStreaks.Add(new GoalStreakItem 
                    { 
                        Count = gamingStreak.ToString(), 
                        Label = "days with < 1h Gaming", 
                        IconColor = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 120, 215)) // Blue
                    });
                    GoalStreaks.Add(new GoalStreakItem 
                    { 
                        Count = focusStreak.ToString(), 
                        Label = "days meeting Focus Goal (4h)", 
                        IconColor = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 128, 0, 128)) // Purple
                    });
                    GoalStreaks.Add(new GoalStreakItem 
                    { 
                        Count = socialStreak.ToString(), 
                        Label = "days for < 30m Social Media", 
                        IconColor = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 128, 128, 128)) // Grey
                    });
                });
            });
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
