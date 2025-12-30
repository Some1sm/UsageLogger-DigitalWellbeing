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

        private string _title;
        public string Title 
        { 
            get => _title;
            set { if (_title != value) { _title = value; OnPropertyChanged(); } }
        }
        public string ParentProcessName { get; set; }
        
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
        
        public string StrDuration { get => StringHelper.TimeSpanToString(Duration); }
        
        private int _percentage;
        public int Percentage 
        { 
            get => _percentage;
            set { _percentage = value; OnPropertyChanged(); }
        }
        
        private ImageSource _iconSource;
        public ImageSource IconSource 
        { 
            get => _iconSource;
            set { if (_iconSource != value) { _iconSource = value; OnPropertyChanged(); } }
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

        public AppUsageSubItem(string title, string parentProcessName, TimeSpan duration, int percentage, ImageSource icon, AppTag tag = AppTag.Untagged)
        {
            Title = title;
            ParentProcessName = parentProcessName;
            _duration = duration;  // Set backing field directly to avoid notification during construction
            _percentage = percentage;
            IconSource = icon;
            ItemTag = tag;
        }
    }
}
