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

        public ObservableCollection<DayTimelineViewModel> Days { get; } = new ObservableCollection<DayTimelineViewModel>();

        public DelegateCommand NextDayCommand { get; }
        public DelegateCommand PreviousDayCommand { get; }
        public DelegateCommand TodayCommand { get; }
        public DelegateCommand ZoomInCommand { get; }
        public DelegateCommand ZoomOutCommand { get; }

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
                    UpdateZoom();
                } 
            }
        }

        private double _totalAvailableWidth;
        public double TotalAvailableWidth
        {
            get => _totalAvailableWidth;
            set
            {
                if (_totalAvailableWidth != value)
                {
                    _totalAvailableWidth = value;
                    OnPropertyChanged();
                    UpdateColumnWidths();
                }
            }
        }

        private void UpdateColumnWidths()
        {
            if (Days.Count == 0 || _totalAvailableWidth <= 0) return;
            
            // Logic: Fill available space, but don't shrink below 350px per day
            double calculatedWidth = _totalAvailableWidth / Days.Count;
            if (calculatedWidth < 350) calculatedWidth = 350;
            
            // Push to child VMs
            foreach(var day in Days)
            {
                day.TimelineWidth = calculatedWidth;
            }
        }

        private DispatcherTimer _timer;

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

            LoadSessions();
        }

        private void UpdateZoom()
        {
            foreach (var day in Days)
            {
                day.SetZoom(PixelsPerHour);
            }
        }

        private void UpdateCurrentTime()
        {
            foreach (var day in Days)
            {
                day.UpdateCurrentTime(PixelsPerHour);
            }
        }

        public async void LoadSessions()
        {
            // Number of days setting
            int daysToShow = UserPreferences.DetailedUsageDayCount; 
            if (daysToShow < 1) daysToShow = 1;

            // Prepare VMs
            // If count matches, reuse VMs? Or clear and recreate?
            // Recreating is safer for Date changes.
            // If we are just refreshing data, we could reuse.
            // Let's clear for now to be safe.
            
            // However, cleaning clears the UI which might flicker. 
            // Better strategy: reusing if dates match?
            // For simplicity, clear.
            
            // We can optimize later if needed.
            
            // Fetch All data concurrently
            var tasks = new List<Task<(DateTime Date, List<AppSession> Sessions)>>();
            for (int i = 0; i < daysToShow; i++)
            {
                // Calculate date so that the last item (i = daysToShow - 1) is the SelectedDate
                // and previous items go back in time. Preserves Left->Right chronological order.
                DateTime targetDate = SelectedDate.Date.AddDays(i - (daysToShow - 1));
                
                tasks.Add(Task.Run(() => 
                {
                    return (targetDate, _repository.GetSessionsForDate(targetDate));
                }));
            }

            var results = await Task.WhenAll(tasks);

            // Update UI on UI Thread
            Days.Clear();
            foreach (var result in results)
            {
                var dayVM = new DayTimelineViewModel(result.Date);
                // Load sessions logic inside DayTimelineViewModel handles layout
                dayVM.LoadSessions(result.Sessions, PixelsPerHour);
                Days.Add(dayVM);
            }
            UpdateColumnWidths();
        }

        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));
        }
    }
}
