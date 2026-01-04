using System;
using Windows.UI;

namespace DigitalWellbeingWinUI3.Models
{
    /// <summary>
    /// POCO data item for pie chart slices.
    /// </summary>
    public class PieChartItem
    {
        public string Name { get; set; }
        public double Value { get; set; }
        public Color Color { get; set; }
        public string Tooltip { get; set; }
        public double Percentage { get; set; }
        
        // Optional: For click handling
        public string ProcessName { get; set; }
    }
    
    /// <summary>
    /// POCO data item for bar chart columns.
    /// </summary>
    public class BarChartItem
    {
        public string Label { get; set; }
        public double Value { get; set; }
        public Color Color { get; set; }
        public string Tooltip { get; set; }
        
        // Optional: For navigation on click
        public DateTime? Date { get; set; }
    }
    
    /// <summary>
    /// POCO data item for heatmap cells.
    /// </summary>
    public class HeatmapDataPoint
    {
        public int Row { get; set; }  // Day of week (0-6)
        public int Column { get; set; }  // Hour of day (0-23)
        public double Value { get; set; }  // Duration in hours
        public DateTime Date { get; set; }  // Full date for navigation
        public string Tooltip { get; set; }

        // Aliases for Win2DHeatmap compatibility
        public int DayOfWeek
        {
            get => Row;
            set => Row = value;
        }
        public int HourOne
        {
            get => Column;
            set => Column = value;
        }
        public double Intensity
        {
            get => Value;
            set => Value = value;
        }
        public Color Color { get; set; }
    }
}
