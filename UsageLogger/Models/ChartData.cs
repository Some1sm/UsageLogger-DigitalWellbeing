using System;
using Windows.UI;

namespace UsageLogger.Models
{
    /// <summary>
    /// POCO data item for pie chart slices.
    /// </summary>
    public class PieChartItem : System.ComponentModel.INotifyPropertyChanged
    {
        private string _name;
        public string Name { get => _name; set { _name = value; OnPropertyChanged(); } }

        private double _value;
        public double Value { get => _value; set { _value = value; OnPropertyChanged(); } }

        private Color _color;
        public Color Color { get => _color; set { _color = value; OnPropertyChanged(); } }

        private string _tooltip;
        public string Tooltip { get => _tooltip; set { _tooltip = value; OnPropertyChanged(); } }

        private double _percentage;
        public double Percentage { get => _percentage; set { _percentage = value; OnPropertyChanged(); } }
        
        // Optional: For click handling
        public string ProcessName { get; set; }

        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
        }
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
