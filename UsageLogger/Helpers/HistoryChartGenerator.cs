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
    public static ObservableCollection<TreemapItem> GenerateTagChart(List<AppUsage> usage, bool isEnergyMode = false)
    {
        Dictionary<AppTag, (double Minutes, double EnergyWh)> tagData = new();

        foreach (var app in usage)
        {
            if (AppUsageViewModel.IsProcessExcluded(app.ProcessName)) continue;

            AppTag parentTag = AppTagHelper.GetAppTag(app.ProcessName);
            double remainingMinutes = app.Duration.TotalMinutes;
            double appEnergy = app.EnergyWattHours;

            if (app.ProgramBreakdown != null && app.ProgramBreakdown.Count > 0)
            {
                foreach (var child in app.ProgramBreakdown)
                {
                    AppTag childTag = AppTagHelper.GetTitleTag(app.ProcessName, child.Key);
                    if (childTag != AppTag.Untagged && childTag != parentTag)
                    {
                        double childMinutes = child.Value.TotalMinutes;
                        double childEnergyRatio = app.Duration.TotalMinutes > 0 ? (childMinutes / app.Duration.TotalMinutes) : 0;
                        double childEnergy = appEnergy * childEnergyRatio;

                        if (tagData.ContainsKey(childTag))
                        {
                            var existing = tagData[childTag];
                            tagData[childTag] = (existing.Minutes + childMinutes, existing.EnergyWh + childEnergy);
                        }
                        else
                        {
                            tagData[childTag] = (childMinutes, childEnergy);
                        }
                        remainingMinutes -= childMinutes;
                    }
                }
            }

            if (remainingMinutes > 0)
            {
                double remRatio = app.Duration.TotalMinutes > 0 ? (remainingMinutes / app.Duration.TotalMinutes) : 1.0;
                double remEnergy = appEnergy * remRatio;

                if (tagData.ContainsKey(parentTag))
                {
                    var existing = tagData[parentTag];
                    tagData[parentTag] = (existing.Minutes + remainingMinutes, existing.EnergyWh + remEnergy);
                }
                else
                {
                    tagData[parentTag] = (remainingMinutes, remEnergy);
                }
            }
        }

        var filteredTags = isEnergyMode
            ? tagData.Where(k => k.Value.EnergyWh >= 0.05).OrderByDescending(k => k.Value.EnergyWh).ToList()
            : tagData.Where(k => k.Value.Minutes >= 1.0).OrderByDescending(k => k.Value.Minutes).ToList();

        double totalMetric = isEnergyMode
            ? filteredTags.Sum(k => k.Value.EnergyWh)
            : filteredTags.Sum(k => k.Value.Minutes);

        var treemapItems = new ObservableCollection<TreemapItem>();
        foreach (var kvp in filteredTags)
        {
            double val = isEnergyMode ? kvp.Value.EnergyWh : kvp.Value.Minutes;
            double percentage = totalMetric > 0 ? (val / totalMetric) * 100 : 0;
            string formattedVal = isEnergyMode
                ? (kvp.Value.EnergyWh >= 1000 ? $"{kvp.Value.EnergyWh / 1000.0:F2} kWh" : $"{kvp.Value.EnergyWh:F1} Wh")
                : StringHelper.FormatDurationFull(TimeSpan.FromMinutes(kvp.Value.Minutes));

            string durationStr = StringHelper.FormatDurationFull(TimeSpan.FromMinutes(kvp.Value.Minutes));
            string energyStr = kvp.Value.EnergyWh >= 1000 ? $"{kvp.Value.EnergyWh / 1000.0:F2} kWh" : $"{kvp.Value.EnergyWh:F1} Wh";
            string tooltip = isEnergyMode
                ? $"{AppTagHelper.GetTagDisplayName(kvp.Key)}\n⚡ {energyStr} ({percentage:F1}% of energy)\n⏱ {durationStr}"
                : $"{AppTagHelper.GetTagDisplayName(kvp.Key)}\n⏱ {durationStr} ({percentage:F1}% of time)\n⚡ {energyStr}";

            Brush brush;
            try { brush = (SolidColorBrush)AppTagHelper.GetTagColor(kvp.Key); }
            catch { brush = new SolidColorBrush(Microsoft.UI.Colors.Gray); }

            treemapItems.Add(new TreemapItem
            {
                Name = AppTagHelper.GetTagDisplayName(kvp.Key),
                Value = val,
                Percentage = percentage,
                FormattedValue = formattedVal,
                EnergyWattHours = kvp.Value.EnergyWh,
                IsEnergyMode = isEnergyMode,
                TooltipText = tooltip,
                Fill = brush
            });
        }

        return treemapItems;
    }

    /// <summary>
    /// Generates app-level treemap data.
    /// </summary>
    public static ObservableCollection<TreemapItem> GenerateAppChart(List<AppUsage> usage, bool isEnergyMode = false)
    {
        Dictionary<string, (double Minutes, double EnergyWh)> appData = new();

        foreach (var app in usage)
        {
            if (AppUsageViewModel.IsProcessExcluded(app.ProcessName)) continue;

            if (appData.ContainsKey(app.ProcessName))
            {
                var existing = appData[app.ProcessName];
                appData[app.ProcessName] = (existing.Minutes + app.Duration.TotalMinutes, existing.EnergyWh + app.EnergyWattHours);
            }
            else
            {
                appData[app.ProcessName] = (app.Duration.TotalMinutes, app.EnergyWattHours);
            }
        }

        var visibleApps = isEnergyMode
            ? appData.Where(k => k.Value.EnergyWh >= 0.05).OrderByDescending(k => k.Value.EnergyWh).Take(15).ToList()
            : appData.Where(k => k.Value.Minutes >= 1.0).OrderByDescending(k => k.Value.Minutes).Take(15).ToList();

        double totalMetric = isEnergyMode
            ? visibleApps.Sum(k => k.Value.EnergyWh)
            : visibleApps.Sum(k => k.Value.Minutes);

        var palette = GenerateAccentPalette(visibleApps.Count);
        var treemapItems = new ObservableCollection<TreemapItem>();
        int colorIndex = 0;

        foreach (var kvp in visibleApps)
        {
            double val = isEnergyMode ? kvp.Value.EnergyWh : kvp.Value.Minutes;
            double percentage = totalMetric > 0 ? (val / totalMetric) * 100 : 0;
            string formattedVal = isEnergyMode
                ? (kvp.Value.EnergyWh >= 1000 ? $"{kvp.Value.EnergyWh / 1000.0:F2} kWh" : $"{kvp.Value.EnergyWh:F1} Wh")
                : StringHelper.FormatDurationFull(TimeSpan.FromMinutes(kvp.Value.Minutes));

            string durationStr = StringHelper.FormatDurationFull(TimeSpan.FromMinutes(kvp.Value.Minutes));
            string energyStr = kvp.Value.EnergyWh >= 1000 ? $"{kvp.Value.EnergyWh / 1000.0:F2} kWh" : $"{kvp.Value.EnergyWh:F1} Wh";
            string displayName = UserPreferences.GetDisplayName(kvp.Key);
            string tooltip = isEnergyMode
                ? $"{displayName}\n⚡ {energyStr} ({percentage:F1}% of energy)\n⏱ {durationStr}"
                : $"{displayName}\n⏱ {durationStr} ({percentage:F1}% of time)\n⚡ {energyStr}";

            var brush = new SolidColorBrush(palette[colorIndex % palette.Count]);

            treemapItems.Add(new TreemapItem
            {
                Name = TruncateName(displayName),
                Value = val,
                Percentage = percentage,
                FormattedValue = formattedVal,
                EnergyWattHours = kvp.Value.EnergyWh,
                IsEnergyMode = isEnergyMode,
                TooltipText = tooltip,
                Fill = brush
            });
            colorIndex++;
        }

        return treemapItems;
    }

    /// <summary>
    /// Generates sub-app-level treemap data (window titles as individual entries).
    /// </summary>
    public static ObservableCollection<TreemapItem> GenerateSubAppChart(List<AppUsage> usage, bool isEnergyMode = false)
    {
        Dictionary<string, (string DisplayName, double Minutes, double EnergyWh)> subAppData = new();

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

                    double subRatio = app.Duration.TotalMinutes > 0 ? (subApp.Value.TotalMinutes / app.Duration.TotalMinutes) : 0;
                    double subEnergy = app.EnergyWattHours * subRatio;

                    if (subAppData.ContainsKey(titleKey))
                    {
                        var existing = subAppData[titleKey];
                        subAppData[titleKey] = (existing.DisplayName, existing.Minutes + subApp.Value.TotalMinutes, existing.EnergyWh + subEnergy);
                    }
                    else
                    {
                        subAppData[titleKey] = (subAppDisplayName, subApp.Value.TotalMinutes, subEnergy);
                    }
                }
            }
            else
            {
                if (subAppData.ContainsKey(app.ProcessName))
                {
                    var existing = subAppData[app.ProcessName];
                    subAppData[app.ProcessName] = (existing.DisplayName, existing.Minutes + app.Duration.TotalMinutes, existing.EnergyWh + app.EnergyWattHours);
                }
                else
                {
                    subAppData[app.ProcessName] = (parentDisplayName, app.Duration.TotalMinutes, app.EnergyWattHours);
                }
            }
        }

        var visibleApps = isEnergyMode
            ? subAppData.Where(k => k.Value.EnergyWh >= 0.05).OrderByDescending(k => k.Value.EnergyWh).Take(20).ToList()
            : subAppData.Where(k => k.Value.Minutes >= 1.0).OrderByDescending(k => k.Value.Minutes).Take(20).ToList();

        double totalMetric = isEnergyMode
            ? visibleApps.Sum(k => k.Value.EnergyWh)
            : visibleApps.Sum(k => k.Value.Minutes);

        var palette = GenerateAccentPalette(visibleApps.Count);
        var treemapItems = new ObservableCollection<TreemapItem>();
        int colorIndex = 0;

        foreach (var kvp in visibleApps)
        {
            double val = isEnergyMode ? kvp.Value.EnergyWh : kvp.Value.Minutes;
            double percentage = totalMetric > 0 ? (val / totalMetric) * 100 : 0;
            string formattedVal = isEnergyMode
                ? (kvp.Value.EnergyWh >= 1000 ? $"{kvp.Value.EnergyWh / 1000.0:F2} kWh" : $"{kvp.Value.EnergyWh:F1} Wh")
                : StringHelper.FormatDurationFull(TimeSpan.FromMinutes(kvp.Value.Minutes));

            string durationStr = StringHelper.FormatDurationFull(TimeSpan.FromMinutes(kvp.Value.Minutes));
            string energyStr = kvp.Value.EnergyWh >= 1000 ? $"{kvp.Value.EnergyWh / 1000.0:F2} kWh" : $"{kvp.Value.EnergyWh:F1} Wh";
            string tooltip = isEnergyMode
                ? $"{kvp.Value.DisplayName}\n⚡ {energyStr} ({percentage:F1}% of energy)\n⏱ {durationStr}"
                : $"{kvp.Value.DisplayName}\n⏱ {durationStr} ({percentage:F1}% of time)\n⚡ {energyStr}";

            var brush = new SolidColorBrush(palette[colorIndex % palette.Count]);

            treemapItems.Add(new TreemapItem
            {
                Name = TruncateName(kvp.Value.DisplayName),
                Value = val,
                Percentage = percentage,
                FormattedValue = formattedVal,
                EnergyWattHours = kvp.Value.EnergyWh,
                IsEnergyMode = isEnergyMode,
                TooltipText = tooltip,
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
