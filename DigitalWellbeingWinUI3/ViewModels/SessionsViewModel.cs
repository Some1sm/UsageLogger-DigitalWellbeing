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

            // Clear immediately to show "loading" state (empty list)
            // Or we could show a loading spinner property
            Days.Clear();
            
            // Fetch All data concurrently
            var tasks = new List<Task<(DateTime Date, List<AppSession> Sessions)>>();
            
            // Capture current selection to avoid race conditions if user clicks fast
            var currentSelection = SelectedDate;

            for (int i = 0; i < daysToShow; i++)
            {
                // Calculate date so that the last item (i = daysToShow - 1) is the SelectedDate
                // and previous items go back in time. Preserves Left->Right chronological order.
                DateTime targetDate = currentSelection.Date.AddDays(i - (daysToShow - 1));
                
                tasks.Add(Task.Run(() => 
                {
                    return (targetDate, _repository.GetSessionsForDate(targetDate));
                }));
            }

            // Await all background data fetching
            var results = await Task.WhenAll(tasks);

            // Double check if selection changed while loading (basic cancellation)
            if (currentSelection.Date != SelectedDate.Date) return;

            // Update UI on UI Thread
            foreach (var result in results)
            {
                var dayVM = new DayTimelineViewModel(result.Date);
                // Load sessions logic inside DayTimelineViewModel handles layout
                // We run this synchronous part on UI thread to ensure ViewModels are created safely
                // but the heavy lifting of sorting sessions was done in GetSessionsForDate (which we ran in background)
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
