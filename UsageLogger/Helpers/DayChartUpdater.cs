using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using UsageLogger.Core.Models;
using UsageLogger.Helpers;
using UsageLogger.Models;
using Windows.UI;
using Windows.UI.ViewManagement;

namespace UsageLogger.ViewModels;

/// <summary>
/// Handles pie chart, app usage list, and background audio list updates for the Dashboard.
/// Extracted from AppUsageViewModel to separate chart/list data transformation from ViewModel state.
/// </summary>
public static class DayChartUpdater
{
    /// <summary>
    /// Builds and updates the pie chart and app usage list from raw usage data.
    /// Dispatches to the UI thread if a dispatcher is available.
    /// </summary>
    public static void UpdatePieChartAndList(
        List<AppUsage> appUsageList,
        Func<AppUsage, bool> appUsageFilter,
        Comparison<AppUsage> appUsageSorter,
        ObservableCollection<PieChartItem> pieChartItems,
        ObservableCollection<AppUsageListItem> dayListItems,
        ObservableCollection<AppUsageListItem> column1Items,
        ObservableCollection<AppUsageListItem> column2Items,
        ObservableCollection<AppUsageListItem> column3Items,
        ObservableCollection<GoalStreakItem> goalStreaks,
        DateTime loadedDate,
        XamlRoot xamlRoot,
        DispatcherQueue viewModelDispatcher,
        Action<TimeSpan> setTotalDuration,
        Action<string, string, bool> setTrend,
        Action notifyChange,
        Action<bool> setLoading,
        Action<List<AppUsage>> checkTimeLimits)
    {
        DispatcherQueue dispatcherQueue = DispatcherQueue.GetForCurrentThread();
        if (dispatcherQueue == null)
        {
            try { dispatcherQueue = Window.Current?.DispatcherQueue; } catch { }
        }

        Action updateAction = () =>
        {
            setLoading(true);
            try
            {
                List<AppUsage> list = appUsageList.Where(appUsageFilter).ToList();
                list.Sort(appUsageSorter);
                TimeSpan totalDuration = TimeSpan.Zero;
                ObservableCollection<PieChartItem> newPie = new ObservableCollection<PieChartItem>();
                ObservableCollection<AppUsageListItem> newList = new ObservableCollection<AppUsageListItem>();
                double otherMinutes = 0.0;

                foreach (AppUsage item in list)
                {
                    if (item.Duration.TotalSeconds > 0.0)
                    {
                        totalDuration = totalDuration.Add(item.Duration);
                    }
                    FocusManager.Instance.RegisterApp(item.ProcessName);
                }

                int visibleCount = 0;
                if (totalDuration.TotalSeconds > 0.0)
                {
                    visibleCount = list.Count(a => a.Duration >= UserPreferences.MinimumDuration && a.Duration.TotalSeconds / totalDuration.TotalSeconds * 100.0 >= 1.0);
                }
                visibleCount = Math.Max(1, Math.Min(visibleCount, 50));

                Color accentColor = new UISettings().GetColorValue(UIColorType.Accent);
                List<Color> colorGradient = new List<Color>();
                for (int i = 0; i < visibleCount; i++)
                {
                    float factor = 1f - 0.6f * (float)i / (float)Math.Max(1, visibleCount);
                    colorGradient.Add(Color.FromArgb(accentColor.A,
                        (byte)(accentColor.R * factor),
                        (byte)(accentColor.G * factor),
                        (byte)(accentColor.B * factor)));
                }

                foreach (AppUsage item in list)
                {
                    if (item.Duration < UserPreferences.MinimumDuration) continue;

                    double percentage = 0.0;
                    if (totalDuration.TotalSeconds > 0.0)
                    {
                        percentage = item.Duration.TotalSeconds / totalDuration.TotalSeconds * 100.0;
                    }

                    if (percentage < 1.0)
                    {
                        otherMinutes += item.Duration.TotalMinutes;
                    }
                    else if (newPie.Count < 50)
                    {
                        int idx = newPie.Count;
                        Color color = colorGradient[idx % colorGradient.Count];
                        newPie.Add(new PieChartItem
                        {
                            Value = item.Duration.TotalMinutes,
                            Name = UserPreferences.GetDisplayName(item.ProcessName),
                            Tooltip = ChartFactory.FormatHours(item.Duration.TotalHours),
                            Color = color,
                            ProcessName = item.ProcessName,
                            Percentage = percentage
                        });
                    }

                    AppTag appTag = AppTagHelper.GetAppTag(item.ProcessName);
                    AppUsageListItem listItem = new AppUsageListItem(item.ProcessName, item.ProgramName, item.Duration, (int)percentage, appTag);

                    if (item.ProgramBreakdown != null && item.ProgramBreakdown.Count > 0)
                    {
                        foreach (var sub in item.ProgramBreakdown.OrderByDescending(k => k.Value).ToList())
                        {
                            if (sub.Value < UserPreferences.MinimumDuration) continue;

                            string compositeKey = item.ProcessName + "|" + sub.Key;
                            if (UserPreferences.ExcludedTitles.Contains(compositeKey)) continue;

                            string title = sub.Key;
                            if (UserPreferences.TitleDisplayNames.TryGetValue(compositeKey, out var displayName))
                            {
                                title = "* " + displayName;
                            }

                            double parentSec = (item.Duration.TotalSeconds > 1.0) ? item.Duration.TotalSeconds : 1.0;
                            int subPercentage = (int)Math.Round(sub.Value.TotalSeconds / parentSec * 100.0);
                            AppTag titleTag = AppTagHelper.GetTitleTag(item.ProcessName, sub.Key);

                            AppUsageSubItem subItem = new AppUsageSubItem(title, item.ProcessName, sub.Value, subPercentage, null, titleTag);
                            if (titleTag == AppTag.Untagged)
                            {
                                subItem.TagIndicatorBrush = new SolidColorBrush(Colors.Transparent);
                                subItem.TagTextBrush = null;
                                subItem.BackgroundBrush = new SolidColorBrush(Colors.Transparent);
                            }
                            else
                            {
                                SolidColorBrush tagBrush = AppTagHelper.GetTagColor(titleTag) as SolidColorBrush;
                                subItem.TagIndicatorBrush = tagBrush;
                                subItem.TagTextBrush = tagBrush;
                                if (tagBrush != null)
                                {
                                    Color bgColor = tagBrush.Color;
                                    bgColor.A = 128;
                                    subItem.BackgroundBrush = new SolidColorBrush(bgColor);
                                }
                            }
                            listItem.Children.Add(subItem);
                        }
                    }
                    newList.Add(listItem);
                }

                if (otherMinutes > 0.0)
                {
                    newPie.Add(new PieChartItem
                    {
                        Value = otherMinutes,
                        Name = "Other Apps",
                        Color = Colors.Gray,
                        Tooltip = ChartFactory.FormatHours(TimeSpan.FromMinutes(otherMinutes).TotalHours),
                        ProcessName = "Other",
                        Percentage = (totalDuration.TotalMinutes > 0.0) ? (otherMinutes / totalDuration.TotalMinutes * 100.0) : 0.0
                    });
                }
                if (newPie.Count == 0)
                {
                    newPie.Add(new PieChartItem
                    {
                        Value = 1.0, Name = "No Data", Color = Colors.LightGray,
                        Tooltip = "No Data", ProcessName = "", Percentage = 0.0
                    });
                }

                setTotalDuration(totalDuration);
                UpdatePieChartInPlace(pieChartItems, newPie);
                UpdateListInPlace(dayListItems, newList);

                // Distribute to 3 columns
                List<AppUsageListItem> col1 = new(), col2 = new(), col3 = new();
                for (int i = 0; i < dayListItems.Count; i++)
                {
                    switch (i % 3)
                    {
                        case 0: col1.Add(dayListItems[i]); break;
                        case 1: col2.Add(dayListItems[i]); break;
                        default: col3.Add(dayListItems[i]); break;
                    }
                }
                UpdateListInPlace(column1Items, col1);
                UpdateListInPlace(column2Items, col2);
                UpdateListInPlace(column3Items, col3);

                TrendDataLoader.LoadAsync(loadedDate, totalDuration, appUsageFilter, viewModelDispatcher, setTrend);
                GoalStreakCalculator.LoadAsync(goalStreaks, viewModelDispatcher);

                if (loadedDate.Date == DateTime.Now.Date && xamlRoot != null)
                {
                    checkTimeLimits(list);
                }
            }
            finally
            {
                notifyChange();
                setLoading(false);
            }
        };

        if (dispatcherQueue != null)
            dispatcherQueue.TryEnqueue(() => updateAction());
        else
            updateAction();
    }

    /// <summary>
    /// Updates an existing list in-place, adding/removing/reordering items to match new data.
    /// </summary>
    public static void UpdateListInPlace(ObservableCollection<AppUsageListItem> existingList, IList<AppUsageListItem> newItems)
    {
        Dictionary<string, AppUsageListItem> dict = new();
        foreach (var item in newItems)
        {
            if (!dict.ContainsKey(item.ProcessName))
                dict[item.ProcessName] = item;
        }

        for (int i = existingList.Count - 1; i >= 0; i--)
        {
            var existing = existingList[i];
            if (dict.TryGetValue(existing.ProcessName, out var updated))
            {
                existing.Duration = updated.Duration;
                existing.Percentage = updated.Percentage;
                existing.Refresh();
                if (existing.Children.Count != updated.Children.Count)
                {
                    existing.Children.Clear();
                    foreach (var child in updated.Children)
                        existing.Children.Add(child);
                }
                else
                {
                    foreach (var existingChild in existing.Children)
                    {
                        var newChild = updated.Children.FirstOrDefault(c => c.Title == existingChild.Title);
                        if (newChild != null)
                        {
                            existingChild.Duration = newChild.Duration;
                            existingChild.Percentage = newChild.Percentage;
                        }
                    }
                    foreach (var newChild in updated.Children)
                    {
                        if (!existing.Children.Any(c => c.Title == newChild.Title))
                            existing.Children.Add(newChild);
                    }
                }
                dict.Remove(existing.ProcessName);
            }
            else
            {
                existingList.RemoveAt(i);
            }
        }

        foreach (var value in dict.Values)
            existingList.Add(value);

        var sorted = existingList.OrderByDescending(x => x.Duration).ToList();
        for (int i = 0; i < sorted.Count; i++)
        {
            int currentIdx = existingList.IndexOf(sorted[i]);
            if (currentIdx != i) existingList.Move(currentIdx, i);
        }
    }

    /// <summary>
    /// Updates an existing pie chart in-place, matching items by ProcessName/Name.
    /// </summary>
    public static void UpdatePieChartInPlace(ObservableCollection<PieChartItem> existingList, ObservableCollection<PieChartItem> newItems)
    {
        Dictionary<string, PieChartItem> dict = new();
        foreach (var item in newItems)
        {
            string key = !string.IsNullOrEmpty(item.ProcessName) ? item.ProcessName : item.Name;
            if (!dict.ContainsKey(key)) dict[key] = item;
        }

        for (int i = existingList.Count - 1; i >= 0; i--)
        {
            var existing = existingList[i];
            string key = !string.IsNullOrEmpty(existing.ProcessName) ? existing.ProcessName : existing.Name;
            if (dict.TryGetValue(key, out var updated))
            {
                existing.Value = updated.Value;
                existing.Percentage = updated.Percentage;
                existing.Color = updated.Color;
                existing.Tooltip = updated.Tooltip;
                existing.Name = updated.Name;
                dict.Remove(key);
            }
            else
            {
                existingList.RemoveAt(i);
            }
        }

        foreach (var value in dict.Values)
            existingList.Add(value);

        var sorted = existingList.OrderByDescending(x => x.Value).ToList();
        for (int i = 0; i < sorted.Count; i++)
        {
            int currentIdx = existingList.IndexOf(sorted[i]);
            if (currentIdx != i) existingList.Move(currentIdx, i);
        }
    }

    /// <summary>
    /// Updates the background audio list and distributes to 3 columns.
    /// </summary>
    public static void UpdateBackgroundAudioList(
        List<AppUsage> backgroundAudioList,
        Func<AppUsage, bool> appUsageFilter,
        ObservableCollection<AppUsageListItem> backgroundAudioItems,
        ObservableCollection<AppUsageListItem> col1,
        ObservableCollection<AppUsageListItem> col2,
        ObservableCollection<AppUsageListItem> col3)
    {
        DispatcherQueue forCurrentThread = DispatcherQueue.GetForCurrentThread();
        Action updateAction = () =>
        {
            try
            {
                backgroundAudioItems.Clear();
                col1.Clear();
                col2.Clear();
                col3.Clear();

                foreach (var audio in backgroundAudioList)
                {
                    if (audio.Duration < UserPreferences.MinimumDuration) continue;
                    AppTag appTag = AppTagHelper.GetAppTag(audio.ProcessName);
                    backgroundAudioItems.Add(new AppUsageListItem(audio.ProcessName, audio.ProgramName, audio.Duration, 0, appTag));
                }

                int idx = 0;
                foreach (var item in backgroundAudioItems)
                {
                    switch (idx % 3)
                    {
                        case 0: col1.Add(item); break;
                        case 1: col2.Add(item); break;
                        default: col3.Add(item); break;
                    }
                    idx++;
                }
            }
            catch { }
        };

        if (forCurrentThread != null)
            forCurrentThread.TryEnqueue(() => updateAction());
        else
            updateAction();
    }
}
