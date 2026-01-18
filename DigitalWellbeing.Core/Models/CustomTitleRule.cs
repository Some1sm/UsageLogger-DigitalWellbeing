#nullable enable

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
        [System.Text.Json.Serialization.JsonIgnore]
        public string FriendlyPattern
        {
            get
            {
                if (!IsRegex) return MatchPattern;

                // Check for our new "Full Coverage" wrappers first
                if (MatchPattern.StartsWith("^.*") && MatchPattern.EndsWith(".*$") && MatchPattern.Length >= 6)
                {
                    // Contains: ^.*TEXT.*$
                    return "Contains: " + Unescape(MatchPattern.Substring(3, MatchPattern.Length - 6));
                }
                else if (MatchPattern.StartsWith("^") && MatchPattern.EndsWith(".*$") && MatchPattern.Length >= 4)
                {
                    // StartsWith: ^TEXT.*$
                    return "Starts with: " + Unescape(MatchPattern.Substring(1, MatchPattern.Length - 4));
                }
                else if (MatchPattern.StartsWith("^.*") && MatchPattern.EndsWith("$") && MatchPattern.Length >= 4)
                {
                    // EndsWith: ^.*TEXT$
                    return "Ends with: " + Unescape(MatchPattern.Substring(3, MatchPattern.Length - 4));
                }
                else if (MatchPattern.StartsWith("^") && MatchPattern.EndsWith("$") && !MatchPattern.Contains(".*") && MatchPattern.Length >= 2)
                {
                    // Exact: ^TEXT$
                    return "Exact: " + Unescape(MatchPattern.Substring(1, MatchPattern.Length - 2));
                }
                
                return "Regex: " + MatchPattern;
            }
        }

        private string Unescape(string regex)
        {
            // Simple unescape for display
            return regex.Replace(@"\.", ".").Replace(@"\(", "(").Replace(@"\)", ")")
                        .Replace(@"\[", "[").Replace(@"\]", "]").Replace(@"\*", "*")
                        .Replace(@"\+", "+").Replace(@"\?", "?").Replace(@"\|", "|");
        }
    }
}
