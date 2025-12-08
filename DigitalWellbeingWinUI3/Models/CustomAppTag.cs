using System.Text.Json.Serialization;

namespace DigitalWellbeingWinUI3.Models
{
    public class CustomAppTag
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string HexColor { get; set; }

        public CustomAppTag() { }

        public CustomAppTag(int id, string name, string hexColor)
        {
            Id = id;
            Name = name;
            HexColor = hexColor;
        }
    }
}
