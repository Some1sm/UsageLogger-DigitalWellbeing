using Microsoft.UI.Xaml.Media;
using System.Linq;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace UsageLogger.Models
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
        public UsageLogger.Core.Models.AppSession OriginalSession { get; set; }

        public System.Collections.Generic.List<string> AudioSources { get; set; } = new System.Collections.Generic.List<string>();
        public bool HasAudio => AudioSources != null && AudioSources.Count > 0;
        public string AudioSourcesText => HasAudio ? string.Join(", ", AudioSources) : "";
        
        public System.DateTime StartTime { get; set; }
        public System.DateTime EndTime { get; set; }

        public System.Collections.Generic.List<InterruptionRecord> Interruptions { get; set; } = new System.Collections.Generic.List<InterruptionRecord>();
        public bool HasInterruptions => Interruptions != null && Interruptions.Count > 0;
        public System.TimeSpan InterruptionDuration => System.TimeSpan.FromSeconds(Interruptions?.Sum(i => i.Duration.TotalSeconds) ?? 0);
        public System.TimeSpan NetActiveDuration => (EndTime - StartTime) - InterruptionDuration;

        public string TooltipText 
        {
            get
            {
                string text = Title;
                if (!string.IsNullOrEmpty(DurationText)) text += $" ({DurationText})";
                
                // Add exact time range on new line
                text += $"\n{StartTime:HH:mm:ss} - {EndTime:HH:mm:ss}";

                if (HasInterruptions)
                {
                    text += $"\n\n⚡ {Interruptions.Count} brief interruption{(Interruptions.Count > 1 ? "s" : "")} (total {UsageLogger.Core.Helpers.StringHelper.FormatDurationCompact(InterruptionDuration)}):";
                    foreach (var inter in Interruptions.Take(4))
                    {
                        text += $"\n  • {inter.DisplayTitle} ({UsageLogger.Core.Helpers.StringHelper.FormatDurationCompact(inter.Duration)} at {inter.StartTime:HH:mm:ss})";
                    }
                    if (Interruptions.Count > 4)
                    {
                        text += $"\n  • ... +{Interruptions.Count - 4} more";
                    }
                }
                
                if (IsAfk) text += "\n[AFK]";
                if (HasAudio) text += $"\n🔊 {AudioSourcesText}";
                return text;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    public class InterruptionRecord
    {
        public string ProcessName { get; set; }
        public string ProgramName { get; set; }
        public string DisplayTitle { get; set; }
        public System.DateTime StartTime { get; set; }
        public System.DateTime EndTime { get; set; }
        public System.TimeSpan Duration => EndTime - StartTime;
        public UsageLogger.Core.Models.AppTag Tag { get; set; }
        public Brush TagColor { get; set; }
    }
}
