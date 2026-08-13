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
                    string headerFormat = Interruptions.Count == 1 
                        ? UsageLogger.Helpers.LocalizationHelper.GetString("Sessions_InterruptionHeader_Single") 
                        : UsageLogger.Helpers.LocalizationHelper.GetString("Sessions_InterruptionHeader_Plural");
                    
                    string totalDuration = UsageLogger.Core.Helpers.StringHelper.FormatDurationCompact(InterruptionDuration);
                    
                    if (!string.IsNullOrEmpty(headerFormat))
                    {
                        text += $"\n\n⚡ " + string.Format(headerFormat, Interruptions.Count, totalDuration);
                    }
                    else
                    {
                        text += $"\n\n⚡ {Interruptions.Count} brief interruption{(Interruptions.Count > 1 ? "s" : "")} (total {totalDuration}):";
                    }

                    string itemFormat = UsageLogger.Helpers.LocalizationHelper.GetString("Sessions_InterruptionItem");
                    foreach (var inter in Interruptions.Take(4))
                    {
                        string dur = UsageLogger.Core.Helpers.StringHelper.FormatDurationCompact(inter.Duration);
                        string time = inter.StartTime.ToString("HH:mm:ss");
                        if (!string.IsNullOrEmpty(itemFormat))
                        {
                            text += $"\n  • " + string.Format(itemFormat, inter.DisplayTitle, dur, time);
                        }
                        else
                        {
                            text += $"\n  • {inter.DisplayTitle} ({dur} at {time})";
                        }
                    }

                    if (Interruptions.Count > 4)
                    {
                        string moreFormat = UsageLogger.Helpers.LocalizationHelper.GetString("Sessions_InterruptionMore");
                        int remaining = Interruptions.Count - 4;
                        if (!string.IsNullOrEmpty(moreFormat))
                        {
                            text += $"\n  • " + string.Format(moreFormat, remaining);
                        }
                        else
                        {
                            text += $"\n  • ... +{remaining} more";
                        }
                    }
                }
                
                if (IsAfk)
                {
                    string afkLabel = UsageLogger.Helpers.LocalizationHelper.GetString("Sessions_Tooltip_AFK");
                    text += !string.IsNullOrEmpty(afkLabel) ? $"\n[{afkLabel}]" : "\n[AFK]";
                }
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
