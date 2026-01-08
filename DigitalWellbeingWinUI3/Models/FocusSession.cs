using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DigitalWellbeingWinUI3.Models
{
    /// <summary>
    /// Represents an enforcement level for a Focus Session.
    /// </summary>
    public enum FocusMode
    {
        /// <summary>
        /// Shows a toast notification when the user strays from the target app.
        /// </summary>
        Chill = 0,

        /// <summary>
        /// Shows an intrusive popup/overlay like the time limit dialog.
        /// </summary>
        Normal = 1,

        /// <summary>
        /// Aggressively closes any non-whitelisted app. DigitalWellbeing is always allowed.
        /// </summary>
        Focus = 2
    }

    /// <summary>
    /// Represents a scheduled focus block where the user commits to using a specific app.
    /// </summary>
    public class FocusSession : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public string Id { get; set; } = Guid.NewGuid().ToString();
        
        /// <summary>
        /// User-friendly name for this session (e.g., "Morning Coding", "Study Time").
        /// </summary>
        public string Name { get; set; } = "Focus Session";

        /// <summary>
        /// Time of day when this session starts (e.g., 14:00:00 for 2 PM).
        /// </summary>
        public TimeSpan StartTime { get; set; } = TimeSpan.FromHours(9);

        /// <summary>
        /// How long this session lasts (e.g., 2 hours).
        /// </summary>
        public TimeSpan Duration { get; set; } = TimeSpan.FromHours(1);

        /// <summary>
        /// Days of the week this session is active. Empty = one-time.
        /// </summary>
        public List<DayOfWeek> Days { get; set; } = new List<DayOfWeek>();

        /// <summary>
        /// The process name of the target app (e.g., "chrome", "Code").
        /// </summary>
        public string ProcessName { get; set; }

        /// <summary>
        /// Gets the user-friendly display name for the target app.
        /// </summary>
        public string DisplayName => Helpers.UserPreferences.GetDisplayName(ProcessName ?? "");

        /// <summary>
        /// Optional: Specific window title / sub-app (e.g., "GitHub", "YouTube").
        /// If null/empty, any window of the ProcessName is allowed.
        /// </summary>
        public string ProgramName { get; set; }

        /// <summary>
        /// The enforcement level for this session.
        /// </summary>
        public FocusMode Mode { get; set; } = FocusMode.Normal;

        private bool _isEnabled = true;
        /// <summary>
        /// Whether this session is currently active (user can toggle on/off).
        /// </summary>
        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (_isEnabled != value)
                {
                    _isEnabled = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Calculates whether the current time falls within this session.
        /// </summary>
        public bool IsActiveNow()
        {
            DateTime now = DateTime.Now;
            
            // Check day of week
            if (Days != null && Days.Count > 0 && !Days.Contains(now.DayOfWeek))
                return false;

            TimeSpan currentTime = now.TimeOfDay;
            TimeSpan endTime = StartTime + Duration;

            // Handle sessions that cross midnight
            if (endTime > TimeSpan.FromHours(24))
            {
                // Session wraps around midnight
                return currentTime >= StartTime || currentTime < (endTime - TimeSpan.FromHours(24));
            }

            return currentTime >= StartTime && currentTime < endTime;
        }

        /// <summary>
        /// Returns the end time of this session.
        /// </summary>
        public TimeSpan EndTime => StartTime + Duration;
    }
}
