using System;
using System.Collections.Generic;
using System.Linq;
using UsageLogger.Core;
using UsageLogger.Core.Models;
using UsageLogger.Views;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace UsageLogger.Helpers;

/// <summary>
/// Encapsulates hidden sub-app and custom title rule management for Settings page.
/// Extracted from SettingsPage.xaml.cs.
/// </summary>
public static class SubAppRulesHelper
{
    /// <summary>
    /// Populates the hidden sub-apps list. If <paramref name="showActualText"/> is false,
    /// keywords are masked with bullet characters.
    /// </summary>
    public static void LoadHiddenSubApps(ListView list, ToggleSwitch tglRetroactive, bool showActualText)
    {
        list.Items.Clear();
        foreach (string keyword in UserPreferences.IgnoredWindowTitles)
        {
            list.Items.Add(showActualText ? keyword : new string('●', keyword.Length));
        }
        tglRetroactive.IsOn = UserPreferences.HideSubAppsRetroactively;
    }

    /// <summary>
    /// Adds a keyword to the ignored list and refreshes the UI.
    /// </summary>
    public static void AddHiddenKeyword(TextBox txtKeyword, Action reloadList)
    {
        string keyword = txtKeyword.Text?.Trim();
        if (!string.IsNullOrEmpty(keyword) &&
            !UserPreferences.IgnoredWindowTitles.Contains(keyword, StringComparer.OrdinalIgnoreCase))
        {
            UserPreferences.IgnoredWindowTitles.Add(keyword);
            UserPreferences.Save();
            reloadList?.Invoke();
            txtKeyword.Text = "";
        }
    }

    /// <summary>
    /// Removes a selected hidden keyword (only when keywords are visible).
    /// </summary>
    public static void RemoveHiddenKeyword(ListView list, bool keywordsVisible, Action reloadList)
    {
        if (list.SelectedItem != null && keywordsVisible)
        {
            int index = list.SelectedIndex;
            if (index >= 0 && index < UserPreferences.IgnoredWindowTitles.Count)
            {
                UserPreferences.IgnoredWindowTitles.RemoveAt(index);
                UserPreferences.Save();
                reloadList?.Invoke();
            }
        }
    }

    /// <summary>
    /// Loads custom title rules into the list.
    /// </summary>
    public static void LoadCustomRules(ListView list)
    {
        list.ItemsSource = null;
        list.ItemsSource = UserPreferences.CustomTitleRules;
    }

    /// <summary>
    /// Shows dialog for adding a new custom title rule.
    /// </summary>
    public static async void AddRule(XamlRoot xamlRoot, Action reloadRules)
    {
        var dialog = new EditTitleRuleDialog();
        dialog.XamlRoot = xamlRoot;
        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            UserPreferences.CustomTitleRules.Add(dialog.Result);
            UserPreferences.Save();
            reloadRules?.Invoke();
        }
    }

    /// <summary>
    /// Shows dialog for editing an existing custom title rule.
    /// </summary>
    public static async void EditRule(object sender, XamlRoot xamlRoot, Action reloadRules)
    {
        var btn = sender as Button;
        var rule = btn?.Tag as CustomTitleRule;
        if (rule == null) return;

        var dialog = new EditTitleRuleDialog(rule);
        dialog.XamlRoot = xamlRoot;
        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            UserPreferences.Save();
            reloadRules?.Invoke();
        }
    }

    /// <summary>
    /// Deletes a custom title rule.
    /// </summary>
    public static void DeleteRule(object sender, Action reloadRules)
    {
        var btn = sender as Button;
        var rule = btn?.Tag as CustomTitleRule;
        if (rule != null)
        {
            UserPreferences.CustomTitleRules.Remove(rule);
            UserPreferences.Save();
            reloadRules?.Invoke();
        }
    }
}
