using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.UI.Xaml.Media;
using UsageLogger.Core.Helpers;
using UsageLogger.Core.Models;
using UsageLogger.Helpers;
using UsageLogger.Models;
using UsageLogger.ViewModels;

namespace UsageLogger.Helpers;

/// <summary>
/// Generates treemap data for the History page from aggregated usage.
/// Extracted from HistoryViewModel to separate chart building from ViewModel state.
/// </summary>
public static class HistoryChartGenerator
{
    /// <summary>
    /// Generates category-level treemap data by aggregating usage per AppTag.
    /// </summary>
    public static ObservableCollection<TreemapItem> GenerateTagChart(List<AppUsage> usage)
    {
        Dictionary<AppTag, double> tagDurations = new Dictionary<AppTag, double>();

        foreach (var app in usage)
        {
            if (AppUsageViewModel.IsProcessExcluded(app.ProcessName)) continue;

            AppTag parentTag = AppTagHelper.GetAppTag(app.ProcessName);
            double remainingMinutes = app.Duration.TotalMinutes;

            if (app.ProgramBreakdown != null && app.ProgramBreakdown.Count > 0)
            {
                foreach (var child in app.ProgramBreakdown)
                {
                    AppTag childTag = AppTagHelper.GetTitleTag(app.ProcessName, child.Key);
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

            if (remainingMinutes > 0)
            {
                if (tagDurations.ContainsKey(parentTag))
                    tagDurations[parentTag] += remainingMinutes;
                else
                    tagDurations[parentTag] = remainingMinutes;
            }
        }

        var filteredTags = tagDurations.Where(k => k.Value >= 1.0).ToList();
        double totalDuration = filteredTags.Sum(k => k.Value);

        var treemapItems = new ObservableCollection<TreemapItem>();
        foreach (var kvp in filteredTags.OrderByDescending(k => k.Value))
        {
            try
            {
                var brush = (SolidColorBrush)AppTagHelper.GetTagColor(kvp.Key);
                double percentage = totalDuration > 0 ? (kvp.Value / totalDuration) * 100 : 0;

                treemapItems.Add(new TreemapItem
                {
                    Name = AppTagHelper.GetTagDisplayName(kvp.Key),
                    Value = kvp.Value,
                    Percentage = percentage,
                    FormattedValue = StringHelper.FormatDurationFull(TimeSpan.FromMinutes(kvp.Value)),
                    Fill = brush
                });
            }
            catch
            {
                double percentage = totalDuration > 0 ? (kvp.Value / totalDuration) * 100 : 0;
                treemapItems.Add(new TreemapItem
                {
                    Name = AppTagHelper.GetTagDisplayName(kvp.Key),
                    Value = kvp.Value,
                    Percentage = percentage,
                    FormattedValue = StringHelper.FormatDurationFull(TimeSpan.FromMinutes(kvp.Value)),
                    Fill = new SolidColorBrush(Microsoft.UI.Colors.Gray)
                });
            }
        }

        return treemapItems;
    }

    /// <summary>
    /// Generates app-level treemap data.
    /// </summary>
    public static ObservableCollection<TreemapItem> GenerateAppChart(List<AppUsage> usage)
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

        var palette = GenerateAccentPalette(visibleApps.Count);

        var treemapItems = new ObservableCollection<TreemapItem>();
        int colorIndex = 0;
        foreach (var kvp in visibleApps)
        {
            double percentage = totalDuration > 0 ? (kvp.Value / totalDuration) * 100 : 0;
            var brush = new SolidColorBrush(palette[colorIndex % palette.Count]);

            treemapItems.Add(new TreemapItem
            {
                Name = TruncateName(UserPreferences.GetDisplayName(kvp.Key)),
                Value = kvp.Value,
                Percentage = percentage,
                FormattedValue = StringHelper.FormatDurationFull(TimeSpan.FromMinutes(kvp.Value)),
                Fill = brush
            });
            colorIndex++;
        }

        return treemapItems;
    }

    /// <summary>
    /// Generates sub-app-level treemap data (window titles as individual entries).
    /// </summary>
    public static ObservableCollection<TreemapItem> GenerateSubAppChart(List<AppUsage> usage)
    {
        Dictionary<string, (string DisplayName, double Minutes)> subAppDurations = new();

        foreach (var app in usage)
        {
            if (AppUsageViewModel.IsProcessExcluded(app.ProcessName)) continue;

            string parentDisplayName = UserPreferences.GetDisplayName(app.ProcessName);

            if (app.ProgramBreakdown != null && app.ProgramBreakdown.Count > 0)
            {
                foreach (var subApp in app.ProgramBreakdown)
                {
                    if (UserPreferences.ShouldHideSubApp(subApp.Key)) continue;

                    string titleKey = $"{app.ProcessName}|{subApp.Key}";
                    if (UserPreferences.ExcludedTitles.Contains(titleKey)) continue;

                    string subAppDisplayName;
                    if (UserPreferences.TitleDisplayNames.TryGetValue(titleKey, out string customName))
                        subAppDisplayName = customName;
                    else
                        subAppDisplayName = subApp.Key;

                    if (subAppDurations.ContainsKey(titleKey))
                    {
                        var existing = subAppDurations[titleKey];
                        subAppDurations[titleKey] = (existing.DisplayName, existing.Minutes + subApp.Value.TotalMinutes);
                    }
                    else
                    {
                        subAppDurations[titleKey] = (subAppDisplayName, subApp.Value.TotalMinutes);
                    }
                }
            }
            else
            {
                if (subAppDurations.ContainsKey(app.ProcessName))
                {
                    var existing = subAppDurations[app.ProcessName];
                    subAppDurations[app.ProcessName] = (existing.DisplayName, existing.Minutes + app.Duration.TotalMinutes);
                }
                else
                {
                    subAppDurations[app.ProcessName] = (parentDisplayName, app.Duration.TotalMinutes);
                }
            }
        }

        var visibleApps = subAppDurations.Where(k => k.Value.Minutes >= 1.0).OrderByDescending(k => k.Value.Minutes).Take(20).ToList();
        double totalDuration = visibleApps.Sum(k => k.Value.Minutes);

        var palette = GenerateAccentPalette(visibleApps.Count);

        var treemapItems = new ObservableCollection<TreemapItem>();
        int colorIndex = 0;
        foreach (var kvp in visibleApps)
        {
            double percentage = totalDuration > 0 ? (kvp.Value.Minutes / totalDuration) * 100 : 0;
            var brush = new SolidColorBrush(palette[colorIndex % palette.Count]);

            treemapItems.Add(new TreemapItem
            {
                Name = TruncateName(kvp.Value.DisplayName),
                Value = kvp.Value.Minutes,
                Percentage = percentage,
                FormattedValue = StringHelper.FormatDurationFull(TimeSpan.FromMinutes(kvp.Value.Minutes)),
                Fill = brush
            });
            colorIndex++;
        }

        return treemapItems;
    }

    /// <summary>
    /// Generates an accent-color-based gradient palette.
    /// </summary>
    private static List<Windows.UI.Color> GenerateAccentPalette(int count)
    {
        var uiSettings = new Windows.UI.ViewManagement.UISettings();
        var accent = uiSettings.GetColorValue(Windows.UI.ViewManagement.UIColorType.Accent);
        var palette = new List<Windows.UI.Color>();
        for (int i = 0; i < count; i++)
        {
            float factor = 1.0f - (0.6f * i / (float)Math.Max(1, count));
            palette.Add(Windows.UI.Color.FromArgb(accent.A,
                (byte)(accent.R * factor),
                (byte)(accent.G * factor),
                (byte)(accent.B * factor)));
        }
        return palette;
    }

    /// <summary>
    /// Truncates a name to the specified length with ellipsis.
    /// </summary>
    public static string TruncateName(string name, int maxLength = 30)
    {
        if (string.IsNullOrEmpty(name)) return name;
        if (name.Length <= maxLength) return name;
        return name.Substring(0, maxLength - 3) + "...";
    }
}
