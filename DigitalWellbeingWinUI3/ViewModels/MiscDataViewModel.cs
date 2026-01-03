using DigitalWellbeing.Core;
using DigitalWellbeing.Core.Data;
using DigitalWellbeing.Core.Models;
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
    /// Computes various statistics from session data.
    /// </summary>
    public class MiscDataViewModel : INotifyPropertyChanged
    {
        private readonly AppSessionRepository _repository;

        public MiscDataViewModel()
        {
            _repository = new AppSessionRepository(ApplicationPath.UsageLogsFolder);
            Stats = new ObservableCollection<StatItem>();
        }

        /// <summary>
        /// Collection of stat items to display.
        /// </summary>
        public ObservableCollection<StatItem> Stats { get; }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        /// <summary>
        /// Loads and computes all statistics for today.
        /// </summary>
        public async Task LoadDataAsync()
        {
            Stats.Clear();

            var sessions = await Task.Run(() => _repository.GetSessionsForDate(DateTime.Today));
            if (sessions == null || sessions.Count == 0)
            {
                Stats.Add(new StatItem("\uE8B7", "No Data", "Start using your PC to see stats!", ""));
                return;
            }

            // Sort by StartTime for edge detection
            var sorted = sessions.OrderBy(s => s.StartTime).ToList();

            // 1. Window Switches (non-AFK sessions count)
            int windowSwitches = sorted.Count(s => !s.IsAfk);
            Stats.Add(new StatItem("\uE8A5", windowSwitches.ToString("N0"), "Window Switches", "Focus changes today"));

            // 2. Audio Plays (Rising Edge: No Audio -> Audio)
            int audioPlays = 0;
            bool prevHadAudio = false;
            foreach (var s in sorted)
            {
                bool currHasAudio = s.AudioSources != null && s.AudioSources.Count > 0;
                if (currHasAudio && !prevHadAudio)
                {
                    audioPlays++;
                }
                prevHadAudio = currHasAudio;
            }
            Stats.Add(new StatItem("\uE995", audioPlays.ToString("N0"), "Audio Events", "Times audio started playing"));

            // 3. AFK Time
            var afkTime = TimeSpan.FromSeconds(sorted.Where(s => s.IsAfk).Sum(s => s.Duration.TotalSeconds));
            Stats.Add(new StatItem("\uE916", FormatDuration(afkTime), "AFK Time", "Time spent away from keyboard"));

            // 4. Active Time (non-AFK)
            var activeTime = TimeSpan.FromSeconds(sorted.Where(s => !s.IsAfk).Sum(s => s.Duration.TotalSeconds));
            Stats.Add(new StatItem("\uE770", FormatDuration(activeTime), "Active Time", "Time actively using PC"));

            // 5. Longest Session
            var longest = sorted.Where(s => !s.IsAfk).OrderByDescending(s => s.Duration).FirstOrDefault();
            if (longest != null)
            {
                string appName = string.IsNullOrEmpty(longest.ProgramName) ? longest.ProcessName : longest.ProgramName;
                if (appName.Length > 20) appName = appName.Substring(0, 20) + "...";
                Stats.Add(new StatItem("\uE768", FormatDuration(longest.Duration), "Longest Session", appName));
            }

            // 6. Most Used App
            var topApp = sorted
                .Where(s => !s.IsAfk)
                .GroupBy(s => s.ProcessName)
                .OrderByDescending(g => g.Sum(s => s.Duration.TotalSeconds))
                .FirstOrDefault();
            if (topApp != null)
            {
                var topDuration = TimeSpan.FromSeconds(topApp.Sum(s => s.Duration.TotalSeconds));
                Stats.Add(new StatItem("\uE734", FormatDuration(topDuration), "Top App", topApp.Key));
            }

            // 7. Unique Apps Used
            int uniqueApps = sorted.Where(s => !s.IsAfk).Select(s => s.ProcessName).Distinct().Count();
            Stats.Add(new StatItem("\uE74C", uniqueApps.ToString(), "Unique Apps", "Different applications used"));

            // 8. Peak Hour (Hour with most sessions)
            var peakHour = sorted
                .Where(s => !s.IsAfk)
                .GroupBy(s => s.StartTime.Hour)
                .OrderByDescending(g => g.Count())
                .FirstOrDefault();
            if (peakHour != null)
            {
                string hourStr = DateTime.Today.AddHours(peakHour.Key).ToString("h tt");
                Stats.Add(new StatItem("\uE823", hourStr, "Busiest Hour", $"{peakHour.Count()} sessions"));
            }

            // 9. Top Website (from browsers)
            var browserProcesses = new HashSet<string>(StringComparer.OrdinalIgnoreCase) 
            { 
                "chrome", "msedge", "firefox", "opera", "brave", "vivaldi", "arc"
            };
            var browserSessions = sorted
                .Where(s => !s.IsAfk && browserProcesses.Contains(s.ProcessName.ToLowerInvariant()))
                .Where(s => !string.IsNullOrEmpty(s.ProgramName) && s.ProgramName != s.ProcessName)
                .GroupBy(s => s.ProgramName)
                .OrderByDescending(g => g.Sum(s => s.Duration.TotalSeconds))
                .FirstOrDefault();
            if (browserSessions != null)
            {
                var siteDuration = TimeSpan.FromSeconds(browserSessions.Sum(s => s.Duration.TotalSeconds));
                string siteName = browserSessions.Key;
                if (siteName.Length > 25) siteName = siteName.Substring(0, 25) + "...";
                Stats.Add(new StatItem("\uE774", FormatDuration(siteDuration), "Top Website", siteName));
            }

            // 10. Top Category (by AppTag)
            var categoryDurations = sorted
                .Where(s => !s.IsAfk)
                .GroupBy(s => Helpers.AppTagHelper.GetAppTag(s.ProcessName))
                .Where(g => g.Key != DigitalWellbeing.Core.Models.AppTag.Untagged)
                .Select(g => new { Tag = g.Key, Duration = TimeSpan.FromSeconds(g.Sum(s => s.Duration.TotalSeconds)) })
                .OrderByDescending(x => x.Duration)
                .FirstOrDefault();
            if (categoryDurations != null)
            {
                Stats.Add(new StatItem("\uE8EC", Helpers.AppTagHelper.GetTagDisplayName(categoryDurations.Tag), "Top Category", FormatDuration(categoryDurations.Duration)));
            }
        }

        private string FormatDuration(TimeSpan t)
        {
            if (t.TotalHours >= 1)
                return $"{(int)t.TotalHours}h {t.Minutes}m";
            else if (t.TotalMinutes >= 1)
                return $"{t.Minutes}m {t.Seconds}s";
            else
                return $"{t.Seconds}s";
        }
    }

    /// <summary>
    /// Represents a single stat card.
    /// </summary>
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
