using UsageLogger.Core.Models;
using UsageLogger.Helpers;
using UsageLogger.Models;
using UsageLogger.Core;
using UsageLogger.Core.Data;
using UsageLogger.Core.Helpers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Diagnostics;
using Windows.UI;

namespace UsageLogger.ViewModels
{
    public enum ChartViewMode
    {
        Categories = 0,
        Apps = 1,
        SubApps = 2
    }

    public enum DateRangeOption
    {
        LastWeek = 0,
        LastMonth = 1,
        LastYear = 2,
        Custom = 3
    }

    public class HistoryViewModel : INotifyPropertyChanged
    {
        // View mode options for ComboBox binding
        public ObservableCollection<string> ViewModeOptions { get; } = new ObservableCollection<string>();
        
        // Date range preset options for ComboBox binding
        public ObservableCollection<string> DateRangeOptions { get; } = new ObservableCollection<string>();
        
        private int _selectedDateRangeIndex = 0; // Default to Last Week
        public int SelectedDateRangeIndex
        {
            get => _selectedDateRangeIndex;
            set 
            { 
                if (_selectedDateRangeIndex != value) 
                { 
                    _selectedDateRangeIndex = value; 
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsCustomDateRange));
                    ApplyDateRange();
                } 
            }
        }
        
        public bool IsCustomDateRange => _selectedDateRangeIndex == (int)DateRangeOption.Custom;
        
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

        private int _selectedViewModeIndex = 0;
        public int SelectedViewModeIndex
        {
            get => _selectedViewModeIndex;
            set { if (_selectedViewModeIndex != value) { _selectedViewModeIndex = value; OnPropertyChanged(); GenerateChart(); } }
        }

        public ChartViewMode CurrentViewMode => (ChartViewMode)_selectedViewModeIndex;



        private ObservableCollection<TreemapItem> _treemapData;
        public ObservableCollection<TreemapItem> TreemapData
        {
            get => _treemapData;
            set { if (_treemapData != value) { _treemapData = value; OnPropertyChanged(); } }
        }

        // HeatMap Data
        private ObservableCollection<HeatmapDataPoint> _heatMapData;
        public ObservableCollection<HeatmapDataPoint> HeatMapData
        {
            get => _heatMapData;
            set { if (_heatMapData != value) { _heatMapData = value; OnPropertyChanged(); } }
        }

        // Heatmap cell details for tooltips and navigation
        private Dictionary<(int day, int hour), HeatmapCellData> _heatmapCellDetails = new();
        
        // Trend chart data
        private ObservableCollection<BarChartItem> _trendData;
        public ObservableCollection<BarChartItem> TrendData
        {
            get => _trendData;
            set { if (_trendData != value) { _trendData = value; OnPropertyChanged(); } }
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
        
        // AFK Time for the date range
        private string _afkTimeText;
        public string AfkTimeText
        {
            get => _afkTimeText;
            set { if (_afkTimeText != value) { _afkTimeText = value; OnPropertyChanged(); } }
        }
        
        // Previous period average line value (for trend chart)
        private double _previousPeriodAverage;
        public double PreviousPeriodAverage
        {
            get => _previousPeriodAverage;
            set { if (_previousPeriodAverage != value) { _previousPeriodAverage = value; OnPropertyChanged(); } }
        }

        private string _prevPeriodAvgText = "0h 0m";
        public string PrevPeriodAvgText
        {
            get => _prevPeriodAvgText;
            set { if (_prevPeriodAvgText != value) { _prevPeriodAvgText = value; OnPropertyChanged(); } }
        }
        
        // Navigation event for heatmap cell click
        public event Action<DateTime> NavigateToDate;

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set { if (_isLoading != value) { _isLoading = value; OnPropertyChanged(); } }
        }

        public RelayCommand GenerateChartCommand { get; }

        // Cached sessions for search
        private List<AppSession> _cachedSessions = new();
        private List<AppSession> _cachedPrevSessions = new();

        private string _searchResultText;
        public string SearchResultText
        {
            get => _searchResultText;
            set { if (_searchResultText != value) { _searchResultText = value; OnPropertyChanged(); } }
        }

        // Overlay data for trend chart (search highlight per day)
        private ObservableCollection<BarChartItem> _searchOverlayData;
        public ObservableCollection<BarChartItem> SearchOverlayData
        {
            get => _searchOverlayData;
            set { if (_searchOverlayData != value) { _searchOverlayData = value; OnPropertyChanged(); } }
        }

        // Top app for the selected period
        private string _topAppText;
        public string TopAppText
        {
            get => _topAppText;
            set { if (_topAppText != value) { _topAppText = value; OnPropertyChanged(); } }
        }

        private string _topAppComparisonText;
        public string TopAppComparisonText
        {
            get => _topAppComparisonText;
            set { if (_topAppComparisonText != value) { _topAppComparisonText = value; OnPropertyChanged(); } }
        }

        private void ComputeTopApp()
        {
            if (_cachedSessions.Count == 0)
            {
                TopAppText = null;
                TopAppComparisonText = null;
                return;
            }

            var topGroup = _cachedSessions
                .Where(s => !AppUsageViewModel.IsProcessExcluded(s.ProcessName))
                .GroupBy(s => s.ProcessName)
                .OrderByDescending(g => g.Sum(s => s.Duration.TotalMinutes))
                .FirstOrDefault();

            if (topGroup == null) { TopAppText = null; TopAppComparisonText = null; return; }

            string displayName = UserPreferences.GetDisplayName(topGroup.Key);
            double currentMinutes = topGroup.Sum(s => s.Duration.TotalMinutes);
            string durationStr = StringHelper.FormatDurationCompact(TimeSpan.FromMinutes(currentMinutes));
            TopAppText = $"{displayName}: {durationStr}";

            // Comparison with previous period
            double prevMinutes = _cachedPrevSessions
                .Where(s => s.ProcessName.Equals(topGroup.Key, StringComparison.OrdinalIgnoreCase))
                .Sum(s => s.Duration.TotalMinutes);

            TopAppComparisonText = FormatComparison(currentMinutes, prevMinutes);
        }

        private string FormatComparison(double currentMinutes, double prevMinutes)
        {
            if (prevMinutes <= 0)
                return currentMinutes > 0 ? "🆕 " + LocalizationHelper.GetString("History_SearchNewThisPeriod") : null;

            double changePercent = ((currentMinutes - prevMinutes) / prevMinutes) * 100;
            string arrow = changePercent >= 0 ? "↑" : "↓";
            string prevDur = StringHelper.FormatDurationCompact(TimeSpan.FromMinutes(prevMinutes));
            return $"{arrow} {Math.Abs(changePercent):F0}% (prev: {prevDur})";
        }

        public List<string> GetSearchSuggestions(string query)
        {
            if (string.IsNullOrWhiteSpace(query) || _cachedSessions.Count == 0)
                return new List<string>();

            string q = query.Trim();
            var rules = UserPreferences.CustomTitleRules;
            var minDuration = UserPreferences.MinimumDuration;

            // Build a dictionary of label -> total duration
            var labelDurations = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

            foreach (var s in _cachedSessions)
            {
                if (AppUsageViewModel.IsProcessExcluded(s.ProcessName)) continue;

                string displayName = UserPreferences.GetDisplayName(s.ProcessName);
                if (!labelDurations.ContainsKey(displayName)) labelDurations[displayName] = 0;
                labelDurations[displayName] += s.Duration.TotalSeconds;

                if (!string.IsNullOrEmpty(s.ProgramName) && s.ProgramName != s.ProcessName)
                {
                    string subAppLabel = s.ProgramName;
                    if (rules != null && rules.Count > 0)
                        subAppLabel = WindowTitleParser.Parse(s.ProcessName, s.ProgramName, rules);
                        
                    if (subAppLabel != displayName)
                    {
                        if (!labelDurations.ContainsKey(subAppLabel)) labelDurations[subAppLabel] = 0;
                        labelDurations[subAppLabel] += s.Duration.TotalSeconds;
                    }
                }
            }

            // Filter: matches query AND exceeds minimum duration
            return labelDurations
                .Where(kvp => kvp.Key.Contains(q, StringComparison.OrdinalIgnoreCase) && kvp.Value >= minDuration.TotalSeconds)
                .OrderBy(kvp => kvp.Key)
                .Select(kvp => kvp.Key)
                .ToList();
        }

        private string GetSessionLabel(AppSession s)
        {
            string displayName = UserPreferences.GetDisplayName(s.ProcessName);
            if (!string.IsNullOrEmpty(s.ProgramName) && s.ProgramName != s.ProcessName)
            {
                // Apply custom title rules
                var rules = UserPreferences.CustomTitleRules;
                if (rules != null && rules.Count > 0)
                    return WindowTitleParser.Parse(s.ProcessName, s.ProgramName, rules);
                return s.ProgramName;
            }
            return displayName;
        }

        public void SearchApp(string query, bool exactMatch = false)
        {
            if (string.IsNullOrWhiteSpace(query) || _cachedSessions.Count == 0)
            {
                SearchResultText = null;
                SearchOverlayData = null;
                return;
            }

            string q = query.Trim();
            var rules = UserPreferences.CustomTitleRules;

            // Generate pairs of (Label, Session) for all valid sessions
            var allLabelSessions = _cachedSessions
                .Where(s => !AppUsageViewModel.IsProcessExcluded(s.ProcessName))
                .SelectMany(s => {
                    var labels = new List<string>();
                    string displayName = UserPreferences.GetDisplayName(s.ProcessName);
                    labels.Add(displayName);

                    if (!string.IsNullOrEmpty(s.ProgramName) && s.ProgramName != s.ProcessName)
                    {
                        string subAppLabel = s.ProgramName;
                        if (rules != null && rules.Count > 0)
                            subAppLabel = WindowTitleParser.Parse(s.ProcessName, s.ProgramName, rules);
                        
                        if (subAppLabel != displayName) 
                            labels.Add(subAppLabel);
                    }
                    return labels.Select(l => new { Label = l, Session = s });
                });

            // Filter by query
            var matchingGroups = allLabelSessions
                .Where(x => exactMatch 
                    ? x.Label.Equals(q, StringComparison.OrdinalIgnoreCase) 
                    : x.Label.Contains(q, StringComparison.OrdinalIgnoreCase))
                .GroupBy(x => x.Label)
                .ToList();

            if (matchingGroups.Count == 0)
            {
                SearchResultText = LocalizationHelper.GetString("History_SearchNoResults");
                SearchOverlayData = null;
                return;
            }

            // Pick top group
            var topGroup = matchingGroups
                .OrderByDescending(g => g.Sum(x => x.Session.Duration.TotalMinutes))
                .First();

            string bestMatch = topGroup.Key;
            var topSessions = topGroup.Select(x => x.Session).ToList();
            double totalMinutes = topSessions.Sum(s => s.Duration.TotalMinutes);
            string durationStr = StringHelper.FormatDurationCompact(TimeSpan.FromMinutes(totalMinutes));

            // Previous period comparison scoped to the same label
            var prevFiltered = _cachedPrevSessions
                .Where(s => !AppUsageViewModel.IsProcessExcluded(s.ProcessName))
                .SelectMany(s => {
                    var labels = new List<string>();
                    string displayName = UserPreferences.GetDisplayName(s.ProcessName);
                    labels.Add(displayName);
                    if (!string.IsNullOrEmpty(s.ProgramName) && s.ProgramName != s.ProcessName)
                    {
                        string subAppLabel = s.ProgramName;
                        if (rules != null && rules.Count > 0)
                            subAppLabel = WindowTitleParser.Parse(s.ProcessName, s.ProgramName, rules);
                        if (subAppLabel != displayName) labels.Add(subAppLabel);
                    }
                    return labels.Select(l => new { Label = l, Session = s });
                });

            var prevGroup = prevFiltered
                .Where(x => x.Label.Equals(bestMatch, StringComparison.OrdinalIgnoreCase))
                .GroupBy(x => x.Label)
                .FirstOrDefault();

            double prevMinutes = prevGroup?.Sum(x => x.Session.Duration.TotalMinutes) ?? 0;
            string comparison = FormatComparison(totalMinutes, prevMinutes);

            SearchResultText = $"{bestMatch}: {durationStr}";
            if (!string.IsNullOrEmpty(comparison))
                SearchResultText += $"  {comparison}";

            // Build per-day overlay for trend chart
            // Get the category color for the best-matching process
            string overlayProcessName = topSessions.First().ProcessName;
            var tag = AppTagHelper.GetAppTag(overlayProcessName);
            var customTag = UserPreferences.CustomTags.FirstOrDefault(t => t.Id == (int)tag);
            Color overlayColor = customTag != null
                ? ColorHelper.GetColorFromHex(customTag.HexColor)
                : Color.FromArgb(255, 100, 200, 255); // Default blue if untagged

            var overlay = new ObservableCollection<BarChartItem>();
            var dailyGroups = topSessions
                .GroupBy(s => s.StartTime.Date)
                .ToDictionary(g => g.Key, g => g.Sum(s => s.Duration.TotalMinutes));

            for (var d = StartDate.Date; d <= EndDate.Date; d = d.AddDays(1))
            {
                double minutes = dailyGroups.ContainsKey(d) ? dailyGroups[d] : 0;
                overlay.Add(new BarChartItem
                {
                    Date = d,
                    Value = minutes / 60.0, // Hours to match main chart
                    Color = overlayColor
                });
            }
            SearchOverlayData = overlay;
        }

        public HistoryViewModel()
        {
            GenerateChartCommand = new RelayCommand(_ => GenerateChart());
            TrendData = new ObservableCollection<BarChartItem>();
            HeatMapData = new ObservableCollection<HeatmapDataPoint>();
            
            // Localize View Modes
            ViewModeOptions.Add(LocalizationHelper.GetString("History_View_Categories"));
            ViewModeOptions.Add(LocalizationHelper.GetString("History_View_Apps"));
            ViewModeOptions.Add(LocalizationHelper.GetString("History_View_SubApps"));

            // Localize Date Ranges
            DateRangeOptions.Add(LocalizationHelper.GetString("History_Period_LastWeek"));
            DateRangeOptions.Add(LocalizationHelper.GetString("History_Period_LastMonth"));
            DateRangeOptions.Add(LocalizationHelper.GetString("History_Period_LastYear"));
            DateRangeOptions.Add(LocalizationHelper.GetString("History_Period_Custom"));

            // Apply initial date range (Last Week) without triggering chart generation yet
            ApplyDateRangeWithoutGenerate();
        }

        private void ApplyDateRange()
        {
            ApplyDateRangeWithoutGenerate();
            GenerateChart();
        }
        
        private void ApplyDateRangeWithoutGenerate()
        {
            switch ((DateRangeOption)_selectedDateRangeIndex)
            {
                case DateRangeOption.LastWeek:
                    SetLastWeekDates();
                    break;
                case DateRangeOption.LastMonth:
                    SetLastMonthDates();
                    break;
                case DateRangeOption.LastYear:
                    SetLastYearDates();
                    break;
                case DateRangeOption.Custom:
                    // Keep current dates, user will set them manually
                    break;
            }
        }

        private void SetLastWeekDates()
        {
            // Get the previous week (Monday to Sunday)
            DateTime today = DateTime.Now.Date;
            int daysFromMonday = ((int)today.DayOfWeek + 6) % 7; // Days since Monday (0 = Mon, 6 = Sun)
            DateTime thisMonday = today.AddDays(-daysFromMonday);
            DateTime lastSunday = thisMonday.AddDays(-1);
            DateTime lastMonday = lastSunday.AddDays(-6);
            
            StartDate = lastMonday;
            EndDate = lastSunday;
        }

        private void SetLastMonthDates()
        {
            // Get the previous month (1st to last day)
            DateTime today = DateTime.Now.Date;
            DateTime firstOfCurrentMonth = new DateTime(today.Year, today.Month, 1);
            DateTime lastOfPreviousMonth = firstOfCurrentMonth.AddDays(-1);
            DateTime firstOfPreviousMonth = new DateTime(lastOfPreviousMonth.Year, lastOfPreviousMonth.Month, 1);
            
            StartDate = firstOfPreviousMonth;
            EndDate = lastOfPreviousMonth;
        }

        private void SetLastYearDates()
        {
            // Get the previous year (Jan 1 to Dec 31)
            int previousYear = DateTime.Now.Year - 1;
            StartDate = new DateTime(previousYear, 1, 1);
            EndDate = new DateTime(previousYear, 12, 31);
        }

        private string _errorMessage;
        public string ErrorMessage
        {
            get => _errorMessage;
            set 
            { 
                if (_errorMessage != value) 
                { 
                    _errorMessage = value; 
                    OnPropertyChanged(); 
                    OnPropertyChanged(nameof(HasError));
                } 
            }
        }

        public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

        public async void GenerateChart()
        {
            if (EndDate < StartDate) 
            {
                ErrorMessage = "End Date cannot be before Start Date.";
                return;
            }

            IsLoading = true;
            ErrorMessage = null;
            
            Debug.WriteLine($"[HistoryViewModel] Generating Chart for {StartDate.Date.ToShortDateString()} - {EndDate.Date.ToShortDateString()}");
            try
            {
                // Clear previous data
                TreemapData = null;
                TrendData = null;
                HeatMapData = null;
                TotalHoursText = "Loading...";
                TotalChangeText = "";

                // Load current period
                List<AppSession> allSessions = await LoadSessionsForDateRange(StartDate.Date, EndDate.Date);
                _cachedSessions = allSessions;
                SearchResultText = null;
                SearchOverlayData = null;
                Debug.WriteLine($"[HistoryViewModel] Loaded {allSessions.Count} sessions.");

                if (allSessions.Count == 0)
                {
                    TotalHoursText = "0h 0m 0s";
                    TotalChangeText = "No data found for this period.";
                    ErrorMessage = "No activity logs found for the selected date range.";
                    TopAppText = null;
                    TopAppComparisonText = null;
                    return;
                }

                // Load previous period for comparison
                int dayCount = (EndDate.Date - StartDate.Date).Days + 1;
                DateTime prevStart = StartDate.Date.AddDays(-dayCount);
                DateTime prevEnd = StartDate.Date.AddDays(-1);
                List<AppSession> prevSessions = await LoadSessionsForDateRange(prevStart, prevEnd);
                _cachedPrevSessions = prevSessions;

                // Compute top app for the card
                ComputeTopApp();

                // Generate Heatmap with cell details
                GenerateHeatMap(allSessions);

                // Generate Trend Chart (day-by-day bars with previous period average line)
                GenerateTrendChart(allSessions, prevSessions, StartDate.Date, EndDate.Date);

                // Aggregate for Pie Charts
                List<AppUsage> aggregatedUsage = AggregateSessions(allSessions);

                switch (CurrentViewMode)
                {
                    case ChartViewMode.Categories:
                        GenerateTagChart(aggregatedUsage);
                        break;
                    case ChartViewMode.Apps:
                        GenerateAppChart(aggregatedUsage);
                        break;
                    case ChartViewMode.SubApps:
                        GenerateSubAppChart(aggregatedUsage);
                        break;
                }

                if (TreemapData == null || TreemapData.Count == 0)
                {
                    ErrorMessage = "No data available for the selected view.";
                }
            }
            catch (Exception ex) 
            {
                Debug.WriteLine($"[HistoryViewModel] Generation Error: {ex}");
                ErrorMessage = $"Failed to load data: {ex.Message}";
                TotalHoursText = "Error";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task<List<AppSession>> LoadSessionsForDateRange(DateTime start, DateTime end)
        {
            return await SessionAggregator.LoadSessionsForDateRangeAsync(start, end);
        }

        private List<AppUsage> AggregateSessions(List<AppSession> sessions)
        {
            return SessionAggregator.AggregateSessions(sessions);
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
                    // Remap DayOfWeek: .NET uses 0=Sunday. We want 0=Monday, 6=Sunday.
                    int day = ((int)t.DayOfWeek + 6) % 7; 
                    int hour = t.Hour;
                    
                    DateTime slotEnd = t.Date.AddHours(hour + 1);
                    DateTime actualEnd = (s.EndTime < slotEnd) ? s.EndTime : slotEnd;
                    
                    double minutes = (actualEnd - t).TotalMinutes;
                    if (minutes > 0)
                    {
                        grid[day, hour] += minutes;
                        
                        // Determine the label for this session based on the selected ViewMode
                        string name = CurrentViewMode switch
                        {
                            ChartViewMode.Categories => AppTagHelper.GetTagDisplayName(
                                // Use sub-app tag if available and explicitly tagged, else parent tag
                                s.ProgramName != s.ProcessName && !string.IsNullOrEmpty(s.ProgramName)
                                    ? AppTagHelper.GetTitleTag(s.ProcessName, s.ProgramName) is AppTag t2 && t2 != AppTag.Untagged
                                        ? AppTagHelper.GetTitleTag(s.ProcessName, s.ProgramName)
                                        : AppTagHelper.GetAppTag(s.ProcessName)
                                    : AppTagHelper.GetAppTag(s.ProcessName)),

                            ChartViewMode.SubApps =>
                                // Show sub-app name when available, otherwise fall back to app display name
                                (s.ProgramName != s.ProcessName && !string.IsNullOrEmpty(s.ProgramName))
                                    ? s.ProgramName
                                    : UserPreferences.GetDisplayName(s.ProcessName),

                            _ => // Apps (default)
                                UserPreferences.GetDisplayName(s.ProcessName)
                        };

                        // Track label breakdown per cell
                        var apps = cellApps[(day, hour)];
                        if (apps.ContainsKey(name)) apps[name] += minutes;
                        else apps[name] = minutes;
                    }
                    
                    t = actualEnd;
                    if (t >= s.EndTime) break;
                }
            }

            // Store cell details for tooltips
            _heatmapCellDetails.Clear();
            string[] dayNames = { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday" };

            // Label for tooltip changes based on view mode
            string topLabel = CurrentViewMode switch
            {
                ChartViewMode.Categories => "Top Category",
                ChartViewMode.SubApps    => "Top Sub-App",
                _                        => "Top App"
            };
            
            var heatmapPoints = new ObservableCollection<HeatmapDataPoint>();
            var uiSettings = new Windows.UI.ViewManagement.UISettings();
            var accent = uiSettings.GetColorValue(Windows.UI.ViewManagement.UIColorType.Accent);

            for (int d = 0; d < 7; d++)
            {
                for (int h = 0; h < 24; h++)
                {
                    double val = grid[d, h];
                    
                    // Get top entry for this cell (respects ViewMode via the label computed above)
                    var topApp = cellApps[(d, h)].OrderByDescending(x => x.Value).FirstOrDefault();
                    string topAppName = topApp.Key ?? "No activity";
                    double topAppMinutes = topApp.Value;

                    string timeStr = $"{h:D2}:00";
                    string totalStr = StringHelper.FormatDurationCompact(TimeSpan.FromMinutes(val));
                    string tooltip = $"{dayNames[d]} at {timeStr} • {totalStr} • {topLabel}: {topAppName}";

                    heatmapPoints.Add(new HeatmapDataPoint
                    {
                        HourOne = h,
                        DayOfWeek = d,
                        Intensity = val,
                        Color = accent,
                        Tooltip = tooltip
                    });
                    
                    _heatmapCellDetails[(d, h)] = new HeatmapCellData
                    {
                        DayName = dayNames[d],
                        Hour = h,
                        TotalMinutes = val,
                        TopAppName = topAppName,
                        TopAppMinutes = topAppMinutes
                    };
                }
            }
            
            HeatMapData = heatmapPoints;
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
            PreviousPeriodAverage = prevAvg / 60.0;
            PrevPeriodAvgText = StringHelper.FormatDurationCompact(TimeSpan.FromMinutes(prevAvg));

            // Calculate KPIs
            double currentTotal = dailyTotals.Values.Sum();
            TotalHoursText = StringHelper.FormatDurationFull(TimeSpan.FromMinutes(currentTotal));
            
            // Calculate AFK time (Away + LogonUI)
            double afkMinutes = currentSessions
                .Where(s => s.ProcessName.Equals("Away", StringComparison.OrdinalIgnoreCase) || 
                           s.ProcessName.Equals("LogonUI", StringComparison.OrdinalIgnoreCase))
                .Sum(s => s.Duration.TotalMinutes);
            AfkTimeText = afkMinutes > 0 ? StringHelper.FormatDurationCompact(TimeSpan.FromMinutes(afkMinutes)) : "0m";            
            if (prevTotal > 0)
            {
                double changePercent = ((currentTotal - prevTotal) / prevTotal) * 100;
                TotalChangePositive = changePercent >= 0;
                string arrow = TotalChangePositive ? "↑" : "↓";
                TotalChangeText = $"{arrow} {Math.Abs(changePercent):F0}% {string.Format(LocalizationHelper.GetString("History_VsPreviousDays"), prevDays)}";
            }
            else
            {
                TotalChangeText = LocalizationHelper.GetString("History_NoPreviousData");
                TotalChangePositive = true;
            }

            // Create bar chart
            // Create bar chart items
            var trendItems = new ObservableCollection<BarChartItem>();
            var uiSettings = new Windows.UI.ViewManagement.UISettings();
            var accent = uiSettings.GetColorValue(Windows.UI.ViewManagement.UIColorType.Accent);
            
            foreach(var kvp in dailyTotals.OrderBy(x => x.Key))
            {
                double hours = kvp.Value / 60.0;
                string label = kvp.Key.ToString("MM/dd");
                
                // Enhanced tooltip: Include date for context when labels are hidden
                string tooltipDate = kvp.Key.ToString("ddd, MMM d");
                string tooltipDuration = ChartFactory.FormatHours(hours);
                
                trendItems.Add(new BarChartItem
                {
                    Value = hours,
                    Label = label,
                    Date = kvp.Key,
                    Tooltip = $"{tooltipDate} • {tooltipDuration}",
                    Color = accent
                });
            }

            TrendData = trendItems;
            // Axis labels are handled by Win2DBarChart via BarChartItem.Label
        }
        
        // Method for heatmap cell click -> navigate to that day
        public void OnHeatmapCellClicked(int dayOfWeekMapping, int hour)
        {
            // dayOfWeekMapping: 0=Monday, 6=Sunday
            int targetDayOfWeek = (dayOfWeekMapping + 1) % 7; // Convert back to 0=Sunday for .NET DayOfWeek

            // Find the actual date for this day of week within the selected range
            for (var d = StartDate.Date; d <= EndDate.Date; d = d.AddDays(1))
            {
                if ((int)d.DayOfWeek == targetDayOfWeek)
                {
                    NavigateToDate?.Invoke(d);
                    return;
                }
            }
        }

        private void GenerateTagChart(List<AppUsage> usage)
        {
            TreemapData = HistoryChartGenerator.GenerateTagChart(usage);
        }

        private void GenerateAppChart(List<AppUsage> usage)
        {
            TreemapData = HistoryChartGenerator.GenerateAppChart(usage);
        }

        private void GenerateSubAppChart(List<AppUsage> usage)
        {
            TreemapData = HistoryChartGenerator.GenerateSubAppChart(usage);
        }







        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
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
