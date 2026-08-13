using UsageLogger.Core.Helpers;
using UsageLogger.Core.Models;
using Microsoft.UI.Xaml.Media;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace UsageLogger.Models
{
    public class AppUsageSubDetailItem : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private string _title;
        public string Title
        {
            get => _title;
            set { if (_title != value) { _title = value; OnPropertyChanged(); } }
        }

        public string FullTitle { get; set; }
        public string ParentProcessName { get; set; }
        public string GroupTitle { get; set; }

        private TimeSpan _duration;
        public TimeSpan Duration
        {
            get => _duration;
            set
            {
                if (_duration != value)
                {
                    _duration = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(StrDuration));
                }
            }
        }

        public string StrDuration => StringHelper.TimeSpanToString(Duration);

        private int _percentage;
        public int Percentage
        {
            get => _percentage;
            set { if (_percentage != value) { _percentage = value; OnPropertyChanged(); } }
        }

        public AppTag ItemTag { get; set; }

        private SolidColorBrush _tagIndicatorBrush;
        public SolidColorBrush TagIndicatorBrush
        {
            get => _tagIndicatorBrush;
            set { _tagIndicatorBrush = value; OnPropertyChanged(); }
        }

        private SolidColorBrush _tagTextBrush;
        public SolidColorBrush TagTextBrush
        {
            get => _tagTextBrush;
            set { _tagTextBrush = value; OnPropertyChanged(); }
        }

        private SolidColorBrush _backgroundBrush;
        public SolidColorBrush BackgroundBrush
        {
            get => _backgroundBrush;
            set { _backgroundBrush = value; OnPropertyChanged(); }
        }

        public AppUsageSubDetailItem(string title, string parentProcessName, string groupTitle, TimeSpan duration, int percentage, AppTag tag = AppTag.Untagged)
        {
            Title = title;
            FullTitle = title;
            ParentProcessName = parentProcessName;
            GroupTitle = groupTitle;
            _duration = duration;
            _percentage = percentage;
            ItemTag = tag;
        }
    }
}
