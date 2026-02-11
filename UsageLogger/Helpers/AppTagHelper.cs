using UsageLogger.Core.Models;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace UsageLogger.Helpers
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
            return UserPreferences.GetAppTag(processName);
        }

        public static void UpdateAppTag(string processName, AppTag tag)
        {
            UserPreferences.UpdateAppTag(processName, tag);
        }

        public static void UpdateTitleTag(string processName, string keyword, int tagId)
        {
            UserPreferences.UpdateTitleTag(processName, keyword, tagId);
        }
        
        public static AppTag GetTitleTag(string processName, string title)
        {
            int? id = UserPreferences.GetTitleTagId(processName, title);
            if (id.HasValue) return (AppTag)id.Value;
            return AppTag.Untagged;
        }

        public static void RemoveTag(int tagId)
        {
            UserPreferences.RemoveTagFromAll(tagId);
        }

        public static SolidColorBrush GetBrush(string hex)
        {
            return new SolidColorBrush(ColorHelper.GetColorFromHex(hex));
        }

        public static async Task ValidateAppTags()
        {
            var validIds = UserPreferences.CustomTags.Select(t => t.Id).ToHashSet();
            // Default Tag (Untagged = 0) is always valid
            validIds.Add(0);

            var orphanApps = new List<string>();
            var currentTags = UserPreferences.GetAllAppTags(); 

            foreach (var kvp in currentTags)
            {
                if (!validIds.Contains((int)kvp.Value))
                {
                    orphanApps.Add(kvp.Key);
                }
            }

            foreach (var app in orphanApps)
            {
                UserPreferences.UpdateAppTag(app, AppTag.Untagged);
            }
        }
    }
}
