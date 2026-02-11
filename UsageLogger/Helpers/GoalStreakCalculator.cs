using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media;
using UsageLogger.Core.Models;
using UsageLogger.Helpers;
using UsageLogger.Models;
using Windows.UI;

namespace UsageLogger.ViewModels;

/// <summary>
/// Computes goal streak data (gaming, focus, social) from historical usage.
/// Extracted from AppUsageViewModel to separate computation from presentation.
/// </summary>
public static class GoalStreakCalculator
{
    /// <summary>
    /// Loads goal streaks looking back up to 30 days from yesterday.
    /// Updates the GoalStreaks collection on the dispatcher thread.
    /// </summary>
    public static async Task LoadAsync(
        ObservableCollection<GoalStreakItem> goalStreaks,
        DispatcherQueue dispatcherQueue)
    {
        await Task.Run(async () =>
        {
            int gamingStreak = 0;
            int focusStreak = 0;
            int socialStreak = 0;
            bool gFail = false;
            bool fFail = false;
            bool sFail = false;
            DateTime date = DateTime.Now.Date.AddDays(-1.0);

            for (int i = 0; i < 30; i++)
            {
                if (gFail && fFail && sFail) break;

                List<AppUsage> data = await AppUsageViewModel.GetData(date);
                TimeSpan gamingTime = TimeSpan.Zero;
                TimeSpan focusTime = TimeSpan.Zero;
                TimeSpan socialTime = TimeSpan.Zero;

                foreach (AppUsage item in data)
                {
                    if (!AppUsageViewModel.IsProcessExcluded(item.ProcessName))
                    {
                        AppTag tag = AppTagHelper.GetAppTag(item.ProcessName);
                        if (tag == AppTag.Game) gamingTime += item.Duration;
                        if (tag == AppTag.Work || tag == AppTag.Education) focusTime += item.Duration;
                        if (tag == AppTag.Social) socialTime += item.Duration;
                    }
                }

                if (!gFail)
                {
                    if (gamingTime.TotalHours < 1.0) gamingStreak++;
                    else gFail = true;
                }
                if (!fFail)
                {
                    if (focusTime.TotalHours >= 4.0) focusStreak++;
                    else fFail = true;
                }
                if (!sFail)
                {
                    if (socialTime.TotalMinutes < 30.0) socialStreak++;
                    else sFail = true;
                }
                date = date.AddDays(-1.0);
            }

            dispatcherQueue.TryEnqueue(() =>
            {
                goalStreaks.Clear();
                goalStreaks.Add(new GoalStreakItem
                {
                    Count = gamingStreak.ToString(),
                    Label = "days with < 1h Gaming",
                    IconColor = new SolidColorBrush(Color.FromArgb(255, 0, 120, 215))
                });
                goalStreaks.Add(new GoalStreakItem
                {
                    Count = focusStreak.ToString(),
                    Label = "days meeting Focus Goal (4h)",
                    IconColor = new SolidColorBrush(Color.FromArgb(255, 128, 0, 128))
                });
                goalStreaks.Add(new GoalStreakItem
                {
                    Count = socialStreak.ToString(),
                    Label = "days for < 30m Social Media",
                    IconColor = new SolidColorBrush(Color.FromArgb(255, 128, 128, 128))
                });
            });
        });
    }
}
