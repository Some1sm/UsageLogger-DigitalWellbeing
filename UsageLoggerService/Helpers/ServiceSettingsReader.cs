#nullable enable
using UsageLogger.Core;
using UsageLogger.Core.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace UsageLoggerService.Helpers;

/// <summary>
/// Reads and caches user preferences from the JSON settings file.
/// Extracted from ActivityLogger to separate settings I/O from logging logic.
/// </summary>
public class ServiceSettingsReader
{
    private const int DEFAULT_BUFFER_FLUSH_INTERVAL_SEC = 300;
    private const int IMMEDIATE_FLUSH_INTERVAL_SEC = 1;
    private const int SETTINGS_THROTTLE_SEC = 30;
    private const int MIN_IDLE_THRESHOLD_SEC = 60;
    private const int MIN_FLUSH_INTERVAL_SEC = 60;

    private int _bufferFlushIntervalSec = DEFAULT_BUFFER_FLUSH_INTERVAL_SEC;
    private int _idleThresholdSec = 300;
    private List<string> _ignoredWindowTitles = new List<string>();
    private List<CustomTitleRule> _customTitleRules = new List<CustomTitleRule>();
    private DateTime _lastSettingsRead = DateTime.MinValue;
    private DateTime _lastFileWriteTime = DateTime.MinValue;
    private readonly string _settingsPath = ApplicationPath.UserPreferencesFile;

    /// <summary>
    /// Currently cached ignored window title keywords.
    /// </summary>
    public IReadOnlyList<string> IgnoredWindowTitles => _ignoredWindowTitles;

    /// <summary>
    /// Currently cached custom title rules.
    /// </summary>
    public IReadOnlyList<CustomTitleRule> CustomTitleRules => _customTitleRules;

    /// <summary>
    /// Reads settings and returns the buffer flush interval in seconds.
    /// Uses file-timestamp caching to avoid redundant reads.
    /// </summary>
    public int GetBufferFlushInterval()
    {
        if (File.Exists(_settingsPath))
        {
            var currentWriteTime = File.GetLastWriteTime(_settingsPath);
            if (currentWriteTime != _lastFileWriteTime)
            {
                _lastFileWriteTime = currentWriteTime;
                _lastSettingsRead = DateTime.Now;
            }
            else
            {
                if ((DateTime.Now - _lastSettingsRead).TotalSeconds < SETTINGS_THROTTLE_SEC)
                    return _bufferFlushIntervalSec;

                _lastSettingsRead = DateTime.Now;
            }
        }
        else
        {
            if ((DateTime.Now - _lastSettingsRead).TotalSeconds < SETTINGS_THROTTLE_SEC)
                return _bufferFlushIntervalSec;
            _lastSettingsRead = DateTime.Now;
        }

        try
        {
            if (!File.Exists(_settingsPath)) return _bufferFlushIntervalSec;

            string json;
            using (var fs = new FileStream(_settingsPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var sr = new StreamReader(fs))
            {
                json = sr.ReadToEnd();
            }

            bool useRamCache = ParseJsonBool(json, "UseRamCache", true);
            if (!useRamCache)
            {
                _bufferFlushIntervalSec = IMMEDIATE_FLUSH_INTERVAL_SEC;
                return _bufferFlushIntervalSec;
            }

            _bufferFlushIntervalSec = ParseJsonInt(json, "DataFlushIntervalSeconds", DEFAULT_BUFFER_FLUSH_INTERVAL_SEC, MIN_FLUSH_INTERVAL_SEC);

            // Sync DayStartMinutes to Core DateHelper
            int dayStartHour = ParseJsonInt(json, "DayStartHour", 0, 0);
            int dayStartMinute = ParseJsonInt(json, "DayStartMinute", 0, 0);
            UsageLogger.Core.Helpers.DateHelper.DayStartMinutes = Math.Clamp(dayStartHour, 0, 23) * 60 + Math.Clamp(dayStartMinute, 0, 59);

            // Parse IgnoredWindowTitles array
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("IgnoredWindowTitles", out var titlesElement) &&
                    titlesElement.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    _ignoredWindowTitles = titlesElement.EnumerateArray()
                        .Where(e => e.ValueKind == System.Text.Json.JsonValueKind.String)
                        .Select(e => e.GetString() ?? "")
                        .Where(s => !string.IsNullOrEmpty(s))
                        .ToList();
                }
            }
            catch (System.Text.Json.JsonException ex)
            {
                ServiceLogger.Log("Settings", $"Failed to parse IgnoredWindowTitles: {ex.Message}");
            }

            // Parse CustomTitleRules array
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("CustomTitleRules", out var rulesElement) &&
                    rulesElement.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    _customTitleRules = System.Text.Json.JsonSerializer.Deserialize<List<CustomTitleRule>>(rulesElement.GetRawText())
                                        ?? new List<CustomTitleRule>();
                }
            }
            catch (System.Text.Json.JsonException ex)
            {
                ServiceLogger.Log("Settings", $"Failed to parse CustomTitleRules: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            ServiceLogger.Log("Settings", $"Failed to read settings: {ex.Message}");
        }

        return _bufferFlushIntervalSec;
    }

    /// <summary>
    /// Checks if a window title should be hidden (merged into parent process).
    /// </summary>
    public bool ShouldHideSubApp(string? windowTitle)
    {
        if (string.IsNullOrEmpty(windowTitle) || _ignoredWindowTitles.Count == 0)
            return false;

        foreach (var keyword in _ignoredWindowTitles)
        {
            if (windowTitle.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Gets the idle threshold in milliseconds, reading from settings if available.
    /// </summary>
    public int GetIdleThresholdMs()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                string json;
                using (var fs = new FileStream(_settingsPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var sr = new StreamReader(fs))
                {
                    json = sr.ReadToEnd();
                }

                _idleThresholdSec = ParseJsonInt(json, "IdleThresholdSeconds", _idleThresholdSec, MIN_IDLE_THRESHOLD_SEC);
            }
        }
        catch (Exception ex)
        {
            ServiceLogger.Log("Settings", $"Failed to read idle threshold: {ex.Message}");
        }

        return _idleThresholdSec * 1000;
    }

    // --- JSON Parsing Utilities ---

    private static string? ParseJsonPropertyRaw(string json, string propertyName)
    {
        int idx = json.IndexOf($"\"{propertyName}\":");
        if (idx < 0) return null;

        int colonIdx = json.IndexOf(':', idx);
        int endIdx = json.IndexOf(',', colonIdx);
        if (endIdx < 0) endIdx = json.IndexOf('}', colonIdx);
        if (endIdx < 0) return null;

        return json.Substring(colonIdx + 1, endIdx - colonIdx - 1).Trim();
    }

    private static bool ParseJsonBool(string json, string propertyName, bool defaultValue)
    {
        string? value = ParseJsonPropertyRaw(json, propertyName);
        if (value == null) return defaultValue;
        return value.ToLowerInvariant() == "true";
    }

    private static int ParseJsonInt(string json, string propertyName, int defaultValue, int minValue = int.MinValue)
    {
        string? value = ParseJsonPropertyRaw(json, propertyName);
        if (value == null) return defaultValue;
        if (int.TryParse(value, out int result) && result >= minValue)
            return result;
        return defaultValue;
    }
}
