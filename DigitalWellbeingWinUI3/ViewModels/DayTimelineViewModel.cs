using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using DigitalWellbeing.Core.Models;
using DigitalWellbeingWinUI3.Models;
using Microsoft.UI.Xaml;

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
                    UpdateLayoutWidths();
                }
            }
        }

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
            SessionBlocks.Clear();
            if (_cachedSessions == null) return;

            foreach (var s in _cachedSessions)
            {
                DateTime dayStart = Date.Date;
                DateTime dayEnd = dayStart.AddDays(1);
                
                DateTime validStart = s.StartTime < dayStart ? dayStart : s.StartTime;
                DateTime validEnd = s.EndTime > dayEnd ? dayEnd : s.EndTime;
                
                if (validEnd <= validStart) continue;

                double totalMinutesFromMidnight = (validStart - dayStart).TotalMinutes;
                double durationMinutes = (validEnd - validStart).TotalMinutes;

                double top = (totalMinutesFromMidnight / 60.0) * pixelsPerHour;
                double height = (durationMinutes / 60.0) * pixelsPerHour;
                
                if (height < 1) height = 1; 

                var color = DigitalWellbeingWinUI3.Helpers.AppTagHelper.GetTagColor(DigitalWellbeingWinUI3.Helpers.AppTagHelper.GetAppTag(s.ProcessName));

                SessionBlocks.Add(new SessionBlock
                {
                    Title = string.IsNullOrEmpty(s.ProgramName) ? s.ProcessName : s.ProgramName,
                    DurationText = $"{durationMinutes:F0}m",
                    Top = top,
                    Height = height,
                    Left = 0, 
                    Width = TimelineWidth, 
                    BackgroundColor = color,
                    IsAfk = s.IsAfk,
                    OriginalSession = s
                });
            }
            UpdateLayoutWidths(); 
        }

        private void RefreshGridLines(double pixelsPerHour)
        {
            GridLines.Clear();

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

                GridLines.Add(new TimeGridLine
                {
                    TimeText = text,
                    Height = rowHeight,
                    Top = (i / 60.0) * pixelsPerHour,
                    Opacity = i == 0 ? 0.0 : (isHour ? 0.3 : 0.1),
                    FontSize = isHour ? 12 : 10,
                    Width = TimelineWidth
                });
            }
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
                line.Width = TimelineWidth;
            }

            foreach (var block in SessionBlocks)
            {
                block.Width = TimelineWidth;
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
