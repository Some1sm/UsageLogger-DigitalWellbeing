using DigitalWellbeing.Core.Helpers;
using Microsoft.UI.Xaml.Media;
using System;

namespace DigitalWellbeingWinUI3.Models
{
    public class AppUsageSubItem
    {
        public string Title { get; set; }
        public TimeSpan Duration { get; set; }
        public string StrDuration { get => StringHelper.TimeSpanToString(Duration); }
        public int Percentage { get; set; } // Relative to parent
        public ImageSource IconSource { get; set; }

        public AppUsageSubItem(string title, TimeSpan duration, int percentage, ImageSource icon)
        {
            Title = title;
            Duration = duration;
            Percentage = percentage;
            IconSource = icon;
        }
    }
}
