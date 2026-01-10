
using System;
using System.Text.RegularExpressions;

namespace DigitalWellbeing.Core.Models
{
    public class CustomTitleRule
    {
        public string ProcessName { get; set; } = "";
        public string MatchPattern { get; set; } = "";
        public string Replacement { get; set; } = "";
        public bool IsRegex { get; set; } = false;
        
        // Priority order (lowest number executes first)
        public int Order { get; set; } = 0;

        public bool IsValid()
        {
            if (string.IsNullOrWhiteSpace(MatchPattern)) return false;
            
            if (IsRegex)
            {
                try
                {
                    // Validate regex syntax
                    Regex.Match("", MatchPattern);
                }
                catch
                {
                    return false;
                }
            }
            return true;
        }

        public string Apply(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return input;

            if (IsRegex)
            {
                try
                {
                    return Regex.Replace(input, MatchPattern, Replacement);
                }
                catch
                {
                    return input;
                }
            }
            else
            {
                // Simple string replace (case-insensitive for convenience)
                return input.Replace(MatchPattern, Replacement, StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}
