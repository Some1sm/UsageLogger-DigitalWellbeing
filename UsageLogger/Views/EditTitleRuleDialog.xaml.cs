
using UsageLogger.Core.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Text.RegularExpressions;

namespace UsageLogger.Views
{
    public sealed partial class EditTitleRuleDialog : ContentDialog
    {
        public CustomTitleRule Result { get; private set; }

        public EditTitleRuleDialog(CustomTitleRule existingRule = null)
        {
            this.InitializeComponent();

            if (existingRule != null)
            {
                TxtProcess.Text = existingRule.ProcessName;
                TxtReplacement.Text = existingRule.Replacement;
                Result = existingRule;

                // Heuristic to detect existing rule type
                if (!existingRule.IsRegex)
                {
                    // Legacy simple (Contains)
                    TxtPattern.Text = existingRule.MatchPattern;
                    ComboMatchType.SelectedIndex = 0; // Contains
                }
                else
                {
                    string p = existingRule.MatchPattern;
                    
                    // Check for our new "Full Coverage" wrappers first
                    if (p.StartsWith("^.*") && p.EndsWith(".*$"))
                    {
                        // Contains: ^.*TEXT.*$
                        // Extract text between ^.* and .*$ (Length 3 and 3)
                        if (p.Length >= 6)
                        {
                            TxtPattern.Text = Unescape(p.Substring(3, p.Length - 6));
                            ComboMatchType.SelectedIndex = 0; // Contains
                        }
                    }
                    else if (p.StartsWith("^") && p.EndsWith(".*$")) 
                    {
                        // StartsWith: ^TEXT.*$
                        // Extract text between ^ and .*$ (Length 1 and 3)
                        if (p.Length >= 4)
                        {
                            TxtPattern.Text = Unescape(p.Substring(1, p.Length - 4));
                            ComboMatchType.SelectedIndex = 1; // StartsWith
                        }
                    }
                    else if (p.StartsWith("^.*") && p.EndsWith("$"))
                    {
                         // EndsWith: ^.*TEXT$
                         // Extract text between ^.* and $ (Length 3 and 1)
                         if (p.Length >= 4)
                         {
                             TxtPattern.Text = Unescape(p.Substring(3, p.Length - 4));
                             ComboMatchType.SelectedIndex = 2; // EndsWith
                         }
                    }
                    else if (p.StartsWith("^") && p.EndsWith("$") && !p.Contains(".*"))
                    {
                        // Exact: ^TEXT$
                        if (p.Length >= 2)
                        {
                            TxtPattern.Text = Unescape(p.Substring(1, p.Length - 2));
                            ComboMatchType.SelectedIndex = 3; // Exact
                        }
                    }
                    else
                    {
                        // Custom Regex or Legacy
                        TxtPattern.Text = p;
                        ComboMatchType.SelectedIndex = 4; // Regex
                    }
                }
            }
            else
            {
                Result = new CustomTitleRule();
                ComboMatchType.SelectedIndex = 0; // Default Contains
            }

            PrimaryButtonClick += EditTitleRuleDialog_PrimaryButtonClick;
            TxtReplacement.TextChanged += (s, e) => RunTest();
            Validate();
        }

        private string Unescape(string regex)
        {
            // Simple unescape for display if it was just Regex.Escape()
            // This is "best effort" to show friendly text
            return regex.Replace(@"\.", ".").Replace(@"\(", "(").Replace(@"\)", ")")
                        .Replace(@"\[", "[").Replace(@"\]", "]").Replace(@"\*", "*")
                        .Replace(@"\+", "+").Replace(@"\?", "?").Replace(@"\|", "|");
        }

        private void EditTitleRuleDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            Result.ProcessName = TxtProcess.Text.Trim();
            Result.Replacement = TxtReplacement.Text;
            
            // Convert UI input to Model pattern based on Type
            Result.MatchPattern = GetRegexFromInput();
            Result.IsRegex = true; // Always use regex engine now for consistency
        }

        private string GetRegexFromInput()
        {
            string input = TxtPattern.Text;
            string type = (ComboMatchType.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Contains";

            switch (type)
            {
                case "Contains":
                    // Full coverage: ^.*TEXT.*$
                    return "^.*" + Regex.Escape(input) + ".*$";
                case "StartsWith":
                    // Full coverage: ^TEXT.*$
                    return "^" + Regex.Escape(input) + ".*$";
                case "EndsWith":
                    // Full coverage: ^.*TEXT$
                    return "^.*" + Regex.Escape(input) + "$";
                case "Exact":
                    return "^" + Regex.Escape(input) + "$";
                case "Regex":
                default:
                    return input;
            }
        }

        private void TxtPattern_TextChanged(object sender, TextChangedEventArgs e)
        {
            Validate();
            RunTest();
        }

        private void ComboMatchType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            RunTest();
        }

        private void TxtTestInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            RunTest();
        }

        private void Validate()
        {
            IsPrimaryButtonEnabled = !string.IsNullOrWhiteSpace(TxtPattern.Text);
        }

        private void RunTest()
        {
            // Safety check against initialization race condition
            if (TxtTestResult == null || TxtPattern == null || TxtReplacement == null || TxtProcess == null || ComboMatchType == null) return;

            var rule = new CustomTitleRule
            {
                MatchPattern = GetRegexFromInput(),
                Replacement = TxtReplacement.Text,
                IsRegex = true, // We always test as regex with our generated pattern
                ProcessName = TxtProcess.Text 
            };

            if (rule.IsValid())
            {
                TxtTestResult.Text = rule.Apply(TxtTestInput.Text);
            }
            else
            {
                TxtTestResult.Text = "(Invalid Pattern)";
            }
        }
    }
}
