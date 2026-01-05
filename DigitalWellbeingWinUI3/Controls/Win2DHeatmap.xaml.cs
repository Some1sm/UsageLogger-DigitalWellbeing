using DigitalWellbeingWinUI3.Models;
using DigitalWellbeing.Core.Models;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Text;
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

        // Hover tracking
        private (int day, int hour)? _hoverCell = null;
        private Windows.Foundation.Point _lastMousePos;

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
            float margin = 28; // Increased for larger text
            float gridWidth = width - margin;
            float gridHeight = height - margin;
            
            float cellWidth = gridWidth / 24f;
            float cellHeight = gridHeight / 7f;

            HeatmapDataPoint hoveredItem = null;

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
                
                bool isHovered = _hoverCell.HasValue && 
                                 _hoverCell.Value.day == item.DayOfWeek && 
                                 _hoverCell.Value.hour == item.HourOne;
                
                if (isHovered)
                {
                    hoveredItem = item;
                }
                
                if (item.Intensity > 0)
                {
                    Color c = item.Color;
                    c.A = (byte)(opacity * 255);
                    ds.FillRectangle(x, y, cellWidth - 1, cellHeight - 1, c);
                    
                    // Draw highlight border for hovered cell
                    if (isHovered)
                    {
                        ds.DrawRectangle(x, y, cellWidth - 1, cellHeight - 1, Colors.White, 2);
                    }
                }
                else
                {
                    ds.FillRectangle(x, y, cellWidth - 1, cellHeight - 1, Color.FromArgb(20, 100, 100, 100)); // faint grid
                    
                    // Draw highlight border for hovered empty cell too
                    if (isHovered)
                    {
                        ds.DrawRectangle(x, y, cellWidth - 1, cellHeight - 1, Colors.Gray, 1);
                    }
                }
            }
            
            // Draw Axis Labels (Simplified)
            // Y-Axis: Days (Monday Start)
            string[] days = { "M", "T", "W", "T", "F", "S", "S" };
            for(int i=0; i<7; i++)
            {
                 ds.DrawText(days[i], 4, margin + i * cellHeight + cellHeight/2 - 8, Colors.Gray, new CanvasTextFormat { FontSize = 14, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
            }
            
            // X-Axis: Hours (every hour)
            for(int i=0; i<24; i++)
            {
                ds.DrawText(i.ToString(), margin + i * cellWidth + cellWidth/2 - 6, 2, Colors.Gray, new CanvasTextFormat { FontSize = 11 });
            }
            
            // Draw Tooltip for hovered cell (like other charts do)
            if (hoveredItem != null && !string.IsNullOrEmpty(hoveredItem.Tooltip))
            {
                string tooltipText = hoveredItem.Tooltip;
                var format = new CanvasTextFormat 
                { 
                    FontSize = 12, 
                    FontFamily = "Segoe UI",
                    HorizontalAlignment = CanvasHorizontalAlignment.Left, 
                    VerticalAlignment = CanvasVerticalAlignment.Top
                };

                using (var layout = new CanvasTextLayout(ds, tooltipText, format, 200.0f, 0.0f))
                {
                    float textWidth = (float)layout.LayoutBounds.Width;
                    float textHeight = (float)layout.LayoutBounds.Height;
                    float padding = 8;
                    float tipWidth = textWidth + padding * 2;
                    float tipHeight = textHeight + padding * 2;

                    float tipX = (float)_lastMousePos.X + 15;
                    float tipY = (float)_lastMousePos.Y + 15;

                    // Clamp to bounds
                    if (tipX + tipWidth > width) tipX = (float)_lastMousePos.X - tipWidth - 5;
                    if (tipY + tipHeight > height) tipY = (float)_lastMousePos.Y - tipHeight - 5;
                    if (tipX < 0) tipX = 5;
                    if (tipY < 0) tipY = 5;

                    ds.FillRoundedRectangle(tipX, tipY, tipWidth, tipHeight, 6, 6, Color.FromArgb(255, 43, 43, 43));
                    ds.DrawRoundedRectangle(tipX, tipY, tipWidth, tipHeight, 6, 6, Color.FromArgb(255, 68, 68, 68), 1);
                    ds.DrawTextLayout(layout, tipX + padding, tipY + padding, Colors.White);
                }
            }
        }

        private void Canvas_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (Canvas == null) return;
            
            var pt = e.GetCurrentPoint(Canvas).Position;
            _lastMousePos = pt;
            var cell = GetCellAt(pt.X, pt.Y);
            
            _hoverCell = cell;
            // Always invalidate so tooltip follows mouse
            Canvas.Invalidate();
        }

        private void Canvas_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (_hoverCell != null)
            {
                _hoverCell = null;
                Canvas?.Invalidate();
            }
        }

        private void Canvas_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (Canvas == null) return;
            
            var pt = e.GetCurrentPoint(Canvas).Position;
            var cell = GetCellAt(pt.X, pt.Y);
            if (cell != null)
            {
                CellClicked?.Invoke(cell.Value.day, cell.Value.hour);
            }
        }

        private (int day, int hour)? GetCellAt(double x, double y)
        {
            if (Canvas == null) return null;
            
            float width = (float)Canvas.ActualWidth;
            float height = (float)Canvas.ActualHeight;
            float margin = 28;
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
