using UsageLogger.Helpers;
using UsageLogger.Models;
using WinUI3Localizer;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace UsageLogger.Views
{
    public sealed partial class FocusPage : Page
    {
        public ObservableCollection<FocusSession> Sessions { get; } = new ObservableCollection<FocusSession>();

        public FocusPage()
        {
            this.InitializeComponent();
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            // Set XamlRoot for dialogs
            FocusManager.Instance.XamlRoot = this.XamlRoot;

            RefreshList();

            // Set toggle state
            MonitorToggle.IsOn = FocusManager.Instance.IsMonitoring;
            UpdateStatusText();
        }

        private void RefreshList()
        {
            Sessions.Clear();
            foreach (var session in FocusManager.Instance.Sessions)
            {
                Sessions.Add(session);
            }

            EmptyState.Visibility = Sessions.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            ScheduleList.Visibility = Sessions.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void UpdateStatusText()
        {
            if (FocusManager.Instance.IsMonitoring)
            {
                if (FocusManager.Instance.ActiveSession != null)
                {
                    var fmt = LocalizationHelper.GetString("FocusPageStatusActiveSession");
                    StatusText.Text = string.Format(fmt, FocusManager.Instance.ActiveSession.Name);
                }
                else
                {
                    StatusText.Text = LocalizationHelper.GetString("FocusPageStatusNoSession");
                }
            }
            else
            {
                StatusText.Text = LocalizationHelper.GetString("FocusPageStatusInactive");
            }
        }

        private void MonitorToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (MonitorToggle.IsOn)
            {
                FocusManager.Instance.StartMonitoring();
            }
            else
            {
                FocusManager.Instance.StopMonitoring();
            }
            UpdateStatusText();
        }

        private async void AddSchedule_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new FocusScheduleDialog();
            dialog.XamlRoot = this.XamlRoot;

            var result = await dialog.ShowAsync();

            if (result == ContentDialogResult.Primary && dialog.Session != null)
            {
                FocusManager.Instance.AddSession(dialog.Session);
                RefreshList();
            }
        }

        private async void EditSession_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string sessionId)
            {
                var session = FocusManager.Instance.Sessions.FirstOrDefault(s => s.Id == sessionId);
                if (session == null) return;

                var dialog = new FocusScheduleDialog(session);
                dialog.XamlRoot = this.XamlRoot;

                var result = await dialog.ShowAsync();

                if (result == ContentDialogResult.Primary && dialog.Session != null)
                {
                    FocusManager.Instance.UpdateSession(dialog.Session);
                    RefreshList();
                }
            }
        }

        private void DeleteSession_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string sessionId)
            {
                FocusManager.Instance.RemoveSession(sessionId);
                RefreshList();
            }
        }

        private void SessionToggle_Toggled(object sender, RoutedEventArgs e)
        {
            // Save when toggle changes
            FocusManager.Instance.Save();
        }
    }

    #region Converters
    public class FocusModeToIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is FocusMode mode)
            {
                return mode switch
                {
                    FocusMode.Chill => "\uE7BA",   // Volume/Sound icon
                    FocusMode.Normal => "\uE783", // Warning icon
                    FocusMode.Focus => "\uE72E",  // Shield icon
                    _ => "\uE823"
                };
            }
            return "\uE823";
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    public class FocusModeToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is FocusMode mode)
            {
                return mode switch
                {
                    FocusMode.Chill => new SolidColorBrush(Windows.UI.Color.FromArgb(255, 76, 175, 80)),   // Green
                    FocusMode.Normal => new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 152, 0)), // Orange
                    FocusMode.Focus => new SolidColorBrush(Windows.UI.Color.FromArgb(255, 244, 67, 54)),  // Red
                    _ => new SolidColorBrush(Colors.Gray)
                };
            }
            if (value is bool isActive)
            {
                return isActive 
                    ? new SolidColorBrush(Windows.UI.Color.FromArgb(255, 76, 175, 80))
                    : new SolidColorBrush(Colors.Gray);
            }
            return new SolidColorBrush(Colors.Gray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    #endregion
}
