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
        private List<string> _allApps = new List<string>();
        private bool _isEditMode = false;

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
            _allApps = await FocusManager.Instance.GetHistoricalAppNamesAsync();
        }

        private void PopulateFromSession(FocusSession session)
        {
            NameBox.Text = session.Name ?? "";
            AppSuggestBox.Text = session.ProcessName ?? "";
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
            foreach (var item in ModeSelector.Items)
            {
                if (item is RadioButton rb && rb.Tag?.ToString() == session.Mode.ToString())
                {
                    rb.IsChecked = true;
                    break;
                }
            }
        }

        private void AppSuggestBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            {
                var query = sender.Text.ToLowerInvariant();
                var filtered = _allApps
                    .Where(a => a.ToLowerInvariant().Contains(query))
                    .Take(10)
                    .ToList();
                sender.ItemsSource = filtered;
            }
        }

        private void AppSuggestBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
        {
            sender.Text = args.SelectedItem?.ToString() ?? "";
        }

        private void PrimaryButton_Click(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            // Validate
            if (string.IsNullOrWhiteSpace(AppSuggestBox.Text))
            {
                args.Cancel = true;
                return;
            }

            // Build session
            if (_isEditMode && Session != null)
            {
                // Update existing
                Session.Name = string.IsNullOrWhiteSpace(NameBox.Text) ? "Focus Session" : NameBox.Text;
                Session.ProcessName = AppSuggestBox.Text.Trim();
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
                    ProcessName = AppSuggestBox.Text.Trim(),
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
            foreach (var item in ModeSelector.Items)
            {
                if (item is RadioButton rb && rb.IsChecked == true)
                {
                    var tag = rb.Tag?.ToString();
                    if (Enum.TryParse<FocusMode>(tag, out var mode))
                        return mode;
                }
            }
            return FocusMode.Normal;
        }
    }
}
