using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Linq;
using DigitalWellbeing.Core.Models;
using DigitalWellbeingWinUI3.Models;
using DigitalWellbeingWinUI3.Helpers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using SkiaSharp;

namespace DigitalWellbeingWinUI3.ViewModels
{
    public class DayTimelineViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        
        public DateTime Date { get; private set; }
        public string DateString => Date.ToString("D"); // Full date format

        private double _timelineWidth;
        public double TimelineWidth
        {
            get => _timelineWidth;
            set
            {
                if (_timelineWidth != value)
                {
                    _timelineWidth = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ContentWidth)); // Notify derived property
                    UpdateLayoutWidths();
                }
            }
        }

        public double ContentWidth => TimelineWidth - 30.0 > 0 ? TimelineWidth - 30.0 : 0;

        private double _canvasHeight = 1440; // Default 24h
        public double CanvasHeight
        {
            get => _canvasHeight;
            set { _canvasHeight = value; OnPropertyChanged(); }
        }

        public ObservableCollection<TimeGridLine> GridLines { get; set; } = new ObservableCollection<TimeGridLine>();
        public ObservableCollection<SessionBlock> SessionBlocks { get; set; } = new ObservableCollection<SessionBlock>();

        // Current Time Indicator (Red Line) - Only relevant if Today
        private bool _isToday;
        public bool IsToday
        {
            get => _isToday;
            set { _isToday = value; OnPropertyChanged(); UpdateCurrentTimeVisibility(); }
        }

        private Visibility _currentTimeVisibility = Visibility.Collapsed;
        public Visibility CurrentTimeVisibility
        {
            get => _currentTimeVisibility;
            set { _currentTimeVisibility = value; OnPropertyChanged(); }
        }

        private double _currentTimeTop;
        public double CurrentTimeTop
        {
            get => _currentTimeTop;
            set { _currentTimeTop = value; OnPropertyChanged(); }
        }

        public DayTimelineViewModel(DateTime date)
        {
            Date = date;
            IsToday = Date.Date == DateTime.Now.Date;
            // Initialize with default zoom
            RefreshGridLines(60.0);
        }

        public void SetZoom(double pixelsPerHour)
        {
            CanvasHeight = pixelsPerHour * 24;
            RefreshGridLines(pixelsPerHour);
            
            if (_cachedSessions != null)
            {
                RefreshSessionLayout(pixelsPerHour);
            }
            UpdateCurrentTime(pixelsPerHour);
        }

        private List<AppSession> _cachedSessions;

        public void LoadSessions(List<AppSession> sessions, double pixelsPerHour)
        {
            _cachedSessions = sessions;
            RefreshSessionLayout(pixelsPerHour);
            UpdateCurrentTime(pixelsPerHour);
        }

        private void RefreshSessionLayout(double pixelsPerHour)
        {
            if (_cachedSessions == null) return;

            var newBlocks = new ObservableCollection<SessionBlock>();
            // Force GroupBy ProcessName in Incognito mode to merge fragmented sessions
            var processGroups = _cachedSessions.GroupBy(s => 
                UserPreferences.IncognitoMode 
                    ? s.ProcessName 
                    : (!string.IsNullOrEmpty(s.ProgramName) ? s.ProgramName : s.ProcessName)
            );
            var mergedBlocks = new List<SessionBlock>();

            foreach (var group in processGroups)
            {
                var sortedGroup = group.OrderBy(s => s.StartTime).ToList();
                SessionBlock pendingBlock = null;
                DateTime? lastEnd = null;

                foreach (var s in sortedGroup)
                {
                    DateTime dayStart = Date.Date;
                    DateTime dayEnd = dayStart.AddDays(1);
                    
                    DateTime validStart = s.StartTime < dayStart ? dayStart : s.StartTime;
                    DateTime validEnd = s.EndTime > dayEnd ? dayEnd : s.EndTime;
                    
                    if (validEnd <= validStart) continue;

                    // In Incognito mode, always use ProcessName for the label
                    string title = UserPreferences.IncognitoMode 
                        ? s.ProcessName 
                        : (string.IsNullOrEmpty(s.ProgramName) ? s.ProcessName : s.ProgramName);
                    
                    // Check for sub-app specific tag first, fallback to parent process tag
                    AppTag tag;
                    if (!string.IsNullOrEmpty(s.ProgramName))
                    {
                        int? titleTagId = DigitalWellbeingWinUI3.Helpers.SettingsManager.GetTitleTagId(s.ProcessName, s.ProgramName);
                        if (titleTagId.HasValue)
                            tag = (AppTag)titleTagId.Value;
                        else
                            tag = DigitalWellbeingWinUI3.Helpers.AppTagHelper.GetAppTag(s.ProcessName);
                    }
                    else
                    {
                        tag = DigitalWellbeingWinUI3.Helpers.AppTagHelper.GetAppTag(s.ProcessName);
                    }
                    
                    var baseBrush = DigitalWellbeingWinUI3.Helpers.AppTagHelper.GetTagColor(tag) as SolidColorBrush;
                    Brush color = baseBrush;

                    if (baseBrush != null)
                    {
                        var c = baseBrush.Color;
                        // Apply Tint: Opacity 64 (~25%)
                        var tintedColor = Windows.UI.Color.FromArgb(64, c.R, c.G, c.B);
                        color = new SolidColorBrush(tintedColor);
                    }

                    bool isCompatible = false;
                    if (pendingBlock != null)
                    {
                        // Check Time threshold
                        int threshold = DigitalWellbeingWinUI3.Helpers.UserPreferences.TimelineMergeThresholdSeconds;
                        if (lastEnd.HasValue && (validStart - lastEnd.Value).TotalSeconds < threshold)
                        {
                            // Check Audio Compatibility
                            var pendingAudio = pendingBlock.AudioSources ?? new List<string>();
                            var currentAudio = s.AudioSources ?? new List<string>();
                            
                            bool audioEqual = false;
                            if (pendingAudio.Count == currentAudio.Count)
                            {
                                var set1 = new HashSet<string>(pendingAudio);
                                var set2 = new HashSet<string>(currentAudio);
                                if (set1.SetEquals(set2)) audioEqual = true;
                            }

                            if (audioEqual && pendingBlock.IsAfk == s.IsAfk)
                            {
                                isCompatible = true;
                            }
                        }
                    }

                    if (isCompatible)
                    {
                        // EXTEND
                        double newEndMinutes = (validEnd - dayStart).TotalMinutes;
                        double newHeight = (newEndMinutes * pixelsPerHour / 60.0) - pendingBlock.Top;
                        if (newHeight < 1) newHeight = 1;
                        pendingBlock.Height = newHeight;
                        
                         lastEnd = validEnd;
                    }
                    else
                    {
                        // FINALIZE
                        if (pendingBlock != null) mergedBlocks.Add(pendingBlock);

                        // NEW
                        double startMinutes = (validStart - dayStart).TotalMinutes;
                        double top = startMinutes * pixelsPerHour / 60.0;
                        double durationMinutes = (validEnd - validStart).TotalMinutes;
                        double height = durationMinutes * pixelsPerHour / 60.0;
                        if (height < 1) height = 1;

                        pendingBlock = new SessionBlock
                        {
                            Top = top,
                            Height = height,
                            Left = 0, // Computed later
                            Width = ContentWidth,
                            Title = title, // Keep first title? Or generic? First is fine.
                            ProcessName = s.ProcessName,
                            BackgroundColor = color,
                            OriginalSession = s,
                            IsAfk = s.IsAfk,
                            ShowDetails = height > 20,
                            AudioSources = s.AudioSources != null ? new List<string>(s.AudioSources) : new List<string>()
                        };
                        lastEnd = validEnd;
                    }
                }
                if (pendingBlock != null) mergedBlocks.Add(pendingBlock);
            }
            
            // Add all merged blocks to the observable collection
            // Sort by Top to maintain approximate chronological visual order (though overlaps will happen)
            newBlocks.Clear(); // Clear any existing blocks from previous logic
            foreach (var block in mergedBlocks.OrderBy(b => b.Top))
            {
               // 2-minute threshold rule for AFK visualization
               // Height = Minutes * (PPH / 60)  =>  Minutes = Height * 60 / PPH
               double durationMin = block.Height * 60.0 / pixelsPerHour;
               if (block.IsAfk && durationMin < 2.0) 
               {
                   block.IsAfk = false;
               }

               newBlocks.Add(block);
            }
            SessionBlocks = newBlocks;
            OnPropertyChanged(nameof(SessionBlocks));
            UpdateLayoutWidths(); 
        }

        private void RefreshGridLines(double pixelsPerHour)
        {
            var newLines = new ObservableCollection<TimeGridLine>();

            int stepMinutes = 60;
            if (pixelsPerHour > 1500) stepMinutes = 1;
            else if (pixelsPerHour > 720) stepMinutes = 5;
            else if (pixelsPerHour > 240) stepMinutes = 15;
            else if (pixelsPerHour > 120) stepMinutes = 30;

            int totalMinutes = 24 * 60;
            double pixelsPerMinute = pixelsPerHour / 60.0;
            double rowHeight = stepMinutes * pixelsPerMinute;

            for (int i = 0; i < totalMinutes; i += stepMinutes)
            {
                TimeSpan ts = TimeSpan.FromMinutes(i);
                string text = "";
                bool isHour = (i % 60) == 0;
                
                if (isHour) text = ts.ToString(@"hh\:mm");
                else if (rowHeight > 20) text = ts.ToString(@"mm");

                newLines.Add(new TimeGridLine
                {
                    TimeText = text,
                    Height = rowHeight,
                    Top = (i / 60.0) * pixelsPerHour,
                    Opacity = i == 0 ? 0.0 : (isHour ? 0.3 : 0.1),
                    FontSize = isHour ? 12 : 10,
                    Width = ContentWidth
                });
            }
            GridLines = newLines;
            OnPropertyChanged(nameof(GridLines));
        }
        
        public void UpdateCurrentTime(double pixelsPerHour)
        {
            if (IsToday)
            {
                var now = DateTime.Now;
                double totalMinutes = now.TimeOfDay.TotalMinutes;
                CurrentTimeTop = (totalMinutes / 60.0) * pixelsPerHour;
                CurrentTimeVisibility = Visibility.Visible;
            }
            else
            {
                CurrentTimeVisibility = Visibility.Collapsed;
            }
        }

        private void UpdateLayoutWidths()
        {
            foreach (var line in GridLines)
            {
                line.Width = ContentWidth;
            }

            foreach (var block in SessionBlocks)
            {
                block.Width = ContentWidth;
            }
        }

        private void UpdateCurrentTimeVisibility()
        {
            CurrentTimeVisibility = IsToday ? Visibility.Visible : Visibility.Collapsed;
        }

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
