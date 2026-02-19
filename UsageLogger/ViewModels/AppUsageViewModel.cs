// DigitalWellbeingWinUI3, Version=0.8.1.0, Culture=neutral, PublicKeyToken=null
// UsageLogger.ViewModels.AppUsageViewModel
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using UsageLogger.Core;
using UsageLogger.Core.Data;
using UsageLogger.Core.Helpers;
using UsageLogger.Core.Models;
using UsageLogger.Helpers;
using UsageLogger.Models;
using UsageLogger.ViewModels;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using Windows.UI.ViewManagement;

public class AppUsageViewModel : INotifyPropertyChanged
{
	public static readonly int MinimumPieChartPercentage = 10;

	private DispatcherTimer refreshTimer;

	public DateTime LoadedDate = DateHelper.GetLogicalToday();

	public TimeSpan TotalDuration;

	private static readonly string[] excludeProcesses = new string[13]
	{
		"process", "explorer", "SearchHost", "Idle", "StartMenuExperienceHost", "ShellExperienceHost", "dwm", "LockApp",
		"msiexec", "ApplicationFrameHost", "Away", "LogonUI", "*LAST"
	};

	private static string[] userExcludedProcesses;

	private bool _isLoading;

	public bool IsWeeklyDataLoaded;

	// Store AFK/Lock durations per date for quick access
	private Dictionary<DateTime, (TimeSpan Afk, TimeSpan Lock)> _weekAfkData = new Dictionary<DateTime, (TimeSpan, TimeSpan)>();

	private UISettings uiSettings;

	private DispatcherQueue dispatcherQueue;

	private Comparison<AppUsage> appUsageSorter = (AppUsage a, AppUsage b) => a.Duration.CompareTo(b.Duration) * -1;

	private Func<AppUsage, bool> appUsageFilter = (AppUsage a) => !IsProcessExcluded(a.ProcessName);

	private string _trendPercentage;

	private string _trendDescription;

	private bool _trendIsGood;

	public static int NumberOfDaysToDisplay { get; set; } = UserPreferences.DayAmount;

	private static string folderPath => ApplicationPath.UsageLogsFolder;

	public Func<double, string> HourFormatter { get; set; }

	public string StrLoadedDate
	{
		get
		{
			if (!(LoadedDate.Date == DateHelper.GetLogicalToday()))
			{
				return LoadedDate.ToString("dddd, MMM dd yyyy");
			}
			return LocalizationHelper.GetString("Today") + ", " + LoadedDate.ToString("dddd");
		}
	}

	public string StrTotalDuration => $"{(int)TotalDuration.TotalHours}h {TotalDuration.Minutes}m";

	public string StrMinimumDuration
	{
		get
		{
			if (!(UserPreferences.MinimumDuration.TotalSeconds <= 0.0))
			{
				return "Apps that run less than " + StringHelper.TimeSpanToString(UserPreferences.MinimumDuration) + " are hidden.";
			}
			return "";
		}
	}

	public ObservableCollection<List<AppUsage>> WeekAppUsage { get; set; }

	private ObservableCollection<BarChartItem> _weeklyChartItems;
	public ObservableCollection<BarChartItem> WeeklyChartItems
	{
		get => _weeklyChartItems;
		set
		{
			_weeklyChartItems = value;
			OnPropertyChanged("WeeklyChartItems");
		}
	}

	public ObservableCollection<PieChartItem> PieChartItems { get; set; }

	public ObservableCollection<AppUsageListItem> DayListItems { get; set; }

	public ObservableCollection<AppUsageListItem> Column1Items { get; set; } = new ObservableCollection<AppUsageListItem>();

	public ObservableCollection<AppUsageListItem> Column2Items { get; set; } = new ObservableCollection<AppUsageListItem>();

	public ObservableCollection<AppUsageListItem> Column3Items { get; set; } = new ObservableCollection<AppUsageListItem>();

	public ObservableCollection<AppUsageListItem> BackgroundAudioItems { get; set; } = new ObservableCollection<AppUsageListItem>();

	public ObservableCollection<AppUsageListItem> BackgroundAudioColumn1 { get; set; } = new ObservableCollection<AppUsageListItem>();

	public ObservableCollection<AppUsageListItem> BackgroundAudioColumn2 { get; set; } = new ObservableCollection<AppUsageListItem>();

	public ObservableCollection<AppUsageListItem> BackgroundAudioColumn3 { get; set; } = new ObservableCollection<AppUsageListItem>();

	public ObservableCollection<PieChartItem> TagsChartItems { get; set; }

	public Dictionary<AppTag, TimeSpan> CategoryDurations { get; set; } = new Dictionary<AppTag, TimeSpan>();

	public DateTime[] WeeklyChartLabelDates { get; set; }

	public bool CanGoNext => LoadedDate.Date < DateHelper.GetLogicalToday();

	public bool CanGoPrev => true;

	public bool IsLoading
	{
		get
		{
			return _isLoading;
		}
		set
		{
			if (_isLoading != value)
			{
				_isLoading = value;
				OnPropertyChanged("IsLoading");
			}
		}
	}

	public double PieChartInnerRadius { get; set; }

	public XamlRoot XamlRoot { get; set; }

	public ICommand BarChartClickCommand { get; private set; }

	public string TrendPercentage
	{
		get
		{
			return _trendPercentage;
		}
		set
		{
			_trendPercentage = value;
			OnPropertyChanged("TrendPercentage");
		}
	}

	public string TrendDescription
	{
		get
		{
			return _trendDescription;
		}
		set
		{
			_trendDescription = value;
			OnPropertyChanged("TrendDescription");
		}
	}

	public bool TrendIsGood
	{
		get
		{
			return _trendIsGood;
		}
		set
		{
			_trendIsGood = value;
			OnPropertyChanged("TrendIsGood");
		}
	}

	public ObservableCollection<GoalStreakItem> GoalStreaks { get; set; } = new ObservableCollection<GoalStreakItem>();

	// AFK Time properties for dashboard display
	private TimeSpan _afkDuration = TimeSpan.Zero;
	public TimeSpan AfkDuration
	{
		get => _afkDuration;
		set { _afkDuration = value; OnPropertyChanged(nameof(AfkDuration)); OnPropertyChanged(nameof(AfkDurationStr)); }
	}
	public string AfkDurationStr => UsageLogger.Core.Helpers.StringHelper.FormatDurationCompact(_afkDuration);

	private TimeSpan _lockDuration = TimeSpan.Zero;
	public TimeSpan LockDuration
	{
		get => _lockDuration;
		set { _lockDuration = value; OnPropertyChanged(nameof(LockDuration)); OnPropertyChanged(nameof(LockDurationStr)); }
	}
	public string LockDurationStr => UsageLogger.Core.Helpers.StringHelper.FormatDurationCompact(_lockDuration);

	public event PropertyChangedEventHandler PropertyChanged;

	private void OnBarChartClick(object param)
	{
		if (!(param is BarChartItem { Date: not null } barChartItem) || WeeklyChartLabelDates == null)
		{
			return;
		}
		for (int i = 0; i < WeeklyChartLabelDates.Length; i++)
		{
			if (WeeklyChartLabelDates[i].Date == barChartItem.Date.Value.Date)
			{
				WeeklyChart_SelectionChanged(i);
				break;
			}
		}
	}

	public AppUsageViewModel()
	{
		try
		{
			dispatcherQueue = DispatcherQueue.GetForCurrentThread();
			uiSettings = new UISettings();
			uiSettings.ColorValuesChanged += UiSettings_ColorValuesChanged;
			InitCollections();
			InitFormatters();
			LoadUserExcludedProcesses();
			InitAutoRefreshTimer();
			BarChartClickCommand = new RelayCommand(OnBarChartClick);
			LoadWeeklyData();
		}
		catch (Exception)
		{
			if (WeekAppUsage == null)
			{
				WeekAppUsage = new ObservableCollection<List<AppUsage>>();
			}
			if (WeeklyChartLabelDates == null)
			{
				WeeklyChartLabelDates = new DateTime[0];
			}
			if (DayListItems == null)
			{
				DayListItems = new ObservableCollection<AppUsageListItem>();
			}
		}
	}

	private void UiSettings_ColorValuesChanged(UISettings sender, object args)
	{
	}

	private void InitCollections()
	{
		WeekAppUsage = new ObservableCollection<List<AppUsage>>();
		WeeklyChartItems = new ObservableCollection<BarChartItem>();
		PieChartItems = new ObservableCollection<PieChartItem>();
		DayListItems = new ObservableCollection<AppUsageListItem>();
		TagsChartItems = new ObservableCollection<PieChartItem>();
	}

	private void InitFormatters()
	{
		HourFormatter = (double hours) => hours.ToString("F1") + " h";
	}

	private string FormatMinutes(double totalMinutes)
	{
		TimeSpan timeSpan = TimeSpan.FromMinutes(totalMinutes);
		if (!(timeSpan.TotalHours >= 1.0))
		{
			if (!(timeSpan.TotalMinutes >= 1.0))
			{
				return $"{timeSpan.Seconds}s";
			}
			return $"{timeSpan.Minutes}m {timeSpan.Seconds}s";
		}
		return $"{(int)timeSpan.TotalHours}h {timeSpan.Minutes}m";
	}

	private void InitAutoRefreshTimer()
	{
		TimeSpan interval = TimeSpan.FromSeconds(UserPreferences.RefreshIntervalSeconds);
		refreshTimer = new DispatcherTimer
		{
			Interval = interval
		};
		refreshTimer.Tick += delegate
		{
			TryRefreshData(force: true);
		};
		if (UserPreferences.EnableAutoRefresh)
		{
			refreshTimer.Start();
		}
	}

	/// <summary>
	/// Stops the auto-refresh timer. Call when the UI is minimized to conserve resources.
	/// </summary>
	public void StopTimer()
	{
		refreshTimer?.Stop();
	}

	/// <summary>
	/// Starts the auto-refresh timer if enabled. Call when the UI is restored.
	/// </summary>
	public void StartTimer()
	{
		if (UserPreferences.EnableAutoRefresh && refreshTimer != null)
		{
			refreshTimer.Start();
		}
	}

	/// <summary>
	/// Clears all data collections to reduce RAM usage when the UI is minimized.
	/// Call LoadWeeklyData() to reload after restoring.
	/// </summary>
	public void ClearData()
	{
		// Clear all observable collections
		WeekAppUsage?.Clear();
		WeeklyChartItems?.Clear();
		PieChartItems?.Clear();
		DayListItems?.Clear();
		Column1Items?.Clear();
		Column2Items?.Clear();
		Column3Items?.Clear();
		BackgroundAudioItems?.Clear();
		BackgroundAudioColumn1?.Clear();
		BackgroundAudioColumn2?.Clear();
		BackgroundAudioColumn3?.Clear();
		TagsChartItems?.Clear();
		GoalStreaks?.Clear();
		CategoryDurations?.Clear();
		_weekAfkData?.Clear();

		// Reset state
		IsWeeklyDataLoaded = false;
		TotalDuration = TimeSpan.Zero;
		AfkDuration = TimeSpan.Zero;
		LockDuration = TimeSpan.Zero;

		// Force GC to reclaim memory
		GC.Collect();
		GC.WaitForPendingFinalizers();
	}

	public void LoadUserExcludedProcesses()
	{
		userExcludedProcesses = UserPreferences.UserExcludedProcesses.ToArray();
	}

	public async void LoadWeeklyData(DateTime? baseDate = null)
	{
		DateTime targetDate = baseDate ?? DateHelper.GetLogicalToday();
		SetLoading(value: true);
		// UserPreferences loads synchronously in its static constructor, no need to await
		try
		{
			DateTime dateTime = targetDate.AddDays(-NumberOfDaysToDisplay);
			List<DateTime> list = new List<DateTime>();
			for (int i = 1; i <= NumberOfDaysToDisplay; i++)
			{
				list.Add(dateTime.AddDays(i).Date);
			}
			var list2 = (await Task.WhenAll(list.Select(async delegate(DateTime date)
			{
				// Get unfiltered data first for AFK calculation
				List<AppUsage> unfilteredData = await GetData(date);
				
				// Calculate AFK from unfiltered data
				var afkApp = unfilteredData.FirstOrDefault(a => a.ProcessName.Equals("Away", StringComparison.OrdinalIgnoreCase));
				var lockApp = unfilteredData.FirstOrDefault(a => a.ProcessName.Equals("LogonUI", StringComparison.OrdinalIgnoreCase));
				
				// Now filter for display
				List<AppUsage> list6 = unfilteredData.Where(appUsageFilter).ToList();
				list6.Sort(appUsageSorter);
				TimeSpan timeSpan = TimeSpan.Zero;
				foreach (AppUsage item in list6)
				{
					timeSpan = timeSpan.Add(item.Duration);
				}
				return new
				{
					Date = date,
					Usage = list6,
					Hours = timeSpan.TotalHours,
					Label = date.ToString("ddd"),
					AfkDuration = afkApp?.Duration ?? TimeSpan.Zero,
					LockDuration = lockApp?.Duration ?? TimeSpan.Zero
				};
			}).ToList())).OrderBy(r => r.Date).ToList();
			List<List<AppUsage>> list3 = new List<List<AppUsage>>();
			ObservableCollection<double> observableCollection = new ObservableCollection<double>();
			List<string> list4 = new List<string>();
			List<DateTime> list5 = new List<DateTime>();
			
			// Clear and populate AFK data cache
			_weekAfkData.Clear();
			
			foreach (var item2 in list2)
			{
				list3.Add(item2.Usage);
				observableCollection.Add(item2.Hours);
				list4.Add(item2.Label);
				list5.Add(item2.Date);
				
				// Store AFK data for this date
				_weekAfkData[item2.Date.Date] = (item2.AfkDuration, item2.LockDuration);
			}
			WeekAppUsage.Clear();
			foreach (List<AppUsage> item3 in list3)
			{
				WeekAppUsage.Add(item3);
			}
			// Atomic Update: Build list locally first
			var newWeeklyItems = new ObservableCollection<BarChartItem>();
			Color colorValue = new UISettings().GetColorValue(UIColorType.Accent);
			for (int num = 0; num < list2.Count; num++)
			{
				var anon = list2[num];
				newWeeklyItems.Add(new BarChartItem
				{
					Label = anon.Label,
					Value = anon.Hours,
					Color = colorValue,
					Tooltip = ChartFactory.FormatHours(anon.Hours),
					Date = anon.Date
				});
			}
			// Assign atomically to trigger single notification
			WeeklyChartItems = newWeeklyItems;
			WeeklyChartLabelDates = list5.ToArray();
			IsWeeklyDataLoaded = true;
			int num2 = -1;
			for (int num3 = 0; num3 < WeeklyChartLabelDates.Length; num3++)
			{
				if (WeeklyChartLabelDates[num3].Date == targetDate.Date)
				{
					num2 = num3;
					break;
				}
			}
			if (num2 != -1)
			{
				WeeklyChart_SelectionChanged(num2);
			}
			else
			{
				WeeklyChart_SelectionChanged(WeekAppUsage.Count - 1);
			}
		}
		catch (Exception value)
		{
			AppLogger.WriteLine($"Load Weekly Data Exception {value}");
		}
		finally
		{
			SetLoading(value: false);
		}
	}

	public async void WeeklyChart_SelectionChanged(int index)
	{
		try
		{
			if (index >= 0 && index < WeeklyChartLabelDates.Length)
			{
				DateTime dateTime = WeeklyChartLabelDates.ElementAt(index);
				if (!(dateTime == LoadedDate) || !(dateTime != DateTime.Now.Date))
				{
					LoadedDate = dateTime;
					TryRefreshData();
					UpdatePieChartAndList(WeekAppUsage.ElementAt(index));
					
					// Use cached AFK data instead of searching filtered list
					if (_weekAfkData.TryGetValue(dateTime.Date, out var afkData))
					{
						AfkDuration = afkData.Afk;
						LockDuration = afkData.Lock;
					}
					else
					{
						AfkDuration = TimeSpan.Zero;
						LockDuration = TimeSpan.Zero;
					}
					
					List<AppUsage> list = (await GetBackgroundAudioData(dateTime)).Where(appUsageFilter).ToList();
					list.Sort(appUsageSorter);
					UpdateBackgroundAudioList(list);
				}
			}
		}
		catch
		{
		}
	}

	public void RefreshDayView()
	{
		TryRefreshData(force: true);
	}

	private async void TryRefreshData(bool force = false)
	{
		if (!IsWeeklyDataLoaded || (!force && !(DateHelper.GetLogicalToday() == LoadedDate.Date)))
		{
			return;
		}
		try
		{
			List<AppUsage> list = await GetData(LoadedDate.Date);
			
			// Calculate AFK and Lock duration from unfiltered data
			var afkApp = list.FirstOrDefault(a => a.ProcessName.Equals("Away", StringComparison.OrdinalIgnoreCase));
			var lockApp = list.FirstOrDefault(a => a.ProcessName.Equals("LogonUI", StringComparison.OrdinalIgnoreCase));
			AfkDuration = afkApp?.Duration ?? TimeSpan.Zero;
			LockDuration = lockApp?.Duration ?? TimeSpan.Zero;
			
			int num = -1;
			for (int i = 0; i < WeeklyChartLabelDates.Length; i++)
			{
				if (WeeklyChartLabelDates[i].Date == LoadedDate.Date)
				{
					num = i;
					break;
				}
			}
			if (num != -1 && num < WeekAppUsage.Count)
			{
				WeekAppUsage[num] = list;
			}
			List<AppUsage> list2 = list.Where(appUsageFilter).ToList();
			list2.Sort(appUsageSorter);
			if (num != -1 && num < WeeklyChartItems.Count)
			{
				TimeSpan timeSpan = TimeSpan.Zero;
				foreach (AppUsage item in list2)
				{
					timeSpan = timeSpan.Add(item.Duration);
				}
				BarChartItem barChartItem = WeeklyChartItems[num];
				WeeklyChartItems[num] = new BarChartItem
				{
					Label = barChartItem.Label,
					Value = timeSpan.TotalHours,
					Color = barChartItem.Color,
					Tooltip = ChartFactory.FormatHours(timeSpan.TotalHours),
					Date = barChartItem.Date
				};
			}
			UpdatePieChartAndList(list2);
			List<AppUsage> list3 = (await GetBackgroundAudioData(LoadedDate.Date)).Where(appUsageFilter).ToList();
			list3.Sort(appUsageSorter);
			UpdateBackgroundAudioList(list3);
		}
		catch
		{
		}
	}

	private void UpdatePieChartAndList(List<AppUsage> appUsageList)
	{
		DayChartUpdater.UpdatePieChartAndList(
			appUsageList, appUsageFilter, appUsageSorter,
			PieChartItems, DayListItems, Column1Items, Column2Items, Column3Items,
			GoalStreaks, LoadedDate, XamlRoot, dispatcherQueue,
			(td) => TotalDuration = td,
			(p, d, g) => { TrendPercentage = p; TrendDescription = d; TrendIsGood = g; },
			NotifyChange,
			SetLoading,
			(list) => _ = CheckTimeLimitsForCurrentDataAsync(list));
	}

	private async Task CheckTimeLimitsForCurrentDataAsync(List<AppUsage> appUsages)
	{
		try
		{
			Dictionary<string, TimeSpan> dictionary = new Dictionary<string, TimeSpan>();
			Dictionary<string, TimeSpan> dictionary2 = new Dictionary<string, TimeSpan>();
			foreach (AppUsage appUsage in appUsages)
			{
				if (!dictionary.ContainsKey(appUsage.ProcessName))
				{
					dictionary[appUsage.ProcessName] = TimeSpan.Zero;
				}
				dictionary[appUsage.ProcessName] += appUsage.Duration;
				if (appUsage.ProgramBreakdown == null)
				{
					continue;
				}
				foreach (KeyValuePair<string, TimeSpan> item in appUsage.ProgramBreakdown)
				{
					string key = appUsage.ProcessName + "|" + item.Key;
					if (!dictionary2.ContainsKey(key))
					{
						dictionary2[key] = TimeSpan.Zero;
					}
					dictionary2[key] += item.Value;
				}
			}
			await TimeLimitEnforcer.CheckTimeLimitsAsync(dictionary, dictionary2, XamlRoot);
		}
		catch (Exception)
		{
		}
	}



	private void UpdateBackgroundAudioList(List<AppUsage> backgroundAudioList)
	{
		DayChartUpdater.UpdateBackgroundAudioList(
			backgroundAudioList, appUsageFilter,
			BackgroundAudioItems, BackgroundAudioColumn1, BackgroundAudioColumn2, BackgroundAudioColumn3);
	}

	public void NotifyChange()
	{
		OnPropertyChanged("StrLoadedDate");
		OnPropertyChanged("StrTotalDuration");
		OnPropertyChanged("StrMinimumDuration");
		OnPropertyChanged("CanGoNext");
		OnPropertyChanged("CanGoPrev");
	}

	public static bool IsProcessExcluded(string processName)
	{
		if (!Enumerable.Contains(excludeProcesses, processName))
		{
			return Enumerable.Contains(userExcludedProcesses, processName);
		}
		return true;
	}

	        public static async Task<List<AppUsage>> GetData(DateTime date)
        {
            var sessions = await new AppSessionRepository(folderPath).GetSessionsForDateAsync(date);

            return await Task.Run(() =>
            {
                Dictionary<string, AppUsage> dictionary = new Dictionary<string, AppUsage>();
                foreach (AppSession item in sessions)
                {
                    // RETROACTIVE RULE APPLICATION:
                    // Re-parse the ProgramName using current rules. This allows users to see
                    // changes immediately after adding a rule, even for past sessions.
                    if (UserPreferences.CustomTitleRules != null && UserPreferences.CustomTitleRules.Count > 0)
                    {
                        item.ProgramName = UsageLogger.Core.Helpers.WindowTitleParser.Parse(
                            item.ProcessName, 
                            item.ProgramName, 
                            UserPreferences.CustomTitleRules
                        );
                    }

                    if (!dictionary.ContainsKey(item.ProcessName))
                    {
                        dictionary[item.ProcessName] = new AppUsage(item.ProcessName, item.ProgramName, TimeSpan.Zero);
                    }
                    dictionary[item.ProcessName].Duration = dictionary[item.ProcessName].Duration.Add(item.Duration);
                    if (string.IsNullOrEmpty(dictionary[item.ProcessName].ProgramName) && !string.IsNullOrEmpty(item.ProgramName))
                    {
                        dictionary[item.ProcessName].ProgramName = item.ProgramName;
                    }
                    
                    // Apply retroactive hide filter: use ProcessName if sub-app should be hidden
                    string effectiveProgramName = item.ProgramName;
                    if (UserPreferences.ShouldHideSubApp(item.ProgramName))
                    {
                        effectiveProgramName = item.ProcessName; // Merge into parent
                    }
                    
                    string key = ((!string.IsNullOrEmpty(effectiveProgramName)) ? effectiveProgramName : item.ProcessName);
                    if (dictionary[item.ProcessName].ProgramBreakdown.ContainsKey(key))
                    {
                        dictionary[item.ProcessName].ProgramBreakdown[key] = dictionary[item.ProcessName].ProgramBreakdown[key].Add(item.Duration);
                    }
                    else
                    {
                        dictionary[item.ProcessName].ProgramBreakdown[key] = item.Duration;
                    }
                }
                return dictionary.Values.ToList();
            });
        }

	public static async Task<List<AppUsage>> GetBackgroundAudioData(DateTime date)
	{
        var sessions = await new AppSessionRepository(folderPath).GetSessionsForDateAsync(date);

		return await Task.Run(() =>
		{
			Dictionary<string, TimeSpan> dictionary = new Dictionary<string, TimeSpan>();
			foreach (AppSession item in sessions)
			{
				if (item.AudioSources != null && item.AudioSources.Count != 0)
				{
					foreach (string audioSource in item.AudioSources)
					{
						if (audioSource != item.ProcessName)
						{
							if (!dictionary.ContainsKey(audioSource))
							{
								dictionary[audioSource] = TimeSpan.Zero;
							}
							dictionary[audioSource] = dictionary[audioSource].Add(item.Duration);
						}
					}
				}
			}
			List<AppUsage> list = new List<AppUsage>();
			foreach (KeyValuePair<string, TimeSpan> item2 in dictionary)
			{
				list.Add(new AppUsage(item2.Key, item2.Key, item2.Value));
			}
			return list;
		});
	}





	private void SetLoading(bool value)
	{
		IsLoading = value;
	}

	private void OnPropertyChanged([CallerMemberName] string propertyName = "")
	{
		this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}
}
