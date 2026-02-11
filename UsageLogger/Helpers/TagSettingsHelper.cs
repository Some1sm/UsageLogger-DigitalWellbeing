using System;
using System.Collections.ObjectModel;
using System.Linq;
using UsageLogger.Core;
using UsageLogger.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace UsageLogger.Helpers;

/// <summary>
/// Encapsulates tag management logic for the Settings page.
/// Extracted from SettingsPage.xaml.cs.
/// </summary>
public static class TagSettingsHelper
{
    public static void LoadTags(ObservableCollection<CustomAppTag> tags)
    {
        tags.Clear();
        foreach (var tag in UserPreferences.CustomTags)
        {
            tags.Add(tag);
        }
    }

    public static void AddTag(ObservableCollection<CustomAppTag> tags)
    {
        int nextId = tags.Count > 0 ? tags.Max(t => t.Id) + 1 : 0;
        while (tags.Any(t => t.Id == nextId)) nextId++;

        var newTag = new CustomAppTag(nextId, "New Tag", "#808080");
        tags.Add(newTag);
        SaveTags(tags);
    }

    public static void DeleteTag(ObservableCollection<CustomAppTag> tags, object sender)
    {
        if (sender is Button btn && btn.Tag is int id)
        {
            var tagToRemove = tags.FirstOrDefault(t => t.Id == id);
            if (tagToRemove != null)
            {
                AppTagHelper.RemoveTag(tagToRemove.Id);
                tags.Remove(tagToRemove);
                SaveTags(tags);
            }
        }
    }

    public static void OnColorChanged(ObservableCollection<CustomAppTag> tags, ColorPicker sender, ColorChangedEventArgs args)
    {
        if (sender.Tag is int id)
        {
            var tag = tags.FirstOrDefault(t => t.Id == id);
            if (tag != null)
            {
                tag.HexColor = args.NewColor.ToString();
                SaveTags(tags);
            }
        }
    }

    public static void OnColorPickerFlyoutClosed(ObservableCollection<CustomAppTag> tags)
    {
        SaveTags(tags);
        var copy = new ObservableCollection<CustomAppTag>(tags);
        tags.Clear();
        foreach (var t in copy) tags.Add(t);
    }

    public static void SaveTags(ObservableCollection<CustomAppTag> tags)
    {
        UserPreferences.CustomTags = tags.ToList();
        UserPreferences.Save();
    }

    public static Microsoft.UI.Xaml.Media.SolidColorBrush GetBrush(string hex)
    {
        return new Microsoft.UI.Xaml.Media.SolidColorBrush(ColorHelper.GetColorFromHex(hex));
    }
}
