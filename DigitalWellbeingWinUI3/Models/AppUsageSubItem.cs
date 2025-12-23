using DigitalWellbeing.Core.Helpers;
using DigitalWellbeing.Core.Models;
using Microsoft.UI.Xaml.Media;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DigitalWellbeingWinUI3.Models
{
    public class AppUsageSubItem : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public string Title { get; set; }
        public string ParentProcessName { get; set; }
        public TimeSpan Duration { get; set; }
        public string StrDuration { get => StringHelper.TimeSpanToString(Duration); }
        public int Percentage { get; set; }
        public ImageSource IconSource { get; set; }
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

        public AppUsageSubItem(string title, string parentProcessName, TimeSpan duration, int percentage, ImageSource icon, AppTag tag = AppTag.Untagged)
        {
            Title = title;
            ParentProcessName = parentProcessName;
            Duration = duration;
            Percentage = percentage;
            IconSource = icon;
            ItemTag = tag;
        }
    }
}
