using UsageLogger.Core.Helpers;
using UsageLogger.Core.Models;
using UsageLogger.Helpers;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace UsageLogger.Models
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

        public ObservableCollection<AppUsageSubDetailItem> SubDetails { get; set; } = new ObservableCollection<AppUsageSubDetailItem>();

        public bool HasChildren => SubDetails != null && (SubDetails.Count > 1 || (SubDetails.Count == 1 && SubDetails[0].Title != Title));

        public int ItemCount => SubDetails?.Count ?? 0;

        public string ItemCountBadge => HasChildren ? $"{ItemCount}" : "";

        public string TooltipText
        {
            get
            {
                if (!HasChildren) return Title;
                var format = LocalizationHelper.GetString("SubApp_GroupedItemsTooltip");
                return !string.IsNullOrEmpty(format) ? string.Format(format, Title, ItemCount) : $"{Title} ({ItemCount} grouped items - click to ungroup)";
            }
        }

        private bool _isExpanded;
        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (_isExpanded != value)
                {
                    _isExpanded = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ChevronRotation));
                    OnPropertyChanged(nameof(UngroupButtonText));
                }
            }
        }

        private double _animatedHeight = 0;
        public double AnimatedHeight
        {
            get => _animatedHeight;
            set
            {
                if (Math.Abs(_animatedHeight - value) > 0.1)
                {
                    _animatedHeight = value;
                    OnPropertyChanged();
                }
            }
        }

        public double ChevronRotation => IsExpanded ? 180 : 0;

        public string UngroupButtonText => IsExpanded ? "Group" : "Ungroup";

        public ICommand ToggleExpandCommand { get; private set; }

        private DispatcherQueueTimer _animTimer;

        public AppUsageSubItem(string title, string parentProcessName, TimeSpan duration, int percentage, ImageSource icon, AppTag tag = AppTag.Untagged)
        {
            Title = title;
            ParentProcessName = parentProcessName;
            _duration = duration;  // Set backing field directly to avoid notification during construction
            _percentage = percentage;
            IconSource = icon;
            ItemTag = tag;

            ToggleExpandCommand = new RelayCommand((_) =>
            {
                ToggleExpand();
            });
        }

        public void ToggleExpand()
        {
            if (!HasChildren) return;
            IsExpanded = !IsExpanded;
            double target = IsExpanded ? Math.Min(600, Math.Max(40, SubDetails.Count * 36)) : 0;
            AnimateHeight(target);
        }

        public void NotifyDetailsChanged()
        {
            OnPropertyChanged(nameof(HasChildren));
            OnPropertyChanged(nameof(ItemCount));
            OnPropertyChanged(nameof(ItemCountBadge));
            OnPropertyChanged(nameof(TooltipText));
        }

        private void AnimateHeight(double targetHeight)
        {
            _animTimer?.Stop();

            bool expanding = targetHeight > _animatedHeight;
            double startHeight = _animatedHeight;
            double totalChange = targetHeight - startHeight;

            if (Math.Abs(totalChange) < 1)
            {
                AnimatedHeight = targetHeight;
                return;
            }

            int durationMs = expanding ? 260 : 200;
            var startTime = DateTime.UtcNow;

            var dispatcher = DispatcherQueue.GetForCurrentThread();
            if (dispatcher == null)
            {
                AnimatedHeight = targetHeight;
                return;
            }

            _animTimer = dispatcher.CreateTimer();
            _animTimer.Interval = TimeSpan.FromMilliseconds(8); // ~120fps target
            _animTimer.IsRepeating = true;

            _animTimer.Tick += (timer, _) =>
            {
                double elapsed = (DateTime.UtcNow - startTime).TotalMilliseconds;
                double t = Math.Min(elapsed / durationMs, 1.0);
                double eased = 1 - Math.Pow(1 - t, 3);

                AnimatedHeight = startHeight + totalChange * eased;

                if (t >= 1.0)
                {
                    AnimatedHeight = targetHeight;
                    timer.Stop();
                }
            };

            _animTimer.Start();
        }
    }
}
