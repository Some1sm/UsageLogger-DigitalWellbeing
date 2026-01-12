using DigitalWellbeing.Core;
using DigitalWellbeing.Core.Data;
using DigitalWellbeing.Core.Helpers;
using DigitalWellbeing.Core.Models;
using DigitalWellbeingWinUI3.Helpers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace DigitalWellbeingWinUI3.ViewModels
{
    /// <summary>
    /// ViewModel for the Miscellaneous Data ("Fun Facts") page.
    /// Computes various statistics from session data over a selected date range.
    /// </summary>
    public class MiscDataViewModel : INotifyPropertyChanged
    {
        private readonly AppSessionRepository _repository;

        // Date Range Options
        public enum DateRangeOption
        {
            Today,
            Yesterday,
            Last7Days,
            ThisMonth,
            LastMonth,
            AllTime,
            Custom
        }

        public MiscDataViewModel()
        {
            _repository = new AppSessionRepository(ApplicationPath.UsageLogsFolder);
            Stats = new ObservableCollection<StatItem>();
            DateRangeOptions = Enum.GetValues(typeof(DateRangeOption)).Cast<DateRangeOption>().ToList();
            
            // Create user-friendly strings
            DateRangeDisplayOptions = DateRangeOptions.Select(o => 
            {
                switch (o)
                {
                    case DateRangeOption.Last7Days: return "Last 7 Days";
                    case DateRangeOption.ThisMonth: return "This Month";
                    case DateRangeOption.LastMonth: return "Last Month";
                    case DateRangeOption.AllTime: return "All Time";
                    default: return o.ToString();
                }
            }).ToList();

            SelectedDateRange = DateRangeOption.Last7Days; // Default
            
            RefreshCommand = new RelayCommand(async _ => await LoadDataAsync());
        }

        public ObservableCollection<StatItem> Stats { get; }
        public List<DateRangeOption> DateRangeOptions { get; }
        public List<string> DateRangeDisplayOptions { get; }
        public System.Windows.Input.ICommand RefreshCommand { get; }

        public int SelectedDateRangeIndex
        {
            get => DateRangeOptions.IndexOf(SelectedDateRange);
            set
            {
                if (value >= 0 && value < DateRangeOptions.Count)
                {
                    SelectedDateRange = DateRangeOptions[value];
                    OnPropertyChanged();
                }
            }
        }

        private DateRangeOption _selectedDateRange;
        public DateRangeOption SelectedDateRange
        {
            get => _selectedDateRange;
            set
            {
                if (_selectedDateRange != value)
                {
                    _selectedDateRange = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(SelectedDateRangeIndex)); // Sync index
                    UpdateDateRangeFromOption();
                    OnPropertyChanged(nameof(IsCustomDateRange));
                    _ = LoadDataAsync();
                }
            }
        }

        public bool IsCustomDateRange => SelectedDateRange == DateRangeOption.Custom;

        private DateTimeOffset _startDate = DateTime.Today.AddDays(-30);
        public DateTimeOffset StartDate
        {
            get => _startDate;
            set
            {
                if (_startDate != value)
                {
                    _startDate = value;
                    OnPropertyChanged();
                    if (IsCustomDateRange) _ = LoadDataAsync();
                }
            }
        }

        private DateTimeOffset _endDate = DateTime.Today;
        public DateTimeOffset EndDate
        {
            get => _endDate;
            set
            {
                if (_endDate != value)
                {
                    _endDate = value;
                    OnPropertyChanged();
                    if (IsCustomDateRange) _ = LoadDataAsync();
                }
            }
        }

        private void UpdateDateRangeFromOption()
        {
            var today = DateTime.Today;
            switch (SelectedDateRange)
            {
                case DateRangeOption.Today:
                    StartDate = today;
                    EndDate = today;
                    break;
                case DateRangeOption.Yesterday:
                    StartDate = today.AddDays(-1);
                    EndDate = today.AddDays(-1);
                    break;
                case DateRangeOption.Last7Days:
                    StartDate = today.AddDays(-6);
                    EndDate = today;
                    break;
                case DateRangeOption.ThisMonth:
                    StartDate = new DateTime(today.Year, today.Month, 1);
                    EndDate = today;
                    break;
                case DateRangeOption.LastMonth:
                    var lastMonth = today.AddMonths(-1);
                    StartDate = new DateTime(lastMonth.Year, lastMonth.Month, 1);
                    EndDate = new DateTime(lastMonth.Year, lastMonth.Month, DateTime.DaysInMonth(lastMonth.Year, lastMonth.Month));
                    break;
                case DateRangeOption.AllTime:
                    StartDate = new DateTime(2020, 1, 1); // Safe minimum for UI binding
                    EndDate = today;
                    break;
                case DateRangeOption.Custom:
                    // Keep existing values
                    break;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        private bool _isBusy = false;

        public async Task LoadDataAsync()
        {
            if (_isBusy) return;
            _isBusy = true;

            try
            {
                Stats.Clear();
                
                DateTime start = StartDate.Date;
                DateTime end = EndDate.Date;
                
                // Just in case
                if (SelectedDateRange == DateRangeOption.AllTime)
                {
                    // Ensure we use a safe minimum date consistent with UpdateDateRangeFromOption
                    if (start < new DateTime(2020, 1, 1))
                    {
                        start = new DateTime(2020, 1, 1);
                    }
                }

                var sessions = await LoadSessionsForDateRange(start, end);
                
                if (sessions == null || sessions.Count == 0)
                {
                    Stats.Add(new StatItem("\uE8B7", "No Data", LocalizationHelper.GetString("MiscData_NoData") ?? "No Data", LocalizationHelper.GetString("MiscData_NoDataDesc") ?? "No usage logs found."));
                    return;
                }

                var sorted = sessions.OrderBy(s => s.StartTime).ToList();

                // 1. Window Switches (non-AFK sessions count)
                int windowSwitches = sorted.Count(s => !s.IsAfk);
                Stats.Add(new StatItem("\uE8A5", windowSwitches.ToString("N0"), "Window Switches", "Total focus changes"));

                // 2. Audio Plays (Rising Edge)
                int audioPlays = 0;
                bool prevHadAudio = false;
                foreach (var s in sorted)
                {
                    bool currHasAudio = s.AudioSources != null && s.AudioSources.Count > 0;
                    if (currHasAudio && !prevHadAudio) audioPlays++;
                    prevHadAudio = currHasAudio;
                }
                Stats.Add(new StatItem("\uE995", audioPlays.ToString("N0"), "Audio Events", "Times audio started playing"));

                // Define AFK process names
                var afkProcesses = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Away", "LogonUI" };
                Func<AppSession, bool> isAfkSession = s => s.IsAfk || afkProcesses.Contains(s.ProcessName);

                // 3. AFK Time
                double afkSeconds = sorted.Where(s => s.ProcessName == "Away").Sum(s => s.Duration.TotalSeconds);
                var afkTime = TimeSpan.FromSeconds(afkSeconds);
                Stats.Add(new StatItem("\uE916", StringHelper.FormatDurationCompact(afkTime), LocalizationHelper.GetString("MiscData_AFKTime"), LocalizationHelper.GetString("MiscData_AFKTimeDesc")));

                // 3b. Lock Time
                double lockSeconds = sorted.Where(s => s.ProcessName == "LogonUI").Sum(s => s.Duration.TotalSeconds);
                var lockTime = TimeSpan.FromSeconds(lockSeconds);
                if (lockTime.TotalMinutes > 0)
                {
                    Stats.Add(new StatItem("\uE72E", StringHelper.FormatDurationCompact(lockTime), LocalizationHelper.GetString("MiscData_LockTime"), LocalizationHelper.GetString("MiscData_LockTimeDesc")));
                }

                // --- NEW: Power & Cost ---
                double totalAfkHours = (afkSeconds + lockSeconds) / 3600.0;
                if (totalAfkHours > 0)
                {
                    // Watts
                    int watts = UserPreferences.EstimatedPowerUsageWatts;
                    double kWh = totalAfkHours * watts / 1000.0;
                    
                    // Cost
                    double price = UserPreferences.KwhPrice;
                    double cost = kWh * price;
                    string currency = UserPreferences.CurrencySymbol;
                    
                    string energyLabel = LocalizationHelper.GetString("MiscData_WastedEnergy");
                    string energyDesc = string.Format(LocalizationHelper.GetString("MiscData_WastedEnergyDesc") ?? "Est. {0}W usage when AFK", watts);
                    string moneyLabel = LocalizationHelper.GetString("MiscData_WastedMoney");
                    string moneyDesc = LocalizationHelper.GetString("MiscData_WastedMoneyDesc");

                    Stats.Add(new StatItem("\uE945", $"{kWh:F2} kWh", energyLabel, energyDesc));
                    Stats.Add(new StatItem("\uE1CF", $"{currency}{cost:F2}", moneyLabel, moneyDesc));
                }
                // -------------------------

                // 4. Active Time
                var activeTime = TimeSpan.FromSeconds(sorted.Where(s => !isAfkSession(s)).Sum(s => s.Duration.TotalSeconds));
                Stats.Add(new StatItem("\uE770", StringHelper.FormatDurationCompact(activeTime), "Active Time", "Time actively using PC"));

                // 5. Longest Session
                var longest = sorted.Where(s => !s.IsAfk).OrderByDescending(s => s.Duration).FirstOrDefault();
                if (longest != null)
                {
                    string appName = string.IsNullOrEmpty(longest.ProgramName) ? longest.ProcessName : longest.ProgramName;
                    if (appName.Length > 20) appName = appName.Substring(0, 20) + "...";
                    Stats.Add(new StatItem("\uE768", StringHelper.FormatDurationCompact(longest.Duration), "Longest Session", appName));
                }

                // 6. Most Used App
                var topApp = sorted.Where(s => !isAfkSession(s))
                    .GroupBy(s => s.ProcessName)
                    .OrderByDescending(g => g.Sum(s => s.Duration.TotalSeconds))
                    .FirstOrDefault();
                if (topApp != null)
                {
                    var topDuration = TimeSpan.FromSeconds(topApp.Sum(s => s.Duration.TotalSeconds));
                    Stats.Add(new StatItem("\uE734", StringHelper.FormatDurationCompact(topDuration), "Top App", topApp.Key));
                }

                // 7. Unique Apps
                int uniqueApps = sorted.Where(s => !s.IsAfk).Select(s => s.ProcessName).Distinct().Count();
                Stats.Add(new StatItem("\uE74C", uniqueApps.ToString(), "Unique Apps", "Different applications used"));

                // 9. Top Website
                var browserProcesses = new HashSet<string>(StringComparer.OrdinalIgnoreCase) 
                { "chrome", "msedge", "firefox", "opera", "brave", "vivaldi", "arc" };
                
                var topSite = sorted
                    .Where(s => !s.IsAfk && browserProcesses.Contains(s.ProcessName.ToLowerInvariant()))
                    .Where(s => !string.IsNullOrEmpty(s.ProgramName) && s.ProgramName != s.ProcessName)
                    .GroupBy(s => s.ProgramName)
                    .OrderByDescending(g => g.Sum(s => s.Duration.TotalSeconds))
                    .FirstOrDefault();

                if (topSite != null)
                {
                    var siteDuration = TimeSpan.FromSeconds(topSite.Sum(s => s.Duration.TotalSeconds));
                    string siteName = topSite.Key;
                    if (siteName.Length > 25) siteName = siteName.Substring(0, 25) + "...";
                    Stats.Add(new StatItem("\uE774", StringHelper.FormatDurationCompact(siteDuration), "Top Website", siteName));
                }
            }
            finally
            {
                _isBusy = false;
            }
        }
        
        // Helper to load range (Blocking logic moved to Task)
        private async Task<List<AppSession>> LoadSessionsForDateRange(DateTime start, DateTime end)
        {
            return await Task.Run(async () =>
            {
                List<AppSession> total = new List<AppSession>();
                // Sanity check for AllTime to prevent freeze
                if ((end - start).TotalDays > 365 * 2) start = end.AddYears(-2);

                for (DateTime date = start; date <= end; date = date.AddDays(1))
                {
                    var sessions = await _repository.GetSessionsForDateAsync(date);
                    if (sessions != null) total.AddRange(sessions);
                }
                
                // Retro clean if needed
                if (UserPreferences.CustomTitleRules.Count > 0)
                {
                     foreach (var s in total)
                    {
                        s.ProgramName = DigitalWellbeing.Core.Helpers.WindowTitleParser.Parse(
                            s.ProcessName, s.ProgramName, UserPreferences.CustomTitleRules);
                    }
                }
                return total;
            });
        }
    }

    public class StatItem
    {
        public string Icon { get; set; }
        public string Value { get; set; }
        public string Label { get; set; }
        public string Description { get; set; }

        public StatItem(string icon, string value, string label, string description)
        {
            Icon = icon;
            Value = value;
            Label = label;
            Description = description;
        }
    }
}
