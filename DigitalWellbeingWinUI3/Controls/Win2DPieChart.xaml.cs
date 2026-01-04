using DigitalWellbeingWinUI3.Models;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Windows.UI;

namespace DigitalWellbeingWinUI3.Controls
{
    public sealed partial class Win2DPieChart : UserControl
    {
        public Win2DPieChart()
        {
            this.InitializeComponent();
            this.Unloaded += Win2DPieChart_Unloaded;
        }

        private void Win2DPieChart_Unloaded(object sender, RoutedEventArgs e)
        {
            Canvas.RemoveFromVisualTree();
            Canvas = null;
        }

        public static readonly DependencyProperty ItemsSourceProperty =
            DependencyProperty.Register(nameof(ItemsSource), typeof(IEnumerable<PieChartItem>), typeof(Win2DPieChart), new PropertyMetadata(null, OnItemsSourceChanged));

        public IEnumerable<PieChartItem> ItemsSource
        {
            get => (IEnumerable<PieChartItem>)GetValue(ItemsSourceProperty);
            set => SetValue(ItemsSourceProperty, value);
        }

        public static readonly DependencyProperty LegendItemsProperty =
            DependencyProperty.Register(nameof(LegendItems), typeof(IEnumerable<PieChartItem>), typeof(Win2DPieChart), new PropertyMetadata(null));

        public IEnumerable<PieChartItem> LegendItems
        {
            get => (IEnumerable<PieChartItem>)GetValue(LegendItemsProperty);
            set => SetValue(LegendItemsProperty, value);
        }

        private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is Win2DPieChart chart)
            {
                if (e.OldValue is System.Collections.Specialized.INotifyCollectionChanged oldList)
                {
                    oldList.CollectionChanged -= chart.OnCollectionChanged;
                }
                if (e.NewValue is System.Collections.Specialized.INotifyCollectionChanged newList)
                {
                    newList.CollectionChanged += chart.OnCollectionChanged;
                }

                chart.UpdateLegend();
                chart.Canvas.Invalidate();
            }
        }

        private void OnCollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            UpdateLegend();
            Canvas.Invalidate();
        }

        private void UpdateLegend()
        {
            if (ItemsSource != null)
            {
                LegendItems = ItemsSource.Take(10).ToList();
            }
        }

        
        public static readonly DependencyProperty HoleRadiusProperty =
            DependencyProperty.Register(nameof(HoleRadius), typeof(float), typeof(Win2DPieChart), new PropertyMetadata(0.0f, OnPropertyChanged));

        public float HoleRadius
        {
            get => (float)GetValue(HoleRadiusProperty);
            set => SetValue(HoleRadiusProperty, value);
        }

        public static readonly DependencyProperty IsDonutProperty =
            DependencyProperty.Register(nameof(IsDonut), typeof(bool), typeof(Win2DPieChart), new PropertyMetadata(false, OnPropertyChanged));

        public bool IsDonut
        {
            get => (bool)GetValue(IsDonutProperty);
            set => SetValue(IsDonutProperty, value);
        }

        private static void OnPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
             if (d is Win2DPieChart chart)
             {
                 chart.Canvas.Invalidate();
             }
        }
        

        private struct PieSlice { public float StartAngle; public float SweepAngle; public PieChartItem Item; }
        private List<PieSlice> _slices = new List<PieSlice>();
        private int _hoverIndex = -1;
        private Windows.Foundation.Point _lastMousePos;

        private void Canvas_Draw(CanvasControl sender, CanvasDrawEventArgs args)
        {
            if (ItemsSource == null) return;
            var items = ItemsSource.ToList();
            if (items.Count == 0) return;

            var ds = args.DrawingSession;
            float width = (float)sender.ActualWidth;
            float height = (float)sender.ActualHeight;
            float margin = 20;
            float diameter = Math.Min(width, height) - 2 * margin;
            float radius = diameter / 2;
            System.Numerics.Vector2 center = new System.Numerics.Vector2(width / 2, height / 2);

            double total = items.Sum(i => i.Value);
            if (total <= 0) total = 1;

            float currentAngle = -90f * (float)(Math.PI / 180.0); // Start at top
            _slices.Clear();

            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                float sweepAngle = (float)(item.Value / total * 2 * Math.PI);
                if (sweepAngle == 0) continue;

                var color = item.Color;
                Vector2 sliceCenter = center;
                float sliceRadius = radius;

                bool isHovered = (i == _hoverIndex);

                if (isHovered)
                {
                    float midAngle = currentAngle + sweepAngle / 2;
                    sliceCenter += new Vector2((float)Math.Cos(midAngle), (float)Math.Sin(midAngle)) * 10;
                }

                // Geometry Creation
                using (var builder = new CanvasPathBuilder(sender))
                {
                    builder.BeginFigure(sliceCenter);
                    builder.AddArc(sliceCenter, sliceRadius, sliceRadius, currentAngle, sweepAngle);
                    builder.EndFigure(CanvasFigureLoop.Closed);
                    
                    using (var geometry = CanvasGeometry.CreatePath(builder))
                    {
                         ds.FillGeometry(geometry, color);
                         if (isHovered)
                         {
                             ds.DrawGeometry(geometry, Colors.White, 2);
                         }
                         else
                         {
                             ds.DrawGeometry(geometry, Colors.Transparent, 1);
                         }
                    }
                }

                _slices.Add(new PieSlice { StartAngle = currentAngle, SweepAngle = sweepAngle, Item = item });
                currentAngle += sweepAngle;
            }

            // Draw Tooltip
            if (_hoverIndex != -1 && _hoverIndex < items.Count)
            {
                var item = items[_hoverIndex];
                string tooltipText = $"{item.Name}\n{item.Tooltip} ({item.Percentage:0.0}%)";
                var format = new Microsoft.Graphics.Canvas.Text.CanvasTextFormat 
                { 
                    FontSize = 13, 
                    HorizontalAlignment = Microsoft.Graphics.Canvas.Text.CanvasHorizontalAlignment.Left, // Align left for layout
                    VerticalAlignment = Microsoft.Graphics.Canvas.Text.CanvasVerticalAlignment.Top
                };

                using (var layout = new Microsoft.Graphics.Canvas.Text.CanvasTextLayout(ds, tooltipText, format, 250.0f, 0.0f))
                {
                    float textWidth = (float)layout.LayoutBounds.Width;
                    float textHeight = (float)layout.LayoutBounds.Height;
                    float padding = 10;
                    float tipWidth = textWidth + padding * 2;
                    float tipHeight = textHeight + padding * 2;

                    float tipX = (float)_lastMousePos.X + 15;
                    float tipY = (float)_lastMousePos.Y + 15;

                    if (tipX + tipWidth > width) tipX = (float)_lastMousePos.X - tipWidth - 5;
                    if (tipY + tipHeight > height) tipY = (float)_lastMousePos.Y - tipHeight - 5;

                    ds.FillRoundedRectangle(tipX, tipY, tipWidth, tipHeight, 6, 6, Color.FromArgb(255, 43, 43, 43));
                    ds.DrawRoundedRectangle(tipX, tipY, tipWidth, tipHeight, 6, 6, Color.FromArgb(255, 68, 68, 68), 1);
                    ds.DrawTextLayout(layout, tipX + padding, tipY + padding, Colors.White);
                }
            }
        }

        private void Canvas_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            var pt = e.GetCurrentPoint(Canvas).Position;
            _lastMousePos = pt;

            float width = (float)Canvas.ActualWidth;
            float height = (float)Canvas.ActualHeight;
            Vector2 center = new Vector2(width / 2, height / 2);
            Vector2 vec = new Vector2((float)pt.X, (float)pt.Y) - center;
            float dist = vec.Length();

            float margin = 20;
            float radius = (Math.Min(width, height) - 2 * margin) / 2;

            int newIndex = -1;

            if (dist <= radius + 10) // Small buffer for exploded slices
            {
                float angle = (float)Math.Atan2(vec.Y, vec.X);
                if (angle < -Math.PI / 2) angle += (float)(2 * Math.PI);

                for (int i = 0; i < _slices.Count; i++)
                {
                    var slice = _slices[i];
                    if (angle >= slice.StartAngle && angle < slice.StartAngle + slice.SweepAngle)
                    {
                        newIndex = i;
                        break;
                    }
                }
            }

            if (newIndex != _hoverIndex)
            {
                _hoverIndex = newIndex;
                Canvas.Invalidate();

                // Highlight in legend if possible
                if (_hoverIndex != -1 && LegendItems != null)
                {
                    var items = ItemsSource.ToList();
                    var hoveredItem = items[_hoverIndex];
                    var legendList = LegendItems.ToList();
                    if (legendList.Contains(hoveredItem))
                    {
                        LegendList.ScrollIntoView(hoveredItem);
                        // We can't easily "Select" if SelectionMode=None, 
                        // but ScrollIntoView gives visibility.
                    }
                }
            }
        }

        private void Canvas_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            _hoverIndex = -1;
            Canvas.Invalidate();
        }

        private void LegendItem_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is PieChartItem item && ItemsSource != null)
            {
                var itemsList = ItemsSource.ToList();
                int idx = itemsList.IndexOf(item);
                if (idx != -1)
                {
                    _hoverIndex = idx;
                    Canvas.Invalidate();
                }
            }
        }

        private void LegendItem_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            _hoverIndex = -1;
            Canvas.Invalidate();
        }
    }
}
