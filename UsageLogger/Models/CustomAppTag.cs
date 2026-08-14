using System.Text.Json.Serialization;

namespace UsageLogger.Models
{
    public enum ProductivityTier
    {
        Productive = 0,
        Neutral = 1,
        Leisure = 2
    }

    public class CustomAppTag
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string HexColor { get; set; } = "#808080";
        public ProductivityTier Tier { get; set; } = ProductivityTier.Productive;

        public CustomAppTag() { }

        public CustomAppTag(int id, string name, string hexColor, ProductivityTier tier = ProductivityTier.Productive)
        {
            Id = id;
            Name = name;
            HexColor = hexColor;
            Tier = tier;
        }
    }
}
