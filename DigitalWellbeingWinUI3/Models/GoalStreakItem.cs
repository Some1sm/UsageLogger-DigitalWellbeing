using Microsoft.UI.Xaml.Media;

namespace DigitalWellbeingWinUI3.Models
{
    public class GoalStreakItem
    {
        public string Count { get; set; } // "3", "5", "0"
        public string Label { get; set; } // "days in a row..."
        public string IconPath { get; set; } // Path to icon or Segoe MDL2 glyph
        public Brush IconColor { get; set; }
    }
}
