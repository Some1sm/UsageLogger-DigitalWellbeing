#nullable enable

using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace UsageLogger.Core.Helpers
{
    public static class WindowTitleParser
    {
        public static string Parse(string processName, string rawTitle, System.Collections.Generic.List<Models.CustomTitleRule>? rules = null)
        {
            if (string.IsNullOrWhiteSpace(rawTitle)) return rawTitle;

            // 1. Apply Custom Rules First
            if (rules != null && rules.Count > 0)
            {
                // Simple filter: ProcessName match (or empty for all)
                var applicableRules = rules.Where(r => 
                    string.IsNullOrEmpty(r.ProcessName) || 
                    r.ProcessName.Equals(processName, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(r => r.Order);

                foreach (var rule in applicableRules)
                {
                    if (rule.IsValid())
                    {
                        string result = rule.Apply(rawTitle);
                        // If the rule actually changed something, return immediately (Short-circuit)
                        // Or should we process all rules? Usually chaining is powerful but "First Match Wins" is safer/simpler for now.
                        // Let's go with "First Change Wins" to avoid conflicts.
                        if (result != rawTitle)
                        {
                            return result;
                        }
                    }
                }
            }

            // 2. Default Logic
            switch (processName.ToLower())
            {
                case "chrome":
                case "msedge":
                case "firefox":
                case "brave":
                case "opera":
                    return ParseBrowserTitle(rawTitle);
                
                // Add more apps here
                default:
                    return CleanupGeneric(rawTitle);
            }
        }

        private static string ParseBrowserTitle(string title)
        {
            // 0. Clean Notification Counts: "(1) Instagram" -> "Instagram"
            // Matches (1), (20), (99+)
            title = Regex.Replace(title, @"^\(\d+\+?\)\s*", "");

            // Pattern: "Page Title - Browser Name" or "Page Title - Profile - Browser Name"
            // Example: "YouTube - Google Chrome"
            
            // Heuristic: Split by " - " and take the first part? OR remove the last part?
            // "Video Title - YouTube - Google Chrome" -> We want "Video Title - YouTube" or just "YouTube"??
            // User example: "video title blah blah - Youtube"
            // "gmail inbox()blahblah -Gmail"
            // "reddit - blah blah"
            
            // Strategy: Remove the known browser suffix if present.
            // Then check for common site suffixes.
            
            // 1. Remove Browser Suffix
            var separators = new string[] { " - ", " – ", " — " }; // Dash variations
            var parts = title.Split(separators, StringSplitOptions.RemoveEmptyEntries).ToList();

            if (parts.Count > 1)
            {
                // Remove the last part (usually the browser name "Google Chrome")
                parts.RemoveAt(parts.Count - 1);
            }

            // Reassemble
            string refined = string.Join(" - ", parts);

            // 2. Extract Site Name if possible? 
            // The user wants "Youtube", "Gmail", "Reddit".
            // If the title is "Rick Roll - YouTube", taking the last part "YouTube" is good.
            // If "Inbox (1) - Gmail", taking "Gmail" is good.
            
            // Let's try taking the LAST component after stripping the browser.
            var siteParts = refined.Split(separators, StringSplitOptions.RemoveEmptyEntries);
            if (siteParts.Length > 0)
            {
                string candidate = siteParts.Last();
                
                // Common sites whitelist/cleanup
                if (candidate.Contains("YouTube")) return "YouTube";
                if (candidate.Contains("Gmail")) return "Gmail";
                if (candidate.Contains("Reddit")) return "Reddit";
                if (candidate.Contains("GitHub")) return "GitHub";
                if (candidate.Contains("Stack Overflow")) return "Stack Overflow";
                
                // Fallback: Return the full refined title so we don't lose context?
                // Or just the Candidate? 
                // User said: "window title contains the site name... do you think we can figure out something for that?"
                // Risk: "My Document.docx" -> "My Document.docx" (good).
                // "Detailed Analysis - Google Sheets" -> "Google Sheets" (good).
                
                // Let's return the Candidate (Last Part) but fallback to full if short?
                return candidate;
            }

            return refined;
        }

        private static string CleanupGeneric(string title)
        {
             // Generic cleanup for other apps
             // Visual Studio: "SolutionName - Microsoft Visual Studio" -> "SolutionName"
             var separators = new string[] { " - ", " – " };
             var parts = title.Split(separators, StringSplitOptions.RemoveEmptyEntries);
             
             if (parts.Length > 1)
             {
                 // Usually App Name is last
                 // But wait, for VS code it is "File - Visual Studio Code"
                 // Returning the first part is usually the "Document". The Last part is the "App".
                 // BUT `AppSession` has `ProcessName` (e.g. Code.exe). PROGAM NAME replaces this.
                 // We want the "Context". 
                 // If we return "Visual Studio Code", it's redundant with ProcessName "Code".
                 // If we return "File.cs", it's detailed usage.
                 
                 // User request specifically targeted Browsers mainly.
                 // "in a youtube video... in gmail... in reddit" - all web examples.
                 
                 return title; // Keep full title for non-browsers for now to preserve utility
             }
             return title;
        }
    }
}
