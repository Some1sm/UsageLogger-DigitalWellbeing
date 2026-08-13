#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.Graphics.Canvas.Text;
using UsageLogger.Core;
using UsageLogger.Core.Data;
using UsageLogger.Core.Models;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using Windows.UI;
using Microsoft.UI;
using Microsoft.UI.Text;

namespace UsageLogger.Helpers
{
    public class WeeklyReportData
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public TimeSpan TotalDuration { get; set; }
        public TimeSpan DailyAverage { get; set; }
        public double WeekOverWeekPercent { get; set; }
        public bool TrendIsGood { get; set; }
        public string PeakHoursText { get; set; } = "10:00 - 14:00";
        public List<(string Name, TimeSpan Duration, double Percent, Color Color)> TopApps { get; set; } = new();
        public List<(string DayName, TimeSpan Duration, bool IsHighest)> DailyBreakdown { get; set; } = new();
        public string EstimatedCostText { get; set; } = "$0.00";
        public int TotalActiveDays { get; set; }
    }

    public static class VisualReportGenerator
    {
        private static readonly Color[] Palette = new[]
        {
            Color.FromArgb(255, 99, 102, 241),   // Indigo / Accent
            Color.FromArgb(255, 59, 130, 246),   // Blue
            Color.FromArgb(255, 16, 185, 129),   // Emerald
            Color.FromArgb(255, 245, 158, 11),   // Amber
            Color.FromArgb(255, 236, 72, 153),   // Pink
            Color.FromArgb(255, 139, 92, 246)    // Purple
        };

        public static async Task<WeeklyReportData> CompileWeeklyDataAsync(DateTime referenceDate)
        {
            var data = new WeeklyReportData();
            
            // Align to 7-day window ending on referenceDate or logical week
            data.EndDate = referenceDate.Date;
            data.StartDate = data.EndDate.AddDays(-6);

            var repo = new AppUsageRepository(ApplicationPath.UsageLogsFolder);
            var currWeekUsages = new List<AppUsage>();
            var dayTotals = new Dictionary<DateTime, TimeSpan>();

            for (int i = 0; i < 7; i++)
            {
                var d = data.StartDate.AddDays(i);
                var usages = await repo.GetUsageForDateAsync(d);
                usages = usages.Where(u => !UserPreferences.UserExcludedProcesses.Contains(u.ProcessName, StringComparer.OrdinalIgnoreCase)).ToList();
                
                double sec = usages.Sum(u => u.Duration.TotalSeconds);
                var dayDur = TimeSpan.FromSeconds(sec);
                dayTotals[d] = dayDur;
                currWeekUsages.AddRange(usages);
            }

            // Previous week for trend
            double prevWeekSec = 0;
            for (int i = 0; i < 7; i++)
            {
                var d = data.StartDate.AddDays(-7 + i);
                var usages = await repo.GetUsageForDateAsync(d);
                usages = usages.Where(u => !UserPreferences.UserExcludedProcesses.Contains(u.ProcessName, StringComparer.OrdinalIgnoreCase)).ToList();
                prevWeekSec += usages.Sum(u => u.Duration.TotalSeconds);
            }

            double currWeekSec = currWeekUsages.Sum(u => u.Duration.TotalSeconds);
            data.TotalDuration = TimeSpan.FromSeconds(currWeekSec);
            data.TotalActiveDays = dayTotals.Count(d => d.Value.TotalMinutes >= 5);
            data.DailyAverage = data.TotalActiveDays > 0 ? TimeSpan.FromSeconds(currWeekSec / Math.Max(1, data.TotalActiveDays)) : TimeSpan.Zero;

            if (prevWeekSec > 0)
            {
                double change = ((currWeekSec - prevWeekSec) / prevWeekSec) * 100.0;
                data.WeekOverWeekPercent = change;
                data.TrendIsGood = change <= 0; // Less screen time is generally good
            }
            else
            {
                data.WeekOverWeekPercent = 0;
                data.TrendIsGood = true;
            }

            // Top 5 Apps
            var groupedApps = currWeekUsages
                .GroupBy(u => u.ProcessName)
                .Select(g => new
                {
                    ProcessName = g.Key,
                    Duration = TimeSpan.FromSeconds(g.Sum(x => x.Duration.TotalSeconds))
                })
                .OrderByDescending(x => x.Duration)
                .Take(5)
                .ToList();

            int colorIdx = 0;
            foreach (var app in groupedApps)
            {
                string dName = UserPreferences.GetDisplayName(app.ProcessName);
                if (string.IsNullOrEmpty(dName)) dName = app.ProcessName;
                double pct = currWeekSec > 0 ? (app.Duration.TotalSeconds / currWeekSec) * 100.0 : 0;
                data.TopApps.Add((dName, app.Duration, pct, Palette[colorIdx % Palette.Length]));
                colorIdx++;
            }

            // Daily breakdown
            TimeSpan maxDay = dayTotals.Values.DefaultIfEmpty(TimeSpan.Zero).Max();
            foreach (var kvp in dayTotals.OrderBy(k => k.Key))
            {
                string dayName = kvp.Key.ToString("ddd");
                data.DailyBreakdown.Add((dayName, kvp.Value, kvp.Value == maxDay && maxDay.TotalMinutes > 10));
            }

            // Power Cost estimate
            double totalHours = data.TotalDuration.TotalHours;
            double watts = UserPreferences.EstimatedPowerUsageWatts;
            double priceKwh = UserPreferences.KwhPrice;
            string symbol = UserPreferences.CurrencySymbol ?? "$";
            double cost = (watts * totalHours / 1000.0) * priceKwh;
            data.EstimatedCostText = $"{symbol}{cost:F2}";

            return data;
        }

        public static async Task<InMemoryRandomAccessStream> GenerateReportImageStreamAsync(WeeklyReportData data)
        {
            const float W = 1200f;
            const float H = 800f;

            var device = CanvasDevice.GetSharedDevice();
            using var renderTarget = new CanvasRenderTarget(device, W, H, 96);

            using (var ds = renderTarget.CreateDrawingSession())
            {
                ds.Clear(Color.FromArgb(255, 15, 17, 23)); // Deep sleek navy-dark

                // Subtle ambient glow
                ds.FillEllipse(new Vector2(W * 0.85f, H * 0.15f), 300, 300, Color.FromArgb(20, 99, 102, 241));
                ds.FillEllipse(new Vector2(W * 0.15f, H * 0.85f), 280, 280, Color.FromArgb(15, 59, 130, 246));

                // Outer Card Boundary
                ds.DrawRoundedRectangle(16, 16, W - 32, H - 32, 20, 20, Color.FromArgb(40, 255, 255, 255), 1.5f);

                // Top Header
                using var titleFormat = new CanvasTextFormat { FontSize = 28, FontWeight = FontWeights.Bold, FontFamily = "Segoe UI" };
                using var subtitleFormat = new CanvasTextFormat { FontSize = 14, FontWeight = FontWeights.Normal, FontFamily = "Segoe UI" };
                using var kpiValueFormat = new CanvasTextFormat { FontSize = 32, FontWeight = FontWeights.Bold, FontFamily = "Segoe UI" };
                using var kpiLabelFormat = new CanvasTextFormat { FontSize = 12, FontWeight = FontWeights.SemiBold, FontFamily = "Segoe UI" };
                using var appNameFormat = new CanvasTextFormat { FontSize = 15, FontWeight = FontWeights.SemiBold, FontFamily = "Segoe UI" };
                using var appDurFormat = new CanvasTextFormat { FontSize = 13, FontWeight = FontWeights.Normal, FontFamily = "Segoe UI" };
                using var smallFormat = new CanvasTextFormat { FontSize = 11, FontWeight = FontWeights.Normal, FontFamily = "Segoe UI" };

                // App Title & Logo
                ds.DrawText("⚡ UsageLogger", 44, 42, Color.FromArgb(255, 129, 140, 248), titleFormat);
                ds.DrawText("WEEKLY ACTIVITY & WELLBEING REPORT", 44, 76, Color.FromArgb(160, 255, 255, 255), subtitleFormat);

                // Date Range Badge
                string rangeStr = $"{data.StartDate:MMM dd} — {data.EndDate:MMM dd, yyyy}";
                float badgeW = 220f, badgeH = 34f, badgeX = W - 44 - badgeW, badgeY = 46f;
                ds.FillRoundedRectangle(badgeX, badgeY, badgeW, badgeH, 17, 17, Color.FromArgb(35, 255, 255, 255));
                ds.DrawRoundedRectangle(badgeX, badgeY, badgeW, badgeH, 17, 17, Color.FromArgb(60, 255, 255, 255), 1f);
                ds.DrawText(rangeStr, badgeX + 16, badgeY + 8, Colors.White, subtitleFormat);

                // 4 Top KPI Cards
                float cardY = 125f;
                float cardH = 110f;
                float gap = 16f;
                float cardW = (W - 88 - (gap * 3)) / 4f;

                // KPI 1: Total Screen Time
                DrawKpiCard(ds, 44, cardY, cardW, cardH, "TOTAL SCREEN TIME", 
                    $"{(int)data.TotalDuration.TotalHours}h {data.TotalDuration.Minutes}m", 
                    $"{data.DailyAverage.Hours}h {data.DailyAverage.Minutes}m daily avg", 
                    Color.FromArgb(255, 99, 102, 241), kpiLabelFormat, kpiValueFormat, smallFormat);

                // KPI 2: Week-over-Week Trend
                string trendStr = data.WeekOverWeekPercent >= 0 ? $"+{data.WeekOverWeekPercent:F1}%" : $"{data.WeekOverWeekPercent:F1}%";
                string trendDesc = data.TrendIsGood ? "Lower than last week" : "Higher screen time";
                Color trendColor = data.TrendIsGood ? Color.FromArgb(255, 16, 185, 129) : Color.FromArgb(255, 244, 63, 94);
                DrawKpiCard(ds, 44 + (cardW + gap), cardY, cardW, cardH, "WEEK OVER WEEK", 
                    trendStr, trendDesc, trendColor, kpiLabelFormat, kpiValueFormat, smallFormat);

                // KPI 3: Active Days
                DrawKpiCard(ds, 44 + (cardW + gap) * 2, cardY, cardW, cardH, "ACTIVE DAYS", 
                    $"{data.TotalActiveDays} / 7", "Tracked this week", 
                    Color.FromArgb(255, 59, 130, 246), kpiLabelFormat, kpiValueFormat, smallFormat);

                // KPI 4: Est. Energy Cost
                DrawKpiCard(ds, 44 + (cardW + gap) * 3, cardY, cardW, cardH, "ESTIMATED ENERGY", 
                    data.EstimatedCostText, $"{UserPreferences.EstimatedPowerUsageWatts}W avg load", 
                    Color.FromArgb(255, 245, 158, 11), kpiLabelFormat, kpiValueFormat, smallFormat);

                // Section Headers
                float bodyY = 265f;
                float leftColW = 580f;
                float rightColX = 44 + leftColW + 24f;
                float rightColW = W - rightColX - 44f;

                // Left Panel: Top Apps Breakdown
                ds.FillRoundedRectangle(44, bodyY, leftColW, 440, 16, 16, Color.FromArgb(18, 255, 255, 255));
                ds.DrawRoundedRectangle(44, bodyY, leftColW, 440, 16, 16, Color.FromArgb(30, 255, 255, 255), 1f);
                ds.DrawText("🏆 TOP APPS & CATEGORIES", 64, bodyY + 20, Color.FromArgb(200, 255, 255, 255), kpiLabelFormat);

                float appRowY = bodyY + 54f;
                foreach (var app in data.TopApps)
                {
                    // App Name & Percentage
                    ds.DrawText(app.Name, 64, appRowY, Colors.White, appNameFormat);
                    string durStr = $"{(int)app.Duration.TotalHours}h {app.Duration.Minutes}m  •  {app.Percent:F1}%";
                    ds.DrawText(durStr, 64 + leftColW - 200, appRowY + 2, Color.FromArgb(180, 255, 255, 255), appDurFormat);

                    // Progress Bar
                    float barY = appRowY + 26f;
                    float maxBarW = leftColW - 40f;
                    ds.FillRoundedRectangle(64, barY, maxBarW, 8f, 4f, 4f, Color.FromArgb(35, 255, 255, 255));
                    float fillW = Math.Max(12f, (float)(maxBarW * (app.Percent / 100.0)));
                    ds.FillRoundedRectangle(64, barY, fillW, 8f, 4f, 4f, app.Color);

                    appRowY += 72f;
                }

                // Right Panel: Daily Breakdown Bar Chart
                ds.FillRoundedRectangle(rightColX, bodyY, rightColW, 440, 16, 16, Color.FromArgb(18, 255, 255, 255));
                ds.DrawRoundedRectangle(rightColX, bodyY, rightColW, 440, 16, 16, Color.FromArgb(30, 255, 255, 255), 1f);
                ds.DrawText("📊 DAILY SCREEN TIME PATTERN", rightColX + 20, bodyY + 20, Color.FromArgb(200, 255, 255, 255), kpiLabelFormat);

                // Draw Daily Vertical Bars
                float chartBottomY = bodyY + 380f;
                float maxChartH = 260f;
                double maxDaySec = data.DailyBreakdown.Count > 0 ? data.DailyBreakdown.Max(d => d.Duration.TotalSeconds) : 1;
                if (maxDaySec <= 0) maxDaySec = 1;

                float barColW = (rightColW - 40f) / Math.Max(1, data.DailyBreakdown.Count);
                for (int i = 0; i < data.DailyBreakdown.Count; i++)
                {
                    var day = data.DailyBreakdown[i];
                    float bx = rightColX + 20f + (i * barColW) + (barColW * 0.15f);
                    float bw = barColW * 0.70f;
                    float bh = Math.Max(8f, (float)((day.Duration.TotalSeconds / maxDaySec) * maxChartH));
                    float by = chartBottomY - bh;

                    Color barColor = day.IsHighest ? Color.FromArgb(255, 99, 102, 241) : Color.FromArgb(160, 59, 130, 246);
                    ds.FillRoundedRectangle(bx, by, bw, bh, 6f, 6f, barColor);

                    // Day Name
                    ds.DrawText(day.DayName, bx, chartBottomY + 12f, Color.FromArgb(160, 255, 255, 255), smallFormat);

                    // Hours Text above bar
                    if (day.Duration.TotalMinutes >= 15)
                    {
                        string dStr = $"{(int)day.Duration.TotalHours}h";
                        ds.DrawText(dStr, bx, by - 16f, Color.FromArgb(190, 255, 255, 255), smallFormat);
                    }
                }

                // Footer
                string footerText = $"Generated by UsageLogger • Windows Digital Wellbeing • {DateTime.Now:yyyy-MM-dd HH:mm}";
                ds.DrawText(footerText, 44, H - 48, Color.FromArgb(100, 255, 255, 255), smallFormat);
            }

            var stream = new InMemoryRandomAccessStream();
            await renderTarget.SaveAsync(stream, CanvasBitmapFileFormat.Png);
            stream.Seek(0);
            return stream;
        }

        private static void DrawKpiCard(CanvasDrawingSession ds, float x, float y, float w, float h, 
            string label, string value, string sub, Color accent, 
            CanvasTextFormat labelFmt, CanvasTextFormat valFmt, CanvasTextFormat subFmt)
        {
            ds.FillRoundedRectangle(x, y, w, h, 14, 14, Color.FromArgb(22, 255, 255, 255));
            ds.DrawRoundedRectangle(x, y, w, h, 14, 14, Color.FromArgb(35, 255, 255, 255), 1f);

            // Left accent bar
            ds.FillRoundedRectangle(x, y + 16, 4f, h - 32, 2f, 2f, accent);

            ds.DrawText(label, x + 16, y + 16, Color.FromArgb(160, 255, 255, 255), labelFmt);
            ds.DrawText(value, x + 16, y + 36, Colors.White, valFmt);
            ds.DrawText(sub, x + 16, y + 78, Color.FromArgb(160, accent.R, accent.G, accent.B), subFmt);
        }
    }
}
