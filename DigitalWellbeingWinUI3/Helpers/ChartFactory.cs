using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace DigitalWellbeingWinUI3.Helpers
{
    /// <summary>
    /// Factory class for creating chart series and color palettes.
    /// Centralizes all LiveCharts2 visualization logic.
    /// </summary>
    public static class ChartFactory
    {
        /// <summary>
        /// Generates a multi-hue color palette based on the accent color.
        /// Colors are rotated by a fixed hue step for visual distinctness.
        /// </summary>
        /// <param name="baseColor">The base accent color</param>
        /// <param name="count">Number of colors to generate</param>
        /// <returns>List of SKColors for chart series</returns>
        public static List<SKColor> GenerateMultiHuePalette(SKColor baseColor, int count)
        {
            var palette = new List<SKColor>();
            baseColor.ToHsl(out float h, out float s, out float l);

            // Ensure count is at least 1
            if (count < 1) count = 1;

            // Step size: 25 degrees gives a nice analogous/triadic mix
            float step = 25f;

            for (int i = 0; i < count; i++)
            {
                float newH = (h + (i * step)) % 360f;
                palette.Add(SKColor.FromHsl(newH, s, l));
            }
            return palette;
        }

        /// <summary>
        /// Gets a theme-aware label paint (white for dark mode, black for light mode).
        /// </summary>
        public static SolidColorPaint GetLabelPaint(bool isDarkMode)
        {
            return new SolidColorPaint(isDarkMode ? SKColors.White : SKColors.Black);
        }

        /// <summary>
        /// Determines if the current app theme is dark mode.
        /// </summary>
        public static bool IsDarkMode()
        {
            try
            {
                var theme = Microsoft.UI.Xaml.Application.Current.RequestedTheme;
                return theme == Microsoft.UI.Xaml.ApplicationTheme.Dark;
            }
            catch
            {
                return true; // Default to dark mode
            }
        }

        /// <summary>
        /// Creates a linear gradient paint from top to bottom using the accent color.
        /// </summary>
        /// <param name="accent">Windows accent color</param>
        /// <returns>LinearGradientPaint for bar charts</returns>
        public static LinearGradientPaint CreateAccentGradient(Windows.UI.Color accent)
        {
            var skAccent = new SKColor(accent.R, accent.G, accent.B, accent.A);
            var skAccentDark = new SKColor(
                (byte)Math.Max(0, accent.R - 50),
                (byte)Math.Max(0, accent.G - 50),
                (byte)Math.Max(0, accent.B - 50));

            return new LinearGradientPaint(
                new SKColor[] { skAccent, skAccentDark },
                new SKPoint(0.5f, 0), // Top
                new SKPoint(0.5f, 1)); // Bottom
        }

        /// <summary>
        /// Creates a column series for bar charts with accent gradient.
        /// </summary>
        public static ColumnSeries<double> CreateColumnSeries(
            ObservableCollection<double> values,
            string name,
            LinearGradientPaint fill)
        {
            return new ColumnSeries<double>
            {
                Values = values,
                Name = name,
                YToolTipLabelFormatter = (point) => FormatHours(point.Coordinate.PrimaryValue),
                Fill = fill,
                Rx = 10,
                Ry = 10,
                MaxBarWidth = 35
            };
        }

        /// <summary>
        /// Formats hours as "Xh Ym Zs" for consistent tooltip display.
        /// </summary>
        private static string FormatHours(double hours)
        {
            TimeSpan t = TimeSpan.FromHours(hours);
            return $"{(int)t.TotalHours}h {t.Minutes}m {t.Seconds}s";
        }

        /// <summary>
        /// Creates a pie series for a single app usage item.
        /// </summary>
        public static PieSeries<double> CreatePieSeries(
            double value,
            string name,
            SKColor fill)
        {
            return new PieSeries<double>
            {
                Values = new ObservableCollection<double> { value },
                Name = name,
                ToolTipLabelFormatter = (point) => $"{point.Context.Series.Name}: {FormatMinutes(point.Coordinate.PrimaryValue)}",
                DataLabelsFormatter = (point) => point.Context.Series.Name,
                Fill = new SolidColorPaint(fill)
            };
        }

        /// <summary>
        /// Formats minutes as "HH:MM:SS" for consistent tooltip display.
        /// </summary>
        private static string FormatMinutes(double totalMinutes)
        {
            TimeSpan t = TimeSpan.FromMinutes(totalMinutes);
            return $"{(int)t.TotalHours:D2}:{t.Minutes:D2}:{t.Seconds:D2}";
        }

        /// <summary>
        /// Creates a "No Data" placeholder pie series.
        /// </summary>
        public static PieSeries<double> CreateNoDataSeries()
        {
            return new PieSeries<double>
            {
                Values = new ObservableCollection<double> { 1 },
                Name = "No Data",
                Fill = new SolidColorPaint(SKColors.LightGray)
            };
        }

        /// <summary>
        /// Creates an "Other Apps" aggregation pie series.
        /// </summary>
        public static PieSeries<double> CreateOtherAppsSeries(double totalMinutes)
        {
            return new PieSeries<double>
            {
                Values = new ObservableCollection<double> { totalMinutes },
                Name = "Other Apps",
                Fill = new SolidColorPaint(SKColors.Gray)
            };
        }
    }
}
