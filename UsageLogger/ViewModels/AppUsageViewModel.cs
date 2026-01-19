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

	public DateTime LoadedDate = DateTime.Now.Date;

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
			if (!(LoadedDate.Date == DateTime.Now.Date))
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

	public bool CanGoNext => LoadedDate.Date < DateTime.Now.Date;

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
		DateTime targetDate = baseDate ?? DateTime.Now.Date;
		SetLoading(value: true);
		await SettingsManager.WaitForInit;
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
		if (!IsWeeklyDataLoaded || (!force && !(DateTime.Now.Date == LoadedDate.Date)))
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
		DispatcherQueue dispatcherQueue = DispatcherQueue.GetForCurrentThread();
		if (dispatcherQueue == null)
		{
			try
			{
				dispatcherQueue = Window.Current?.DispatcherQueue;
			}
			catch
			{
			}
		}
		Action updateAction = delegate
		{
			SetLoading(value: true);
			try
			{
				List<AppUsage> list = appUsageList.Where(appUsageFilter).ToList();
				list.Sort(appUsageSorter);
				TotalDuration = TimeSpan.Zero;
				ObservableCollection<PieChartItem> observableCollection = new ObservableCollection<PieChartItem>();
				ObservableCollection<AppUsageListItem> observableCollection2 = new ObservableCollection<AppUsageListItem>();
				double num = 0.0;
				foreach (AppUsage item2 in list)
				{
					if (item2.Duration.TotalSeconds > 0.0)
					{
						TotalDuration = TotalDuration.Add(item2.Duration);
					}
					FocusManager.Instance.RegisterApp(item2.ProcessName);
				}
				int val = 0;
				if (TotalDuration.TotalSeconds > 0.0)
				{
					val = list.Count((AppUsage a) => a.Duration >= UserPreferences.MinimumDuration && a.Duration.TotalSeconds / TotalDuration.TotalSeconds * 100.0 >= 1.0);
				}
				val = Math.Max(1, Math.Min(val, 50));
				Color colorValue = new UISettings().GetColorValue(UIColorType.Accent);
				List<Color> list2 = new List<Color>();
				for (int num2 = 0; num2 < val; num2++)
				{
					float num3 = 1f - 0.6f * (float)num2 / (float)Math.Max(1, val);
					list2.Add(Color.FromArgb(colorValue.A, (byte)((float)(int)colorValue.R * num3), (byte)((float)(int)colorValue.G * num3), (byte)((float)(int)colorValue.B * num3)));
				}
				foreach (AppUsage item3 in list)
				{
					if (!(item3.Duration < UserPreferences.MinimumDuration))
					{
						double num4 = 0.0;
						if (TotalDuration.TotalSeconds > 0.0)
						{
							num4 = item3.Duration.TotalSeconds / TotalDuration.TotalSeconds * 100.0;
						}
						bool flag = false;
						if (num4 < 1.0)
						{
							flag = true;
						}
						if (flag)
						{
							num += item3.Duration.TotalMinutes;
						}
						else if (observableCollection.Count < 50)
						{
							int count = observableCollection.Count;
							Color color = list2[count % list2.Count];
							PieChartItem item = new PieChartItem
							{
								Value = item3.Duration.TotalMinutes,
								Name = UserPreferences.GetDisplayName(item3.ProcessName),
								Tooltip = ChartFactory.FormatHours(item3.Duration.TotalHours),
								Color = color,
								ProcessName = item3.ProcessName,
								Percentage = num4
							};
							observableCollection.Add(item);
						}
						AppTag appTag = AppTagHelper.GetAppTag(item3.ProcessName);
						AppUsageListItem appUsageListItem = new AppUsageListItem(item3.ProcessName, item3.ProgramName, item3.Duration, (int)num4, appTag);
						if (item3.ProgramBreakdown != null && item3.ProgramBreakdown.Count > 0)
						{
							foreach (KeyValuePair<string, TimeSpan> item4 in item3.ProgramBreakdown.OrderByDescending((KeyValuePair<string, TimeSpan> k) => k.Value).ToList())
							{
								if (!(item4.Value < UserPreferences.MinimumDuration))
								{
									string text = item3.ProcessName + "|" + item4.Key;
									if (!UserPreferences.ExcludedTitles.Contains(text))
									{
										string title = item4.Key;
										if (UserPreferences.TitleDisplayNames.TryGetValue(text, out var value))
										{
											title = "* " + value;
										}
										double num5 = ((item3.Duration.TotalSeconds > 1.0) ? item3.Duration.TotalSeconds : 1.0);
										int percentage = (int)Math.Round(item4.Value.TotalSeconds / num5 * 100.0);
										AppTag titleTag = AppTagHelper.GetTitleTag(item3.ProcessName, item4.Key);
										AppUsageSubItem appUsageSubItem = new AppUsageSubItem(title, item3.ProcessName, item4.Value, percentage, null, titleTag);
										if (titleTag == AppTag.Untagged)
										{
											appUsageSubItem.TagIndicatorBrush = new SolidColorBrush(Colors.Transparent);
											appUsageSubItem.TagTextBrush = null;
											appUsageSubItem.BackgroundBrush = new SolidColorBrush(Colors.Transparent);
										}
										else
										{
											SolidColorBrush solidColorBrush = (appUsageSubItem.TagTextBrush = (appUsageSubItem.TagIndicatorBrush = AppTagHelper.GetTagColor(titleTag) as SolidColorBrush));
											if (solidColorBrush != null)
											{
												Color color2 = solidColorBrush.Color;
												color2.A = 128;
												appUsageSubItem.BackgroundBrush = new SolidColorBrush(color2);
											}
										}
										appUsageListItem.Children.Add(appUsageSubItem);
									}
								}
							}
						}
						observableCollection2.Add(appUsageListItem);
					}
				}
				if (num > 0.0)
				{
					observableCollection.Add(new PieChartItem
					{
						Value = num,
						Name = "Other Apps",
						Color = Colors.Gray,
						Tooltip = ChartFactory.FormatHours(TimeSpan.FromMinutes(num).TotalHours),
						ProcessName = "Other",
						Percentage = ((TotalDuration.TotalMinutes > 0.0) ? (num / TotalDuration.TotalMinutes * 100.0) : 0.0)
					});
				}
				if (observableCollection.Count == 0)
				{
					observableCollection.Add(new PieChartItem
					{
						Value = 1.0,
						Name = "No Data",
						Color = Colors.LightGray,
						Tooltip = "No Data",
						ProcessName = "",
						Percentage = 0.0
					});
				}
				UpdatePieChartInPlace(PieChartItems, observableCollection);

				UpdateListInPlace(DayListItems, observableCollection2);
				List<AppUsageListItem> list3 = new List<AppUsageListItem>();
				List<AppUsageListItem> list4 = new List<AppUsageListItem>();
				List<AppUsageListItem> list5 = new List<AppUsageListItem>();
				for (int num6 = 0; num6 < DayListItems.Count; num6++)
				{
					switch (num6 % 3)
					{
					case 0:
						list3.Add(DayListItems[num6]);
						break;
					case 1:
						list4.Add(DayListItems[num6]);
						break;
					default:
						list5.Add(DayListItems[num6]);
						break;
					}
				}
				UpdateListInPlace(Column1Items, list3);
				UpdateListInPlace(Column2Items, list4);
				UpdateListInPlace(Column3Items, list5);
				LoadTrendData(LoadedDate, TotalDuration);
				LoadGoalStreaks();
				if (LoadedDate.Date == DateTime.Now.Date && XamlRoot != null)
				{
					_ = CheckTimeLimitsForCurrentDataAsync(list);
				}
			}
			finally
			{
				NotifyChange();
				SetLoading(value: false);
			}
		};
		if (dispatcherQueue != null)
		{
			dispatcherQueue.TryEnqueue(delegate
			{
				updateAction();
			});
		}
		else
		{
			updateAction();
		}
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

	private void UpdateListInPlace(ObservableCollection<AppUsageListItem> existingList, IList<AppUsageListItem> newItems)
	{
		Dictionary<string, AppUsageListItem> dictionary = new Dictionary<string, AppUsageListItem>();
		foreach (AppUsageListItem newItem in newItems)
		{
			if (!dictionary.ContainsKey(newItem.ProcessName))
			{
				dictionary[newItem.ProcessName] = newItem;
			}
		}
		for (int num = existingList.Count - 1; num >= 0; num--)
		{
			AppUsageListItem appUsageListItem = existingList[num];
			if (dictionary.TryGetValue(appUsageListItem.ProcessName, out var value))
			{
				appUsageListItem.Duration = value.Duration;
				appUsageListItem.Percentage = value.Percentage;
				appUsageListItem.Refresh();
				if (appUsageListItem.Children.Count != value.Children.Count)
				{
					appUsageListItem.Children.Clear();
					foreach (AppUsageSubItem child in value.Children)
					{
						appUsageListItem.Children.Add(child);
					}
				}
				else
				{
					foreach (AppUsageSubItem existingChild in appUsageListItem.Children)
					{
						AppUsageSubItem appUsageSubItem = value.Children.FirstOrDefault((AppUsageSubItem c) => c.Title == existingChild.Title);
						if (appUsageSubItem != null)
						{
							existingChild.Duration = appUsageSubItem.Duration;
							existingChild.Percentage = appUsageSubItem.Percentage;
						}
					}
					foreach (AppUsageSubItem newChild in value.Children)
					{
						if (!appUsageListItem.Children.Any((AppUsageSubItem c) => c.Title == newChild.Title))
						{
							appUsageListItem.Children.Add(newChild);
						}
					}
				}
				dictionary.Remove(appUsageListItem.ProcessName);
			}
			else
			{
				existingList.RemoveAt(num);
			}
		}
		foreach (AppUsageListItem value2 in dictionary.Values)
		{
			existingList.Add(value2);
		}
		List<AppUsageListItem> list = existingList.OrderByDescending((AppUsageListItem x) => x.Duration).ToList();
		for (int num2 = 0; num2 < list.Count; num2++)
		{
			int num3 = existingList.IndexOf(list[num2]);
			if (num3 != num2)
			{
				existingList.Move(num3, num2);
			}
		}
	}

	private void UpdatePieChartInPlace(ObservableCollection<PieChartItem> existingList, ObservableCollection<PieChartItem> newItems)
	{
		Dictionary<string, PieChartItem> dictionary = new Dictionary<string, PieChartItem>();
		foreach (PieChartItem newItem in newItems)
		{
			// Unique key using ProcessName or Name
			string key = !string.IsNullOrEmpty(newItem.ProcessName) ? newItem.ProcessName : newItem.Name;
			if (!dictionary.ContainsKey(key))
			{
				dictionary[key] = newItem;
			}
		}

		for (int num = existingList.Count - 1; num >= 0; num--)
		{
			PieChartItem existingItem = existingList[num];
			string key = !string.IsNullOrEmpty(existingItem.ProcessName) ? existingItem.ProcessName : existingItem.Name;

			if (dictionary.TryGetValue(key, out var newItem))
			{
				existingItem.Value = newItem.Value;
				existingItem.Percentage = newItem.Percentage;
				existingItem.Color = newItem.Color;
				existingItem.Tooltip = newItem.Tooltip;
				existingItem.Name = newItem.Name; // Display name might change
				
				dictionary.Remove(key);
			}
			else
			{
				existingList.RemoveAt(num);
			}
		}

		foreach (PieChartItem value2 in dictionary.Values)
		{
			existingList.Add(value2);
		}

		// Sort by Value Descending
		List<PieChartItem> sortedList = existingList.OrderByDescending(x => x.Value).ToList();
		for (int num2 = 0; num2 < sortedList.Count; num2++)
		{
			int num3 = existingList.IndexOf(sortedList[num2]);
			if (num3 != num2)
			{
				existingList.Move(num3, num2);
			}
		}
	}

	private void UpdateBackgroundAudioList(List<AppUsage> backgroundAudioList)
	{
		DispatcherQueue forCurrentThread = DispatcherQueue.GetForCurrentThread();
		Action updateAction = delegate
		{
			try
			{
				BackgroundAudioItems.Clear();
				BackgroundAudioColumn1.Clear();
				BackgroundAudioColumn2.Clear();
				BackgroundAudioColumn3.Clear();
				foreach (AppUsage backgroundAudio in backgroundAudioList)
				{
					if (!(backgroundAudio.Duration < UserPreferences.MinimumDuration))
					{
						int percentage = 0;
						AppTag appTag = AppTagHelper.GetAppTag(backgroundAudio.ProcessName);
						AppUsageListItem item = new AppUsageListItem(backgroundAudio.ProcessName, backgroundAudio.ProgramName, backgroundAudio.Duration, percentage, appTag);
						BackgroundAudioItems.Add(item);
					}
				}
				List<AppUsageListItem> list = new List<AppUsageListItem>();
				List<AppUsageListItem> list2 = new List<AppUsageListItem>();
				List<AppUsageListItem> list3 = new List<AppUsageListItem>();
				int num = 0;
				foreach (AppUsageListItem backgroundAudioItem in BackgroundAudioItems)
				{
					if (num % 3 == 0)
					{
						list.Add(backgroundAudioItem);
					}
					else if (num % 3 == 1)
					{
						list2.Add(backgroundAudioItem);
					}
					else
					{
						list3.Add(backgroundAudioItem);
					}
					num++;
				}
				foreach (AppUsageListItem item2 in list)
				{
					BackgroundAudioColumn1.Add(item2);
				}
				foreach (AppUsageListItem item3 in list2)
				{
					BackgroundAudioColumn2.Add(item3);
				}
				foreach (AppUsageListItem item4 in list3)
				{
					BackgroundAudioColumn3.Add(item4);
				}
			}
			catch (Exception)
			{
			}
		};
		if (forCurrentThread != null)
		{
			forCurrentThread.TryEnqueue(delegate
			{
				updateAction();
			});
		}
		else
		{
			updateAction();
		}
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

	private async void LoadTrendData(DateTime currentDate, TimeSpan currentTotal)
	{
		await Task.Run(async delegate
		{
			DateTime pastDate = currentDate.AddDays(-7.0);
			List<AppUsage> list = (await GetData(pastDate)).Where(appUsageFilter).ToList();
			TimeSpan timeSpan = TimeSpan.Zero;
			foreach (AppUsage item in list)
			{
				timeSpan = timeSpan.Add(item.Duration);
			}
			double totalMinutes = currentTotal.TotalMinutes;
			double totalMinutes2 = timeSpan.TotalMinutes;
			string percentage = "";
			string desc = LocalizationHelper.GetString("FromLast") + " " + pastDate.ToString("dddd");
			bool isGood = true;
			if (totalMinutes2 == 0.0)
			{
				percentage = ((totalMinutes > 0.0) ? "100%" : "0%");
				isGood = totalMinutes == 0.0;
			}
			else
			{
				double num = (totalMinutes - totalMinutes2) / totalMinutes2 * 100.0;
				if (num > 0.0)
				{
					percentage = $"↑ {Math.Abs((int)num)}%";
					isGood = true;
				}
				else
				{
					percentage = $"↓ {Math.Abs((int)num)}%";
					isGood = false;
				}
			}
			dispatcherQueue.TryEnqueue(delegate
			{
				TrendPercentage = percentage;
				TrendDescription = desc;
				TrendIsGood = isGood;
			});
		});
	}

	private async void LoadGoalStreaks()
	{
		await Task.Run(async delegate
		{
			int gamingStreak = 0;
			int focusStreak = 0;
			int socialStreak = 0;
			DateTime date = DateTime.Now.Date.AddDays(-1.0);
			for (int i = 0; i < 30; i++)
			{
				List<AppUsage> obj = await GetData(date);
				TimeSpan zero = TimeSpan.Zero;
				TimeSpan zero2 = TimeSpan.Zero;
				TimeSpan zero3 = TimeSpan.Zero;
				foreach (AppUsage item in obj)
				{
					if (!IsProcessExcluded(item.ProcessName))
					{
						AppTag appTag = AppTagHelper.GetAppTag(item.ProcessName);
						if (appTag == AppTag.Game)
						{
							zero += item.Duration;
						}
						if (appTag == AppTag.Work || appTag == AppTag.Education)
						{
							zero2 += item.Duration;
						}
						if (appTag == AppTag.Social)
						{
							zero3 += item.Duration;
						}
					}
				}
				if (!(zero.TotalHours < 1.0))
				{
					break;
				}
				gamingStreak++;
				date = date.AddDays(-1.0);
			}
			gamingStreak = 0;
			focusStreak = 0;
			socialStreak = 0;
			bool gFail = false;
			bool fFail = false;
			bool sFail = false;
			date = DateTime.Now.Date.AddDays(-1.0);
			for (int i = 0; i < 30; i++)
			{
				if (gFail && fFail && sFail)
				{
					break;
				}
				List<AppUsage> obj2 = await GetData(date);
				TimeSpan zero4 = TimeSpan.Zero;
				TimeSpan zero5 = TimeSpan.Zero;
				TimeSpan zero6 = TimeSpan.Zero;
				foreach (AppUsage item2 in obj2)
				{
					if (!IsProcessExcluded(item2.ProcessName))
					{
						AppTag appTag2 = AppTagHelper.GetAppTag(item2.ProcessName);
						if (appTag2 == AppTag.Game)
						{
							zero4 += item2.Duration;
						}
						if (appTag2 == AppTag.Work || appTag2 == AppTag.Education)
						{
							zero5 += item2.Duration;
						}
						if (appTag2 == AppTag.Social)
						{
							zero6 += item2.Duration;
						}
					}
				}
				if (!gFail)
				{
					if (zero4.TotalHours < 1.0)
					{
						gamingStreak++;
					}
					else
					{
						gFail = true;
					}
				}
				if (!fFail)
				{
					if (zero5.TotalHours >= 4.0)
					{
						focusStreak++;
					}
					else
					{
						fFail = true;
					}
				}
				if (!sFail)
				{
					if (zero6.TotalMinutes < 30.0)
					{
						socialStreak++;
					}
					else
					{
						sFail = true;
					}
				}
				date = date.AddDays(-1.0);
			}
			dispatcherQueue.TryEnqueue(delegate
			{
				GoalStreaks.Clear();
				GoalStreaks.Add(new GoalStreakItem
				{
					Count = gamingStreak.ToString(),
					Label = "days with < 1h Gaming",
					IconColor = new SolidColorBrush(Color.FromArgb(byte.MaxValue, 0, 120, 215))
				});
				GoalStreaks.Add(new GoalStreakItem
				{
					Count = focusStreak.ToString(),
					Label = "days meeting Focus Goal (4h)",
					IconColor = new SolidColorBrush(Color.FromArgb(byte.MaxValue, 128, 0, 128))
				});
				GoalStreaks.Add(new GoalStreakItem
				{
					Count = socialStreak.ToString(),
					Label = "days for < 30m Social Media",
					IconColor = new SolidColorBrush(Color.FromArgb(byte.MaxValue, 128, 128, 128))
				});
			});
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
