#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.Storage.Pickers;
using UsageLogger.Core.Helpers;
using UsageLogger.Core.Models;
using UsageLogger.Models;

namespace UsageLogger.Helpers
{
    public static class DataExportHelper
    {
        private static void InitializePickerWithWindow(FileSavePicker picker)
        {
            if (App.Current is App app && app.m_window is MainWindow window)
            {
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
                if (hwnd != IntPtr.Zero)
                {
                    WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
                }
            }
        }

        private static string EscapeCsv(string? field)
        {
            if (string.IsNullOrEmpty(field)) return "";
            if (field.Contains(',') || field.Contains('"') || field.Contains('\n') || field.Contains('\r'))
            {
                return "\"" + field.Replace("\"", "\"\"") + "\"";
            }
            return field;
        }

        /// <summary>
        /// Prompts user with a FileSavePicker and exports the sessions to CSV format.
        /// </summary>
        public static async Task<string?> ExportToCsvAsync(List<AppSession> sessions, DateTime startDate, DateTime endDate)
        {
            try
            {
                var picker = new FileSavePicker();
                InitializePickerWithWindow(picker);
                picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
                picker.FileTypeChoices.Add("CSV (Comma delimited)", new string[] { ".csv" });
                picker.SuggestedFileName = $"UsageLogger_Export_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}.csv";

                var file = await picker.PickSaveFileAsync();
                if (file == null) return null;

                var sb = new StringBuilder();
                // Headers
                sb.AppendLine("Date,Start Time,End Time,Process Name,Display Name,Window Title,Category,Productivity Tier,Duration Seconds,Duration Formatted,Energy Wh,Power Impact,Is AFK");

                var sorted = sessions.OrderBy(s => s.StartTime).ToList();
                foreach (var s in sorted)
                {
                    DateTime start = s.StartTime;
                    DateTime end = s.StartTime + s.Duration;
                    string dateStr = start.ToString("yyyy-MM-dd");
                    string startTimeStr = start.ToString("HH:mm:ss");
                    string endTimeStr = end.ToString("HH:mm:ss");
                    string proc = s.ProcessName ?? "";
                    string disp = UserPreferences.GetDisplayName(proc);
                    string title = s.ProgramName ?? "";
                    
                    AppTag tag = AppTagHelper.GetAppTag(proc);
                    if (!string.IsNullOrEmpty(title))
                    {
                        AppTag titleTag = AppTagHelper.GetTitleTag(proc, title);
                        if (titleTag != AppTag.Untagged) tag = titleTag;
                    }
                    string category = AppTagHelper.GetTagDisplayName(tag);
                    string tier = UserPreferences.GetTagTier(tag).ToString();

                    double sec = Math.Round(s.Duration.TotalSeconds, 1);
                    string durFormatted = StringHelper.FormatDurationCompact(s.Duration);
                    bool isAfk = AnalyticsEngine.IsAfk(s);
                    double energyWh = Math.Round(s.EnergyWattHours > 0 ? s.EnergyWattHours : PowerTracker.CalculateEnergyWattHours(PowerTracker.EstimateProcessPower(!isAfk, s.AudioSources.Count > 0, s.Duration.TotalSeconds, 20.0).ProcessWatts, s.Duration), 2);
                    string powerImpact = !string.IsNullOrEmpty(s.PowerImpact) ? s.PowerImpact : (isAfk ? "VeryLow" : "Low");

                    sb.AppendLine($"{EscapeCsv(dateStr)},{EscapeCsv(startTimeStr)},{EscapeCsv(endTimeStr)},{EscapeCsv(proc)},{EscapeCsv(disp)},{EscapeCsv(title)},{EscapeCsv(category)},{EscapeCsv(tier)},{sec},{EscapeCsv(durFormatted)},{energyWh},{EscapeCsv(powerImpact)},{isAfk}");
                }

                await Windows.Storage.FileIO.WriteTextAsync(file, sb.ToString(), Windows.Storage.Streams.UnicodeEncoding.Utf8);
                return file.Path;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DataExportHelper] CSV Export error: {ex}");
                return null;
            }
        }

        /// <summary>
        /// Prompts user with a FileSavePicker and exports sessions and metadata summaries to JSON format.
        /// </summary>
        public static async Task<string?> ExportToJsonAsync(List<AppSession> sessions, DateTime startDate, DateTime endDate)
        {
            try
            {
                var picker = new FileSavePicker();
                InitializePickerWithWindow(picker);
                picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
                picker.FileTypeChoices.Add("JSON Document", new string[] { ".json" });
                picker.SuggestedFileName = $"UsageLogger_Export_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}.json";

                var file = await picker.PickSaveFileAsync();
                if (file == null) return null;

                var dayParts = AnalyticsEngine.ComputeDayParts(sessions);
                var productivity = AnalyticsEngine.ComputeProductivity(sessions);
                var contextSwitches = AnalyticsEngine.ComputeContextSwitches(sessions);

                double totalActiveSec = sessions.Where(s => !AnalyticsEngine.IsAfk(s)).Sum(s => s.Duration.TotalSeconds);
                double totalAfkSec = sessions.Where(s => AnalyticsEngine.IsAfk(s)).Sum(s => s.Duration.TotalSeconds);

                var exportData = new
                {
                    ExportGeneratedAt = DateTime.Now.ToString("o"),
                    DateRange = new
                    {
                        StartDate = startDate.ToString("yyyy-MM-dd"),
                        EndDate = endDate.ToString("yyyy-MM-dd"),
                        TotalDays = (int)(endDate.Date - startDate.Date).TotalDays + 1
                    },
                    Overview = new
                    {
                        TotalActiveDuration = StringHelper.FormatDurationFull(TimeSpan.FromSeconds(totalActiveSec)),
                        TotalActiveSeconds = totalActiveSec,
                        TotalAfkDuration = StringHelper.FormatDurationFull(TimeSpan.FromSeconds(totalAfkSec)),
                        TotalAfkSeconds = totalAfkSec,
                        TotalEnergyWattHours = Math.Round(sessions.Where(s => !AnalyticsEngine.IsAfk(s)).Sum(s => s.EnergyWattHours > 0 ? s.EnergyWattHours : PowerTracker.CalculateEnergyWattHours(PowerTracker.EstimateProcessPower(true, s.AudioSources.Count > 0, s.Duration.TotalSeconds, 20.0).ProcessWatts, s.Duration)), 2),
                        ProductivityScore = productivity.Score,
                        ProductivePercentage = productivity.ProductivePct,
                        NeutralPercentage = productivity.NeutralPct,
                        LeisurePercentage = productivity.LeisurePct,
                        SwitchesPerHour = contextSwitches.SwitchesPerHour,
                        TotalProcessSwitches = contextSwitches.TotalSwitches,
                        FocusRating = contextSwitches.Rating.ToString()
                    },
                    DayPartBreakdown = new
                    {
                        Morning = new { Hours = dayParts.MorningDuration.TotalHours, Formatted = StringHelper.FormatDurationCompact(dayParts.MorningDuration), Percentage = dayParts.MorningPct, Range = dayParts.MorningRangeText },
                        Afternoon = new { Hours = dayParts.AfternoonDuration.TotalHours, Formatted = StringHelper.FormatDurationCompact(dayParts.AfternoonDuration), Percentage = dayParts.AfternoonPct, Range = dayParts.AfternoonRangeText },
                        Evening = new { Hours = dayParts.EveningDuration.TotalHours, Formatted = StringHelper.FormatDurationCompact(dayParts.EveningDuration), Percentage = dayParts.EveningPct, Range = dayParts.EveningRangeText },
                        LateNight = new { Hours = dayParts.NightDuration.TotalHours, Formatted = StringHelper.FormatDurationCompact(dayParts.NightDuration), Percentage = dayParts.NightPct, Range = dayParts.NightRangeText }
                    },
                    TopSwitchedApps = contextSwitches.TopSwitchedApps.Select(a => new
                    {
                        ProcessName = a.ProcessName,
                        DisplayName = a.DisplayName,
                        SwitchCount = a.Count,
                        Percentage = a.Percentage
                    }),
                    Sessions = sessions.OrderBy(s => s.StartTime).Select(s =>
                    {
                        AppTag tag = AppTagHelper.GetAppTag(s.ProcessName);
                        if (!string.IsNullOrEmpty(s.ProgramName))
                        {
                            AppTag titleTag = AppTagHelper.GetTitleTag(s.ProcessName, s.ProgramName);
                            if (titleTag != AppTag.Untagged) tag = titleTag;
                        }

                        bool isAfk = AnalyticsEngine.IsAfk(s);
                        double energyWh = Math.Round(s.EnergyWattHours > 0 ? s.EnergyWattHours : PowerTracker.CalculateEnergyWattHours(PowerTracker.EstimateProcessPower(!isAfk, s.AudioSources.Count > 0, s.Duration.TotalSeconds, 20.0).ProcessWatts, s.Duration), 2);
                        string powerImpact = !string.IsNullOrEmpty(s.PowerImpact) ? s.PowerImpact : (isAfk ? "VeryLow" : "Low");

                        return new
                        {
                            StartTime = s.StartTime.ToString("o"),
                            EndTime = (s.StartTime + s.Duration).ToString("o"),
                            ProcessName = s.ProcessName,
                            DisplayName = UserPreferences.GetDisplayName(s.ProcessName),
                            WindowTitle = s.ProgramName,
                            Category = AppTagHelper.GetTagDisplayName(tag),
                            ProductivityTier = UserPreferences.GetTagTier(tag).ToString(),
                            DurationSeconds = s.Duration.TotalSeconds,
                            DurationFormatted = StringHelper.FormatDurationCompact(s.Duration),
                            EnergyWattHours = energyWh,
                            PowerImpact = powerImpact,
                            IsAfk = isAfk
                        };
                    })
                };

                string json = JsonSerializer.Serialize(exportData, new JsonSerializerOptions { WriteIndented = true });
                await Windows.Storage.FileIO.WriteTextAsync(file, json, Windows.Storage.Streams.UnicodeEncoding.Utf8);
                return file.Path;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DataExportHelper] JSON Export error: {ex}");
                return null;
            }
        }
    }
}
