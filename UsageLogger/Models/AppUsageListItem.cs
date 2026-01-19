using UsageLogger.Core.Helpers;
using UsageLogger.Core.Models;
using UsageLogger.Helpers;
using Microsoft.UI.Xaml.Media;
using System;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace UsageLogger.Models
{
    public class AppUsageListItem : INotifyPropertyChanged
    {
        private int _percentage;
        public int Percentage 
        { 
            get => _percentage; 
            set { if (_percentage != value) { _percentage = value; OnPropertyChanged(); } }
        }

        public string ProcessName { get; set; }
        public string ProgramName { get; set; }
        public string DisplayName => UserPreferences.GetDisplayName(ProcessName);
        
        private TimeSpan _duration;
        public TimeSpan Duration 
        { 
            get => _duration; 
            set { if (_duration != value) { _duration = value; OnPropertyChanged(); OnPropertyChanged(nameof(StrDuration)); } }
        }
        
        public string StrDuration { get => StringHelper.TimeSpanToString(Duration); }
        private ImageSource _iconSource;
        public ImageSource IconSource 
        { 
            get => _iconSource; 
            set { if (_iconSource != value) { _iconSource = value; OnPropertyChanged(); } } 
        }

        // ...

        public void Refresh()
        {
            OnPropertyChanged(nameof(DisplayName)); // Refresh display name from UserPreferences
            OnPropertyChanged(nameof(Percentage));
            OnPropertyChanged(nameof(Duration));
            OnPropertyChanged(nameof(StrDuration));

            OnPropertyChanged(nameof(_AppTag));
            OnPropertyChanged(nameof(StrAppTag));
            OnPropertyChanged(nameof(BrushAppTag));
            OnPropertyChanged(nameof(BrushAppTagBg));
            OnPropertyChanged(nameof(BrushAppTagInnerBg));
            
            // Reload Icon
            IconSource = IconManager.GetIconSource(ProcessName);
        }
        private AppTag _appTag;
        public AppTag _AppTag 
        { 
            get => _appTag; 
            set 
            { 
                if (_appTag != value) 
                { 
                    _appTag = value; 
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(StrAppTag));
                    OnPropertyChanged(nameof(BrushAppTag));
                    OnPropertyChanged(nameof(BrushAppTagBg));
                    OnPropertyChanged(nameof(BrushAppTagInnerBg));
                } 
            }
        }
        public string StrAppTag { get => AppTagHelper.GetTagDisplayName(this._AppTag); }
        public Brush BrushAppTag { get => AppTagHelper.GetTagColor(this._AppTag); }
        public Brush BrushAppTagBg 
        { 
            get 
            {
                var brush = AppTagHelper.GetTagColor(this._AppTag) as SolidColorBrush;
                if (brush != null)
                {
                    var c = brush.Color;
                    return new SolidColorBrush(Windows.UI.Color.FromArgb(25, c.R, c.G, c.B)); // ~10% Opacity (Card BG - Less Saturated)
                }
                return new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            } 
        }

        public Brush BrushAppTagInnerBg 
        { 
            get 
            {
                var brush = AppTagHelper.GetTagColor(this._AppTag) as SolidColorBrush;
                if (brush != null)
                {
                    var c = brush.Color;
                    return new SolidColorBrush(Windows.UI.Color.FromArgb(128, c.R, c.G, c.B)); // ~50% Opacity (Tag BG)
                }
                return new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            } 
        }

        public ObservableCollection<AppUsageSubItem> Children { get; set; } = new ObservableCollection<AppUsageSubItem>();
        
        public bool HasChildren => Children != null && Children.Count > 0;

        private bool _isExpanded;
        public bool IsExpanded
        {
            get => _isExpanded;
            set { if (_isExpanded != value) { _isExpanded = value; OnPropertyChanged(); OnPropertyChanged(nameof(ChevronRotation)); } }
        }

        public double ChevronRotation => IsExpanded ? 180 : 0;

        public ICommand ToggleExpandCommand { get; private set; }

        public AppUsageListItem(string processName, string programName, TimeSpan duration, int percentage, AppTag appTag)
        {
            ProcessName = processName;
            ProgramName = programName;
            Duration = duration;
            Percentage = percentage;
            // IconSource setter will fire notification, but that's fine/ignored in ctor
            IconSource = IconManager.GetIconSource(processName);
            _AppTag = appTag;
            
            ToggleExpandCommand = new RelayCommand((param) => IsExpanded = !IsExpanded);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] String propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

    }


}
