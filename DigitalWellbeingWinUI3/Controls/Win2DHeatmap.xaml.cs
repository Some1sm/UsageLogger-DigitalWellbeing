using DigitalWellbeingWinUI3.Models;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using Windows.UI;

namespace DigitalWellbeingWinUI3.Controls
{
    public sealed partial class Win2DHeatmap : UserControl
    {
        public event Action<int, int> CellClicked;

        public Win2DHeatmap()
        {
            this.InitializeComponent();
            this.Unloaded += Win2DHeatmap_Unloaded;
        }

        private void Win2DHeatmap_Unloaded(object sender, RoutedEventArgs e)
        {
            Canvas.RemoveFromVisualTree();
            Canvas = null;
        }

        public static readonly DependencyProperty ItemsSourceProperty =
            DependencyProperty.Register(nameof(ItemsSource), typeof(IEnumerable<HeatmapDataPoint>), typeof(Win2DHeatmap), new PropertyMetadata(null, OnItemsSourceChanged));

        public IEnumerable<HeatmapDataPoint> ItemsSource
        {
            get => (IEnumerable<HeatmapDataPoint>)GetValue(ItemsSourceProperty);
            set => SetValue(ItemsSourceProperty, value);
        }

        private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is Win2DHeatmap chart && chart.Canvas != null)
            {
                chart.Canvas.Invalidate();
            }
        }

        private void Canvas_Draw(CanvasControl sender, CanvasDrawEventArgs args)
        {
            if (ItemsSource == null) return;
            var items = ItemsSource.ToList();
            
            var ds = args.DrawingSession;
            float width = (float)sender.ActualWidth;
            float height = (float)sender.ActualHeight;
            float margin = 20; // For labels
            float gridWidth = width - margin;
            float gridHeight = height - margin;
            
            float cellWidth = gridWidth / 24f;
            float cellHeight = gridHeight / 7f;

            // Draw Items
            foreach (var item in items)
            {
                // X = Hour (0-23), Y = Day (0-6)
                float x = margin + item.HourOne * cellWidth;
                float y = margin + item.DayOfWeek * cellHeight;
                
                // Opacity based on intensity (simple mapping)
                // Max intensity in a week is 60 mins.
                float opacity = (float)(item.Intensity / 60.0);
                if (opacity > 1) opacity = 1;
                if (opacity < 0.1 && item.Intensity > 0) opacity = 0.1f;
                
                if (item.Intensity > 0)
                {
                    Color c = item.Color;
                    c.A = (byte)(opacity * 255);
                    ds.FillRectangle(x, y, cellWidth - 1, cellHeight - 1, c);
                }
                else
                {
                    ds.FillRectangle(x, y, cellWidth - 1, cellHeight - 1, Color.FromArgb(20, 100, 100, 100)); // faint grid
                }
            }
            
            // Draw Axis Labels (Simplified)
            // Y-Axis: Days
            string[] days = { "S", "M", "T", "W", "T", "F", "S" };
            for(int i=0; i<7; i++)
            {
                 ds.DrawText(days[i], 0, margin + i * cellHeight + cellHeight/2 - 6, Colors.Gray, new Microsoft.Graphics.Canvas.Text.CanvasTextFormat { FontSize = 10 });
            }
            
            // X-Axis: Hours (every 6 hours)
            for(int i=0; i<24; i+=6)
            {
                ds.DrawText(i.ToString(), margin + i * cellWidth, 0, Colors.Gray, new Microsoft.Graphics.Canvas.Text.CanvasTextFormat { FontSize = 10 });
            }
        }

        private void Canvas_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            var pt = e.GetCurrentPoint(Canvas).Position;
            var cell = GetCellAt(pt.X, pt.Y);
            if (cell != null && ItemsSource != null)
            {
                var item = ItemsSource.FirstOrDefault(i => i.DayOfWeek == cell.Value.day && i.HourOne == cell.Value.hour);
                if (item != null)
                {
                    Canvas.Opacity = 0.8;
                    ToolTipService.SetToolTip(Canvas, item.Tooltip);
                    return;
                }
            }
            Canvas.Opacity = 1.0;
            ToolTipService.SetToolTip(Canvas, null);
        }

        private void Canvas_PointerExited(object sender, PointerRoutedEventArgs e)
        {
             Canvas.Opacity = 1.0;
             ToolTipService.SetToolTip(Canvas, null);
        }

        private void Canvas_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            var pt = e.GetCurrentPoint(Canvas).Position;
            var cell = GetCellAt(pt.X, pt.Y);
            if (cell != null)
            {
                CellClicked?.Invoke(cell.Value.day, cell.Value.hour);
            }
        }

        private (int day, int hour)? GetCellAt(double x, double y)
        {
            float width = (float)Canvas.ActualWidth;
            float height = (float)Canvas.ActualHeight;
            float margin = 20;
            float gridWidth = width - margin;
            float gridHeight = height - margin;
            
            float cellWidth = gridWidth / 24f;
            float cellHeight = gridHeight / 7f;

            if (x < margin || y < margin) return null;

            int hour = (int)((x - margin) / cellWidth);
            int day = (int)((y - margin) / cellHeight);

            if (hour >= 0 && hour < 24 && day >= 0 && day < 7)
            {
                return (day, hour);
            }
            return null;
        }
    }
}
