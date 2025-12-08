using DigitalWellbeing.Core;
using DigitalWellbeing.Core.Data;
using DigitalWellbeing.Core.Models;
using DigitalWellbeingWinUI3.Helpers;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace DigitalWellbeingWinUI3.ViewModels
{
    public class SessionsViewModel : System.ComponentModel.INotifyPropertyChanged
    {
        private AppSessionRepository _repository;
        
        private DateTime _selectedDate = DateTime.Now;
        public DateTime SelectedDate
        {
            get => _selectedDate;
            set 
            {
                if (_selectedDate.Date != value.Date)
                {
                    _selectedDate = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(CanGoNext));
                    LoadSessions();
                }
            }
        }

        public bool CanGoNext => SelectedDate.Date < DateTime.Now.Date;

        private ObservableCollection<DigitalWellbeingWinUI3.Models.SessionBlock> _sessionBlocks = new ObservableCollection<DigitalWellbeingWinUI3.Models.SessionBlock>();
        public ObservableCollection<DigitalWellbeingWinUI3.Models.SessionBlock> SessionBlocks
        {
            get => _sessionBlocks;
            set { if (_sessionBlocks != value) { _sessionBlocks = value; OnPropertyChanged(); } }
        }

        public DelegateCommand NextDayCommand { get; }
        public DelegateCommand PreviousDayCommand { get; }
        public DelegateCommand TodayCommand { get; }
        public DelegateCommand ZoomInCommand { get; }
        public DelegateCommand ZoomOutCommand { get; }

        private double _currentTimeTop;
        public double CurrentTimeTop
        {
            get => _currentTimeTop;
            set { if (_currentTimeTop != value) { _currentTimeTop = value; OnPropertyChanged(); } }
        }

        private Microsoft.UI.Xaml.Visibility _currentTimeVisibility = Microsoft.UI.Xaml.Visibility.Collapsed;
        public Microsoft.UI.Xaml.Visibility CurrentTimeVisibility
        {
            get => _currentTimeVisibility;
            set { if (_currentTimeVisibility != value) { _currentTimeVisibility = value; OnPropertyChanged(); } }
        }

        private double _pixelsPerHour = 60.0;
        public double PixelsPerHour
        {
            get => _pixelsPerHour;
            set 
            { 
                if (_pixelsPerHour != value) 
                { 
                    _pixelsPerHour = value; 
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(CanvasHeight));
                    RefreshLayout();
                    RefreshGridLines();
                    UpdateCurrentTime();
                } 
            }
        }

        public double CanvasHeight => PixelsPerHour * 24;

        private DispatcherTimer _timer;
        private List<AppSession> _cachedSessions = new List<AppSession>();
        
        public ObservableCollection<DigitalWellbeingWinUI3.Models.TimeGridLine> GridLines { get; } = new ObservableCollection<DigitalWellbeingWinUI3.Models.TimeGridLine>();

        // Responsive Width
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
                    UpdateItemsWidth();
                } 
            }
        }

        public void UpdateLayoutWidths(double containerWidth)
        {
            if (containerWidth < 0) return;
            // Subtract label column width (60)
            double available = containerWidth - 60;
            if (available < 0) available = 0;
            
            TimelineWidth = available;
        }

        private void UpdateItemsWidth()
        {
            // Update GridLines width. They span both columns so effectively Full Width (Timeline + 60)
            double fullWidth = TimelineWidth + 60; 
            
            foreach (var line in GridLines)
            {
                line.Width = fullWidth;
            }
            
            foreach (var block in SessionBlocks)
            {
                block.Width = TimelineWidth;
            }
        }

        public SessionsViewModel()
        {
            _repository = new AppSessionRepository(ApplicationPath.UsageLogsFolder);
            NextDayCommand = new DelegateCommand(() => SelectedDate = SelectedDate.AddDays(1));
            PreviousDayCommand = new DelegateCommand(() => SelectedDate = SelectedDate.AddDays(-1));
            TodayCommand = new DelegateCommand(() => SelectedDate = DateTime.Now);
            
            ZoomInCommand = new DelegateCommand(() => 
            {
                if (PixelsPerHour < 3600) PixelsPerHour *= 1.5;
            });
            
            ZoomOutCommand = new DelegateCommand(() => 
            {
                if (PixelsPerHour > 40) PixelsPerHour /= 1.5;
            });

            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromMinutes(1);
            _timer.Tick += (s, e) => UpdateCurrentTime();
            _timer.Start();

            RefreshGridLines();
            LoadSessions();
        }

        private void RefreshGridLines()
        {
            GridLines.Clear();

            int stepMinutes = 60;
            if (PixelsPerHour > 1500) stepMinutes = 1;
            else if (PixelsPerHour > 720) stepMinutes = 5;
            else if (PixelsPerHour > 240) stepMinutes = 15;
            else if (PixelsPerHour > 120) stepMinutes = 30;

            int totalMinutes = 24 * 60;
            double pixelsPerMinute = PixelsPerHour / 60.0;
            double rowHeight = stepMinutes * pixelsPerMinute;
            
            double fullWidth = TimelineWidth + 60;

            for (int i = 0; i < totalMinutes; i += stepMinutes)
            {
                TimeSpan ts = TimeSpan.FromMinutes(i);
                string text = "";

                bool isHour = (i % 60) == 0;
                
                if (isHour) text = ts.ToString(@"hh\:mm");
                else 
                {
                    if (rowHeight > 20) text = ts.ToString(@"mm");
                }

                GridLines.Add(new DigitalWellbeingWinUI3.Models.TimeGridLine
                {
                    TimeText = text,
                    Height = rowHeight,
                    Top = (i / 60.0) * _pixelsPerHour,
                    Opacity = isHour ? 0.3 : 0.1,
                    FontSize = isHour ? 12 : 10,
                    Width = fullWidth
                });
            }
        }

        private void UpdateCurrentTime()
        {
            if (SelectedDate.Date == DateTime.Now.Date)
            {
                CurrentTimeVisibility = Microsoft.UI.Xaml.Visibility.Visible;
                double totalMinutes = DateTime.Now.TimeOfDay.TotalMinutes;
                CurrentTimeTop = (totalMinutes / 60.0) * PixelsPerHour; 
            }
            else
            {
                CurrentTimeVisibility = Microsoft.UI.Xaml.Visibility.Collapsed;
            }
        }

        public async void LoadSessions()
        {
            _cachedSessions.Clear();
            List<AppSession> rawSessions = new List<AppSession>();

            await Task.Run(() =>
            {
                rawSessions = _repository.GetSessionsForDate(SelectedDate.Date);
            });

            _cachedSessions = rawSessions;
            RefreshLayout();
            UpdateCurrentTime();
        }

        private void RefreshLayout()
        {
            SessionBlocks.Clear();
            
            foreach (var s in _cachedSessions)
            {
                DateTime dayStart = SelectedDate.Date;
                DateTime dayEnd = dayStart.AddDays(1);
                
                DateTime validStart = s.StartTime < dayStart ? dayStart : s.StartTime;
                DateTime validEnd = s.EndTime > dayEnd ? dayEnd : s.EndTime;
                
                if (validEnd <= validStart) continue;

                double totalMinutesFromMidnight = (validStart - dayStart).TotalMinutes;
                double durationMinutes = (validEnd - validStart).TotalMinutes;

                double top = (totalMinutesFromMidnight / 60.0) * PixelsPerHour;
                double height = (durationMinutes / 60.0) * PixelsPerHour;
                
                if (height < 1) height = 1; 

                var color = AppTagHelper.GetTagColor(AppTagHelper.GetAppTag(s.ProcessName));

                SessionBlocks.Add(new DigitalWellbeingWinUI3.Models.SessionBlock
                {
                    Title = string.IsNullOrEmpty(s.ProgramName) ? s.ProcessName : s.ProgramName,
                    DurationText = $"{durationMinutes:F0}m",
                    Top = top,
                    Height = height,
                    Left = 60, // Space for labels
                    Width = TimelineWidth,
                    BackgroundColor = color,
                    IsAfk = s.IsAfk,
                    OriginalSession = s
                });
            }
        }

        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));
        }
    }
}
