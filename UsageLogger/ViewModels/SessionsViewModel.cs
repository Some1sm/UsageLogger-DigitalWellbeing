using UsageLogger.Core;
using UsageLogger.Core.Data;
using UsageLogger.Core.Models;
using UsageLogger.Helpers;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace UsageLogger.ViewModels
{
    public class SessionsViewModel : System.ComponentModel.INotifyPropertyChanged
    {
        private AppSessionRepository _repository;
        
        private DateTime _selectedDate = UsageLogger.Core.Helpers.DateHelper.GetLogicalToday();
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

        public bool CanGoNext => SelectedDate.Date < UsageLogger.Core.Helpers.DateHelper.GetLogicalToday();

        public ObservableCollection<DayTimelineViewModel> Days { get; } = new ObservableCollection<DayTimelineViewModel>();

        public RelayCommand NextDayCommand { get; }
        public RelayCommand PreviousDayCommand { get; }
        public RelayCommand TodayCommand { get; }
        public RelayCommand ZoomInCommand { get; }
        public RelayCommand ZoomOutCommand { get; }

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

        // View Mode Toggle (App, SubApp, Category)
        private readonly List<string> _viewModeKeys = new List<string> { "App", "SubApp", "Category" };
        public ObservableCollection<string> ViewModeDisplayOptions { get; } = new ObservableCollection<string>();
        
        private int _selectedViewModeIndex = 1; // Default to SubApp
        public int SelectedViewModeIndex
        {
            get => _selectedViewModeIndex;
            set
            {
                if (_selectedViewModeIndex != value)
                {
                    _selectedViewModeIndex = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ViewMode));
                    RefreshViewMode();
                }
            }
        }
        
        public string ViewMode => _viewModeKeys[_selectedViewModeIndex];
        
        private void RefreshViewMode()
        {
            foreach (var day in Days)
            {
                day.ViewMode = ViewMode;
                day.RefreshSessionLayout(PixelsPerHour);
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
            
            // Localize View Modes
            ViewModeDisplayOptions.Add(LocalizationHelper.GetString("Sessions_View_App"));
            ViewModeDisplayOptions.Add(LocalizationHelper.GetString("Sessions_View_SubApp"));
            ViewModeDisplayOptions.Add(LocalizationHelper.GetString("Sessions_View_Category"));

            NextDayCommand = new RelayCommand(_ => SelectedDate = SelectedDate.AddDays(1));
            PreviousDayCommand = new RelayCommand(_ => SelectedDate = SelectedDate.AddDays(-1));
            TodayCommand = new RelayCommand(_ => SelectedDate = UsageLogger.Core.Helpers.DateHelper.GetLogicalToday());
            
            ZoomInCommand = new RelayCommand(_ => 
            {
                if (PixelsPerHour < 3600) PixelsPerHour *= 1.5;
            });
            
            ZoomOutCommand = new RelayCommand(_ => 
            {
                if (PixelsPerHour > 40) PixelsPerHour /= 1.5;
            });

            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromMinutes(1);
            _timer.Tick += (s, e) => 
            {
                UpdateCurrentTime();
                RefreshCurrentView();
            };
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
            // Create fresh repository to pick up any path changes
            _repository = new AppSessionRepository(ApplicationPath.UsageLogsFolder);
            
            // Number of days setting
            int daysToShow = UserPreferences.DetailedUsageDayCount; 
            if (daysToShow < 1) daysToShow = 1;

            // Clear immediately to show "loading" state (empty list)
            // Or we could show a loading spinner property
            Days.Clear();
            
            // Refresh exclusion list from disk to pick up any Settings changes
            new AppUsageViewModel().LoadUserExcludedProcesses();
            
            // Fetch All data concurrently
            var tasks = new List<Task<(DateTime Date, List<AppSession> Sessions)>>();
            
            // Capture current selection to avoid race conditions if user clicks fast
            var currentSelection = SelectedDate;

            for (int i = 0; i < daysToShow; i++)
            {
                // Calculate date so that the last item (i = daysToShow - 1) is the SelectedDate
                // and previous items go back in time. Preserves Left->Right chronological order.
                DateTime targetDate = currentSelection.Date.AddDays(i - (daysToShow - 1));
                
                tasks.Add(Task.Run(async () => 
                {
                    var sessions = await _repository.GetSessionsForDateAsync(targetDate);
                    // Filter out excluded processes
                    sessions = sessions.Where(s => !AppUsageViewModel.IsProcessExcluded(s.ProcessName)).ToList();
                    return (targetDate, sessions);
                }));
            }

            // Await all background data fetching
            var results = await Task.WhenAll(tasks);

            // Double check if selection changed
            if (currentSelection.Date != SelectedDate.Date) return;

            // Merge with existing UI
            // This prevents clearing the list which causes UI flicker
            
            // 1. Remove days not in result (if day count changed or navigated far? usually navigate resets)
            // For simplicity in this specific "LoadSessions" triggered by navigations, we can clear if date changed significantly.
            // But for "Refresh" (timer), we want to keep instances.
            
            // Smart Merge:
            var newDaysDict = results.ToDictionary(r => r.Date.Date, r => r.Sessions);
            var existingDays = Days.ToList();

            // Remove old
            foreach(var day in existingDays)
            {
                if (!newDaysDict.ContainsKey(day.Date.Date))
                {
                    Days.Remove(day);
                }
            }

            // Add/Update
             foreach (var result in results)
            {
                var existingVM = Days.FirstOrDefault(d => d.Date.Date == result.Date.Date);
                if (existingVM != null)
                {
                    // Update in place
                    existingVM.ViewMode = ViewMode;
                    existingVM.LoadSessions(result.Sessions, PixelsPerHour);
                }
                else
                {
                    // Add new
                    var dayVM = new DayTimelineViewModel(result.Date);
                    dayVM.ViewMode = ViewMode;
                    dayVM.LoadSessions(result.Sessions, PixelsPerHour);
                    // Insert in order?
                    // Simple append if we assume chronological generation, but safer to insert.
                    Days.Add(dayVM); // Simplified for now, usually we want sorted
                }
            }
            
            // Sort Days if needed (ObservableCollection doesn't support Sort, so we rely on generation order or re-ordering)
            // Since we generate 'i' from 0 to N, results are ordered.
            // If we just appended, it might be wrong if mixed. 
            // Better: Clear if structure changed significantly, Update if just data.
            
           UpdateColumnWidths();
        }

        private async void RefreshCurrentView()
        {
            // Only refresh if looking at Today or recent days?
            // Usually we only care about Today updates.
            // Check if Today is visible in Days
            var todayVM = Days.FirstOrDefault(d => d.IsToday);
            if (todayVM != null)
            {
                var sessions = await _repository.GetSessionsForDateAsync(DateTime.Now);
                // Filter out excluded processes
                sessions = sessions.Where(s => !AppUsageViewModel.IsProcessExcluded(s.ProcessName)).ToList();
                todayVM.LoadSessions(sessions, PixelsPerHour);
            }
        }

        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));
        }
    }
}
