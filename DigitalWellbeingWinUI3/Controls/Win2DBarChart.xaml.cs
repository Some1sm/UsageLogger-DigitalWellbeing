using DigitalWellbeingWinUI3.Models;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.Graphics.Canvas.Text;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Windows.Input;
using Windows.UI;

namespace DigitalWellbeingWinUI3.Controls
{
    public sealed partial class Win2DBarChart : UserControl
    {
        public Win2DBarChart()
        {
            this.InitializeComponent();
            this.Unloaded += Win2DBarChart_Unloaded;
        }

        private void Win2DBarChart_Unloaded(object sender, RoutedEventArgs e)
        {
            Canvas.RemoveFromVisualTree();
            Canvas = null;
        }

        public static readonly DependencyProperty ItemsSourceProperty =
            DependencyProperty.Register(nameof(ItemsSource), typeof(IEnumerable<BarChartItem>), typeof(Win2DBarChart), new PropertyMetadata(null, OnItemsSourceChanged));

        public IEnumerable<BarChartItem> ItemsSource
        {
            get => (IEnumerable<BarChartItem>)GetValue(ItemsSourceProperty);
            set => SetValue(ItemsSourceProperty, value);
        }

        private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is Win2DBarChart chart && chart.Canvas != null)
            {
                chart.Canvas.Invalidate();
            }
        }

        public static readonly DependencyProperty TargetLineValueProperty =
            DependencyProperty.Register(nameof(TargetLineValue), typeof(double), typeof(Win2DBarChart), new PropertyMetadata(0.0, OnPropertyChanged));

        public double TargetLineValue
        {
            get => (double)GetValue(TargetLineValueProperty);
            set => SetValue(TargetLineValueProperty, value);
        }

        public static readonly DependencyProperty TargetLineLabelProperty =
            DependencyProperty.Register(nameof(TargetLineLabel), typeof(string), typeof(Win2DBarChart), new PropertyMetadata("", OnPropertyChanged));

        public string TargetLineLabel
        {
            get => (string)GetValue(TargetLineLabelProperty);
            set => SetValue(TargetLineLabelProperty, value);
        }

        public static readonly DependencyProperty MaxBarWidthProperty =
            DependencyProperty.Register(nameof(MaxBarWidth), typeof(double), typeof(Win2DBarChart), new PropertyMetadata(50.0, OnPropertyChanged));

        public double MaxBarWidth
        {
            get => (double)GetValue(MaxBarWidthProperty);
            set => SetValue(MaxBarWidthProperty, value);
        }

        private static void OnPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is Win2DBarChart chart && chart.Canvas != null)
            {
                chart.Canvas.Invalidate();
            }
        }

        private void Canvas_Draw(CanvasControl sender, CanvasDrawEventArgs args)
        {
            if (ItemsSource == null) return;
            var items = ItemsSource.ToList();
            if (items.Count == 0) return;

            var ds = args.DrawingSession;
            float width = (float)sender.ActualWidth;
            float height = (float)sender.ActualHeight;

            // Updated Margins for Legend
            float marginLeft = 45;
            float marginBottom = 20;
            float marginOther = 15;
            
            float chartHeight = height - marginBottom - marginOther;
            float chartWidth = width - marginLeft - marginOther;

            // SCALING LOGIC:
            // 1. Find max data value
            double maxDataVal = items.Max(i => i.Value);
            
            // 2. Scale against TargetLine (if set) to avoid "huge bars"
            double maxVal = TargetLineValue > 0 ? Math.Max(maxDataVal, TargetLineValue) : maxDataVal;
            if (maxVal <= 0.1) maxVal = 1;

            // DRAW Y-AXIS LEGEND & GRID LINES
            int stepCount = 4;
            var labelFormat = new CanvasTextFormat { FontSize = 10, HorizontalAlignment = CanvasHorizontalAlignment.Right, VerticalAlignment = CanvasVerticalAlignment.Center, FontFamily = "Segoe UI" };
            Color gridColor = Color.FromArgb(40, 128, 128, 128); // Faint gray

            for (int i = 0; i <= stepCount; i++)
            {
                double val = (maxVal / stepCount) * i;
                float yPos = height - marginBottom - (float)(val / maxVal * chartHeight);
                
                // Label
                ds.DrawText($"{val:F1}h", marginLeft - 5, yPos, Colors.Gray, labelFormat);
                
                // Grid Line (except for the bottom axis which we draw later)
                if (i > 0)
                {
                    ds.DrawLine(marginLeft, yPos, width - marginOther, yPos, gridColor, 1);
                }
            }

            // Updated Layout: Fill available width
            float totalGapSpace = chartWidth * 0.2f; // 20% gap total
            float gap = items.Count > 1 ? totalGapSpace / (items.Count - 1) : 0;
            float barWidth = (chartWidth - totalGapSpace) / items.Count;
            if (items.Count == 1) 
            {
                 barWidth = Math.Min(100, chartWidth * 0.5f);
                 gap = 0;
            }
            // Clamp min/max bar width
            if (barWidth > MaxBarWidth) barWidth = (float)MaxBarWidth;
            if (barWidth < 4) barWidth = 4;
            
            // Re-calculate startX based on new marginLeft
            float totalContentWidth = items.Count * barWidth + (items.Count - 1) * gap;
            float startX = marginLeft + (chartWidth - totalContentWidth) / 2;

            // Draw X-Axis Line (Bottom Anchor)
            ds.DrawLine(marginLeft, height - marginBottom, width - marginOther, height - marginBottom, Colors.Gray, 1);

            // DRAW TARGET LINE (Average)
            if (TargetLineValue > 0 && TargetLineValue <= maxVal)
            {
                float targetY = height - marginBottom - (float)(TargetLineValue / maxVal * chartHeight);
                
                var strokeStyle = new CanvasStrokeStyle { DashStyle = CanvasDashStyle.Dash };
                ds.DrawLine(marginLeft, targetY, width - marginOther, targetY, Colors.Orange, 2, strokeStyle);
                
                // Optional Label
                // ds.DrawText("Avg", width - marginOther - 20, targetY - 10, Colors.Orange, new CanvasTextFormat { FontSize = 10 });
            }

            // Cache bar rects for hit testing
            _barRects.Clear();

            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                float barHeight = (float)(item.Value / maxVal * chartHeight);
                if (barHeight < 2 && item.Value > 0) barHeight = 2; // Min height

                float x = startX + i * (barWidth + gap);
                float y = height - marginBottom - barHeight;

                var rect = new Windows.Foundation.Rect(x, y, barWidth, barHeight);
                _barRects.Add(rect);

                // HOVER HIGHLIGHT LOGIC
                Color barColor = item.Color;
                if (i == _hoverIndex)
                {
                     barColor = Color.FromArgb(item.Color.A, 
                        (byte)Math.Min(255, item.Color.R + 40), 
                        (byte)Math.Min(255, item.Color.G + 40), 
                        (byte)Math.Min(255, item.Color.B + 40));
                }
                
                ds.FillRoundedRectangle(rect, 4, 4, barColor);

                if (!string.IsNullOrEmpty(item.Label))
                {
                    var format = new CanvasTextFormat { FontSize = 10, HorizontalAlignment = CanvasHorizontalAlignment.Center, FontFamily = "Segoe UI" };
                    ds.DrawText(item.Label, x + barWidth / 2, height - marginBottom + 2, Colors.Gray, format);
                }
            }

            // TOOLTIP OVERLAY (Draw last to stay on top)
            if (_hoverIndex != -1 && _hoverIndex < items.Count)
            {
                var hoveredItem = items[_hoverIndex];
                
                string tooltipText = hoveredItem.Tooltip ?? "";
                if (!string.IsNullOrEmpty(tooltipText))
                {
                    // Measure text - provide ample width to prevent wrapping
                    var format = new CanvasTextFormat { FontSize = 12, FontFamily = "Segoe UI", HorizontalAlignment = CanvasHorizontalAlignment.Center, VerticalAlignment = CanvasVerticalAlignment.Center };
                    var layout = new CanvasTextLayout(ds, tooltipText, format, 100.0f, 0.0f);
                    
                    // Force fit logic: Get actual text width
                    float textWidth = (float)layout.LayoutBounds.Width;
                    float textHeight = (float)layout.LayoutBounds.Height;
                    
                    // If text is wrapping (height > fontsize * 1.5), try re-measuring with wider constraint
                    if (textHeight > 16) 
                    {
                         layout.Dispose();
                         layout = new CanvasTextLayout(ds, tooltipText, format, 200.0f, 0.0f);
                         textWidth = (float)layout.LayoutBounds.Width;
                         textHeight = (float)layout.LayoutBounds.Height;
                    }

                    float padding = 6;
                    float tipWidth = textWidth + padding * 2;
                    float tipHeight = textHeight + padding * 2;

                    // Position: FOLLOW MOUSE
                    // Offset slightly to bottom-right of cursor to avoid blocking it
                    float tipX = (float)_lastMousePos.X + 12;
                    float tipY = (float)_lastMousePos.Y + 12;

                    // Clamp to bounds
                    if (tipX + tipWidth > width) tipX = (float)_lastMousePos.X - tipWidth - 4; // Flip to left if too far right
                    if (tipY + tipHeight > height) tipY = height - tipHeight; // Clamp bottom

                    // Draw Background
                    ds.FillRoundedRectangle(tipX, tipY, tipWidth, tipHeight, 6, 6, Color.FromArgb(255, 43, 43, 43)); 
                    ds.DrawRoundedRectangle(tipX, tipY, tipWidth, tipHeight, 6, 6, Color.FromArgb(255, 68, 68, 68), 1);

                    // Draw Text
                    // We need to re-center or just draw at position.
                    // Since layout was created with specific width, we draw the text geometry or just simple text if alignment is an issue.
                    // Actually simple DrawText is easier if we don't need complex wrapping.
                    // Let's use DrawText for simplicity and guaranteed single line.
                    ds.DrawText(tooltipText, tipX + padding + textWidth/2, tipY + padding + textHeight/2, Colors.White, format);
                    
                    layout.Dispose();
                }
            }
        }

        private List<Windows.Foundation.Rect> _barRects = new List<Windows.Foundation.Rect>();
        private int _hoverIndex = -1;
        private Windows.Foundation.Point _lastMousePos;

        private void Canvas_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            var pt = e.GetCurrentPoint(Canvas).Position;
            _lastMousePos = pt;
            int newIndex = -1;
            
            for (int i = 0; i < _barRects.Count; i++)
            {
                if (_barRects[i].Contains(pt))
                {
                    newIndex = i;
                    break;
                }
            }

            // Always invalidate if hovering a bar OR if moving within a bar (to update tooltip pos)
            bool isHovering = newIndex != -1;
            bool wasHovering = _hoverIndex != -1;

            if (isHovering || wasHovering)
            {
                _hoverIndex = newIndex;
                Canvas?.Invalidate(); // Redraw for tooltip follow
            }
        }

        private void Canvas_PointerExited(object sender, PointerRoutedEventArgs e)
        {
             if (_hoverIndex != -1)
             {
                 _hoverIndex = -1;
                 Canvas?.Invalidate();
             }
        }

        public static readonly DependencyProperty ItemClickCommandProperty =
            DependencyProperty.Register(nameof(ItemClickCommand), typeof(ICommand), typeof(Win2DBarChart), new PropertyMetadata(null));

        public ICommand ItemClickCommand
        {
            get => (ICommand)GetValue(ItemClickCommandProperty);
            set => SetValue(ItemClickCommandProperty, value);
        }

        private void Canvas_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (_hoverIndex != -1 && ItemsSource != null)
            {
                var items = ItemsSource.ToList();
                if (_hoverIndex < items.Count)
                {
                    var clickedItem = items[_hoverIndex];
                    if (ItemClickCommand != null && ItemClickCommand.CanExecute(clickedItem))
                    {
                        ItemClickCommand.Execute(clickedItem);
                    }
                }
            }
        }
    }
}
