using DigitalWellbeing.Core.Helpers;
using DigitalWellbeing.Core.Models;
using Microsoft.UI.Xaml.Media;
using System;

namespace DigitalWellbeingWinUI3.Models
{
    public class AppUsageSubItem
    {
        public string Title { get; set; }
        public string ParentProcessName { get; set; } // Added for tagging context
        public TimeSpan Duration { get; set; }
        public string StrDuration { get => StringHelper.TimeSpanToString(Duration); }
        public int Percentage { get; set; } // Relative to parent
        public ImageSource IconSource { get; set; }
        public AppTag ItemTag { get; set; }
        public SolidColorBrush TagIndicatorBrush { get; set; }
        public SolidColorBrush TagTextBrush { get; set; }

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
