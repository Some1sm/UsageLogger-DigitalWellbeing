using Microsoft.UI.Xaml.Media;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DigitalWellbeingWinUI3.Models
{
    public class SessionBlock : INotifyPropertyChanged
    {
        public string Title { get; set; }
        public string DurationText { get; set; }
        public double Top { get; set; } // Canvas.Top
        public double Height { get; set; } // Height
        public double Left { get; set; } // Indentation for handling overlaps (simple version: 0)
        
        private double _width;
        public double Width 
        { 
            get => _width;
            set { if (_width != value) { _width = value; OnPropertyChanged(); } }
        }
        
        public Brush BackgroundColor { get; set; }
        public bool IsAfk { get; set; }
        public bool ShowDetails { get; set; } = true; // Optimization: Hide text on small blocks
        
        public string ProcessName { get; set; }
        public DigitalWellbeing.Core.Models.AppSession OriginalSession { get; set; }

        public System.Collections.Generic.List<string> AudioSources { get; set; } = new System.Collections.Generic.List<string>();
        public bool HasAudio => AudioSources != null && AudioSources.Count > 0;
        public string AudioSourcesText => HasAudio ? string.Join(", ", AudioSources) : "";
        public string TooltipText => $"{Title} ({DurationText}){(IsAfk ? " [AFK]" : "")}";

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
