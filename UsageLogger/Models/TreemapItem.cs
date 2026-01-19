using Microsoft.UI.Xaml.Media;

namespace UsageLogger.Models
{
    /// <summary>
    /// Represents a single item in the Treemap visualization.
    /// </summary>
    public class TreemapItem
    {
        public string Name { get; set; }
        public double Value { get; set; }
        public double Percentage { get; set; }
        public string FormattedValue { get; set; }
        public Brush Fill { get; set; }
        
        // Layout - calculated by TreemapLayout
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
    }
}
