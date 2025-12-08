using DigitalWellbeing.Core.Models;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Linq; // Added for Linq

namespace DigitalWellbeingWinUI3.Helpers
{
    public static class AppTagHelper
    {
        public static string GetTagDisplayName(AppTag tag)
        {
            var customTag = UserPreferences.CustomTags.FirstOrDefault(t => t.Id == (int)tag);
            return customTag != null ? customTag.Name : tag.ToString(); // Fallback
        }

        public static Dictionary<string, int> GetComboBoxChoices()
        {
            var choices = new Dictionary<string, int>();
            foreach (var customTag in UserPreferences.CustomTags)
            {
                choices.Add(customTag.Name, customTag.Id);
            }
            return choices;
        }

        public static Brush GetTagColor(AppTag tag)
        {
            var customTag = UserPreferences.CustomTags.FirstOrDefault(t => t.Id == (int)tag);
            if (customTag != null)
            {
                return new SolidColorBrush(ColorHelper.GetColorFromHex(customTag.HexColor));
            }
            return new SolidColorBrush(Colors.Gray);
        }

        public static AppTag GetAppTag(string processName)
        {
            return SettingsManager.GetAppTag(processName);
        }

        public static void UpdateAppTag(string processName, AppTag tag)
        {
            SettingsManager.UpdateAppTag(processName, tag);
        }

        public static void RemoveTag(int tagId)
        {
            SettingsManager.RemoveTag(tagId);
        }

        public static SolidColorBrush GetBrush(string hex)
        {
            return new SolidColorBrush(ColorHelper.GetColorFromHex(hex));
        }
    }
}
