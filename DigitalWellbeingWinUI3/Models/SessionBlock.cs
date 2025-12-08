using Microsoft.UI.Xaml.Media;

namespace DigitalWellbeingWinUI3.Models
{
    public class SessionBlock
    {
        public string Title { get; set; }
        public string DurationText { get; set; }
        public double Top { get; set; } // Canvas.Top
        public double Height { get; set; } // Height
        public double Left { get; set; } // Indentation for handling overlaps (simple version: 0)
        public double Width { get; set; } // Available Width (simple version: NaN or Auto)
        public Brush BackgroundColor { get; set; }
        public bool IsAfk { get; set; }
        
        public DigitalWellbeing.Core.Models.AppSession OriginalSession { get; set; }
    }
}
