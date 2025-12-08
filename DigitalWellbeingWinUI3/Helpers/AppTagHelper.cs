using DigitalWellbeing.Core.Models;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;

namespace DigitalWellbeingWinUI3.Helpers
{
    public static class AppTagHelper
    {
        public static string GetTagDisplayName(AppTag tag)
        {
            return tag.ToString();
        }

        public static Dictionary<string, int> GetComboBoxChoices()
        {
            var choices = new Dictionary<string, int>();
            foreach (AppTag tag in Enum.GetValues(typeof(AppTag)))
            {
                choices.Add(GetTagDisplayName(tag), (int)tag);
            }
            return choices;
        }

        public static Brush GetTagColor(AppTag tag)
        {
            // Colors from standard WPF Brushes mapped to Microsoft.UI.Colors
            switch (tag)
            {
                case AppTag.Work: return new SolidColorBrush(Colors.DodgerBlue);
                case AppTag.Education: return new SolidColorBrush(Colors.Orange);
                case AppTag.Entertainment: return new SolidColorBrush(Colors.MediumPurple);
                case AppTag.Social: return new SolidColorBrush(Colors.DeepPink);
                case AppTag.Utility: return new SolidColorBrush(Colors.Gray);
                case AppTag.Game: return new SolidColorBrush(Colors.Crimson);
                case AppTag.Untagged:
                default:
                    return new SolidColorBrush(Colors.Gray);
            }
        }

        public static AppTag GetAppTag(string processName)
        {
            return SettingsManager.GetAppTag(processName);
        }

        public static void UpdateAppTag(string processName, AppTag tag)
        {
            SettingsManager.UpdateAppTag(processName, tag);
        }
    }
}
