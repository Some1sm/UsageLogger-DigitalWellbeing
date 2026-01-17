using DigitalWellbeingWinUI3.Helpers;
using DigitalWellbeingWinUI3.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DigitalWellbeingWinUI3.Views
{
    public sealed partial class FocusScheduleDialog : ContentDialog
    {
        public FocusSession Session { get; private set; }
        private List<AppSuggestionItem> _allApps = new List<AppSuggestionItem>();
        private bool _isEditMode = false;
        private string _selectedProcessName = null;

        public FocusScheduleDialog()
        {
            this.InitializeComponent();
            LoadApps();
        }

        public FocusScheduleDialog(FocusSession existingSession) : this()
        {
            _isEditMode = true;
            Session = existingSession;
            PopulateFromSession(existingSession);
        }

        private async void LoadApps()
        {
            var processNames = await FocusManager.Instance.GetHistoricalAppNamesAsync();
            _allApps = processNames.Select(p => new AppSuggestionItem
            {
                ProcessName = p,
                DisplayName = UserPreferences.GetDisplayName(p)
            }).ToList();
        }

        private void PopulateFromSession(FocusSession session)
        {
            NameBox.Text = session.Name ?? "";
            // Show display name but store process name
            _selectedProcessName = session.ProcessName;
            AppSuggestBox.Text = UserPreferences.GetDisplayName(session.ProcessName ?? "");
            SubAppBox.Text = session.ProgramName ?? "";
            StartTimePicker.Time = session.StartTime;
            DurationBox.Value = session.Duration.TotalMinutes;

            // Days
            if (session.Days != null)
            {
                SunToggle.IsChecked = session.Days.Contains(DayOfWeek.Sunday);
                MonToggle.IsChecked = session.Days.Contains(DayOfWeek.Monday);
                TueToggle.IsChecked = session.Days.Contains(DayOfWeek.Tuesday);
                WedToggle.IsChecked = session.Days.Contains(DayOfWeek.Wednesday);
                ThuToggle.IsChecked = session.Days.Contains(DayOfWeek.Thursday);
                FriToggle.IsChecked = session.Days.Contains(DayOfWeek.Friday);
                SatToggle.IsChecked = session.Days.Contains(DayOfWeek.Saturday);
            }

            // Mode
            ModeSelector.SelectedIndex = (int)session.Mode;
        }

        private void AppSuggestBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            {
                _selectedProcessName = null; // Clear selection when user types
                var query = sender.Text.ToLowerInvariant();
                var filtered = _allApps
                    .Where(a => a.DisplayName.ToLowerInvariant().Contains(query) || 
                                a.ProcessName.ToLowerInvariant().Contains(query))
                    .Take(10)
                    .ToList();
                sender.ItemsSource = filtered;
            }
        }

        private void AppSuggestBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
        {
            if (args.SelectedItem is AppSuggestionItem item)
            {
                _selectedProcessName = item.ProcessName;
                sender.Text = item.DisplayName;
            }
        }

        private void PrimaryButton_Click(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            // Get the process name - either from selection or try to find a match
            string processName = _selectedProcessName;
            if (string.IsNullOrEmpty(processName))
            {
                // User typed something manually - try to find a matching app
                var match = _allApps.FirstOrDefault(a => 
                    a.DisplayName.Equals(AppSuggestBox.Text.Trim(), StringComparison.OrdinalIgnoreCase) ||
                    a.ProcessName.Equals(AppSuggestBox.Text.Trim(), StringComparison.OrdinalIgnoreCase));
                processName = match?.ProcessName ?? AppSuggestBox.Text.Trim();
            }

            // Validate
            if (string.IsNullOrWhiteSpace(processName))
            {
                args.Cancel = true;
                return;
            }

            // Build session
            if (_isEditMode && Session != null)
            {
                // Update existing
                Session.Name = string.IsNullOrWhiteSpace(NameBox.Text) ? "Focus Session" : NameBox.Text;
                Session.ProcessName = processName;
                Session.ProgramName = string.IsNullOrWhiteSpace(SubAppBox.Text) ? null : SubAppBox.Text.Trim();
                Session.StartTime = StartTimePicker.Time;
                Session.Duration = TimeSpan.FromMinutes(DurationBox.Value);
                Session.Days = GetSelectedDays();
                Session.Mode = GetSelectedMode();
            }
            else
            {
                // Create new
                Session = new FocusSession
                {
                    Name = string.IsNullOrWhiteSpace(NameBox.Text) ? "Focus Session" : NameBox.Text,
                    ProcessName = processName,
                    ProgramName = string.IsNullOrWhiteSpace(SubAppBox.Text) ? null : SubAppBox.Text.Trim(),
                    StartTime = StartTimePicker.Time,
                    Duration = TimeSpan.FromMinutes(DurationBox.Value),
                    Days = GetSelectedDays(),
                    Mode = GetSelectedMode(),
                    IsEnabled = true
                };
            }
        }

        private List<DayOfWeek> GetSelectedDays()
        {
            var days = new List<DayOfWeek>();
            if (SunToggle.IsChecked == true) days.Add(DayOfWeek.Sunday);
            if (MonToggle.IsChecked == true) days.Add(DayOfWeek.Monday);
            if (TueToggle.IsChecked == true) days.Add(DayOfWeek.Tuesday);
            if (WedToggle.IsChecked == true) days.Add(DayOfWeek.Wednesday);
            if (ThuToggle.IsChecked == true) days.Add(DayOfWeek.Thursday);
            if (FriToggle.IsChecked == true) days.Add(DayOfWeek.Friday);
            if (SatToggle.IsChecked == true) days.Add(DayOfWeek.Saturday);
            return days;
        }

        private FocusMode GetSelectedMode()
        {
            if (ModeSelector.SelectedIndex >= 0 && ModeSelector.SelectedIndex <= 2)
            {
                return (FocusMode)ModeSelector.SelectedIndex;
            }
            
            return FocusMode.Normal;
        }
    }

    /// <summary>
    /// Helper class for app suggestions that holds both process name and display name.
    /// </summary>
    public class AppSuggestionItem
    {
        public string ProcessName { get; set; }
        public string DisplayName { get; set; }

        public override string ToString() => DisplayName;
    }
}

