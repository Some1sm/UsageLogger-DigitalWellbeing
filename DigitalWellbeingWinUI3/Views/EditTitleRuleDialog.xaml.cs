
using DigitalWellbeing.Core.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;

namespace DigitalWellbeingWinUI3.Views
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
                TxtPattern.Text = existingRule.MatchPattern;
                TxtReplacement.Text = existingRule.Replacement;
                ChkRegex.IsChecked = existingRule.IsRegex;
                Result = existingRule;
            }
            else
            {
                Result = new CustomTitleRule();
            }

            PrimaryButtonClick += EditTitleRuleDialog_PrimaryButtonClick;
            ChkRegex.Checked += (s, e) => RunTest();
            ChkRegex.Unchecked += (s, e) => RunTest();
            TxtReplacement.TextChanged += (s, e) => RunTest();

            Validate();
        }

        private void EditTitleRuleDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            Result.ProcessName = TxtProcess.Text.Trim();
            Result.MatchPattern = TxtPattern.Text;
            Result.Replacement = TxtReplacement.Text;
            Result.IsRegex = ChkRegex.IsChecked ?? false;
        }

        private void TxtPattern_TextChanged(object sender, TextChangedEventArgs e)
        {
            Validate();
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
            var rule = new CustomTitleRule
            {
                MatchPattern = TxtPattern.Text,
                Replacement = TxtReplacement.Text,
                IsRegex = ChkRegex.IsChecked ?? false,
                ProcessName = TxtProcess.Text // Not used in filtering here, but part of rule object
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
