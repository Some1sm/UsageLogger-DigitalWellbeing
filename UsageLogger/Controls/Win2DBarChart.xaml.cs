using UsageLogger.Models;
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

namespace UsageLogger.Controls
{
    public sealed partial class Win2DBarChart : UserControl
    {
        // Per-Bar Animation State
        // Key: Date String (or Label) -> Start Time
        private Dictionary<string, DateTime> _animatingBars = new Dictionary<string, DateTime>();
        private const double ANIMATION_DURATION_MS = 600;

        public Win2DBarChart()
        {
            this.InitializeComponent();
            this.Unloaded += Win2DBarChart_Unloaded;
            this.Loaded += Win2DBarChart_Loaded;
            this.SizeChanged += Win2DBarChart_SizeChanged;
        }

        private void Win2DBarChart_Loaded(object sender, RoutedEventArgs e)
        {
             // Ensure canvas redraws when coming back from cached state
             // Use DispatcherQueue to delay the invalidation, ensuring layout is complete
             DispatcherQueue?.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
             {
                 Canvas?.Invalidate();
             });
             
             Microsoft.UI.Xaml.Media.CompositionTarget.Rendering += CompositionTarget_Rendering;
        }

        private void Win2DBarChart_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // Force redraw when size changes (e.g., layout pass completes)
            Canvas?.Invalidate();
        }

        private void Win2DBarChart_Unloaded(object sender, RoutedEventArgs e)
        {
            Microsoft.UI.Xaml.Media.CompositionTarget.Rendering -= CompositionTarget_Rendering;

            // Unsubscribe from collection changes if subscribed
            if (_subscribedCollection != null)
            {
                _subscribedCollection.CollectionChanged -= OnCollectionChanged;
                _subscribedCollection = null;
            }
        }

        private void CompositionTarget_Rendering(object sender, object e)
        {
            if (_animatingBars.Count > 0)
            {
                bool anyAnimating = false;
                List<string> keysToRemove = new List<string>();

                foreach (var kvp in _animatingBars)
                {
                    double elapsed = (DateTime.Now - kvp.Value).TotalMilliseconds;
                    if (elapsed < ANIMATION_DURATION_MS)
                    {
                        anyAnimating = true;
                    }
                    else
                    {
                        keysToRemove.Add(kvp.Key);
                    }
                }

                // Cleanup finished animations
                foreach (var key in keysToRemove)
                {
                    _animatingBars.Remove(key);
                }

                if (anyAnimating || keysToRemove.Count > 0)
                {
                    Canvas?.Invalidate();
                }
            }
        }
        
        private void SyncAnimations()
        {
            if (ItemsSource == null) return;

            var currentKeys = new HashSet<string>();
            foreach (var item in ItemsSource)
            {
                string key = GetItemKey(item);
                currentKeys.Add(key);

                // If this is a NEW bar we haven't seen in this session (or since it was last removed)
                // We should check if we should animate it. 
                // BUT wait, we need to distinguish "First Load" vs "App Running".
                // Actually, if it's not in our 'displayed' set, we animate it.
                // WE NEED A 'DISPLAYED KEYS' SET separate from 'ANIMATING KEYS'.
                
                // Let's refine: 
                // We trust the View Model to largely keep keys stable.
                // If key is NOT in _displayedBars, it's new -> Animate it + Add to _displayedBars.
                // If key IS in _displayedBars, do nothing.
                
                if (!_displayedBars.Contains(key))
                {
                    _displayedBars.Add(key);
                    // Start animation
                    _animatingBars[key] = DateTime.Now;
                }
            }
            
            // Prune _displayedBars that are no longer present? 
            // If we navigate away (Rolling Window), old dates drop off. We should remove them.
            // So if _displayedBars has keys not in currentKeys, remove them.
            
            // Use ToList to avoid modification exception
            var oldKeys = _displayedBars.Where(k => !currentKeys.Contains(k)).ToList();
            foreach (var k in oldKeys)
            {
                 _displayedBars.Remove(k);
                 _animatingBars.Remove(k); // Stop animating if removed
            }

            Canvas?.Invalidate();
        }

        private HashSet<string> _displayedBars = new HashSet<string>();

        private string GetItemKey(BarChartItem item)
        {
            // Use Date if available, else Label
            if (item.Date.HasValue) return item.Date.Value.ToString("yyyy-MM-dd");
            return item.Label ?? Guid.NewGuid().ToString();
        }

        private System.Collections.Specialized.INotifyCollectionChanged _subscribedCollection;

        public static readonly DependencyProperty ItemsSourceProperty =
            DependencyProperty.Register(nameof(ItemsSource), typeof(IEnumerable<BarChartItem>), typeof(Win2DBarChart), new PropertyMetadata(null, OnItemsSourceChanged));

        public IEnumerable<BarChartItem> ItemsSource
        {
            get => (IEnumerable<BarChartItem>)GetValue(ItemsSourceProperty);
            set => SetValue(ItemsSourceProperty, value);
        }

        private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is Win2DBarChart chart)
            {
                // Unsubscribe from old collection
                if (chart._subscribedCollection != null)
                {
                    chart._subscribedCollection.CollectionChanged -= chart.OnCollectionChanged;
                    chart._subscribedCollection = null;
                }

                // Subscribe to new collection if it's observable
                if (e.NewValue is System.Collections.Specialized.INotifyCollectionChanged newCollection)
                {
                    newCollection.CollectionChanged += chart.OnCollectionChanged;
                    chart._subscribedCollection = newCollection;
                }

                // Sync Animations instead of full trigger
                chart.SyncAnimations();
            }
        }

        private void OnCollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            // Redraw/Re-animate when items are added/removed from the observable collection
            SyncAnimations();
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
            
            if (chartWidth <= 0 || chartHeight <= 0) return;

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

            // Updated Layout: Fill available width with Uniform Spacing including Sides
            // "make it work like if sides also had a bar" -> Total gaps = Items + 1
            // Layout: GAP - BAR - GAP - BAR ... - GAP
            
            // 1. Calculate max possible bar width if we reserve space for (Items + 1) gaps of equal width?
            // Simplified approach: calculate RemainingSpace after applying MaxBarWidth clamp.
            
            // Max space available for Bars if Gap was 0:
            float maxPossibleBarWidth = chartWidth / items.Count;
            
            // Apply clamps (Default Max 50, but don't clamp min yet, wait to see if it fits)
            float barWidth = Math.Min(maxPossibleBarWidth, (float)MaxBarWidth);
            
            // Enforce minimum VISIBLE width (e.g. 1px). 
            // Previous min was 4, which might cause overflow on very small charts.
            // Let's try to maintain 4 if possible, but shrink if needed.
            if (barWidth < 4 && maxPossibleBarWidth >= 4) barWidth = 4;
            
            // 3. Calculate remaining space and distribute as gaps (Items.Count + 1 intervals)
            float totalBarWidth = items.Count * barWidth;
            float remainingSpace = chartWidth - totalBarWidth;
            
            // Handle Overflow (if min width caused it)
            if (remainingSpace < 0) 
            {
                 // We don't have enough space for bars + gaps.
                 // Shrink bars to fit exactly with 0 gap, or minimal gap?
                 // Let's prioritize fitting the bars.
                 remainingSpace = 0;
                 barWidth = chartWidth / items.Count; 
                 totalBarWidth = chartWidth;
            }

            // Distribute gaps: N+1 gaps
            float gap = remainingSpace / (items.Count + 1);
            
            // Calculate StartX
            // Start at MarginLeft + First Gap
            float startX = marginLeft + gap;

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

            // Calculate label stride to prevent overlap
            // Assume each label needs ~40px of width
            const float minLabelWidth = 40f;
            float spacePerBar = barWidth + gap;
            int labelStride = spacePerBar > 0 ? Math.Max(1, (int)Math.Ceiling(minLabelWidth / spacePerBar)) : 1;

            // Cache bar rects for hit testing
            _barRects.Clear();

            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                float targetBarHeight = (float)(item.Value / maxVal * chartHeight);
                if (targetBarHeight < 2 && item.Value > 0) targetBarHeight = 2; // Min height target

                // Apply Animation (Per-Bar)
                float animationFactor = 1f;
                string key = GetItemKey(item);
                if (_animatingBars.TryGetValue(key, out DateTime startTime))
                {
                    double elapsed = (DateTime.Now - startTime).TotalMilliseconds;
                    double t = Math.Clamp(elapsed / ANIMATION_DURATION_MS, 0, 1);
                    // Cubic Ease Out
                    animationFactor = 1f - (float)Math.Pow(1 - t, 3);
                    
                    // Note: If t >= 1, the Rendering loop will eventually remove it, 
                    // but for smooth rendering, we calculate it here based on real time.
                }

                float barHeight = targetBarHeight * animationFactor;

                float x = startX + i * (barWidth + gap);
                float y = height - marginBottom - barHeight;

                // Adjust hit rect to target height (so tooltip works even during animation)
                // Or maybe animate hit rect too? 
                // Let's use target height for hit rect so user can hover while it's growing
                float targetY = height - marginBottom - targetBarHeight;
                var rect = new Windows.Foundation.Rect(x, targetY, barWidth, targetBarHeight);
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
                
                // Dynamic corner radius to avoid artifacts on thin bars
                float radius = Math.Min(4, barWidth / 2);
                
                // Draw Animated Bar
                var drawRect = new Windows.Foundation.Rect(x, y, barWidth, barHeight);
                ds.FillRoundedRectangle(drawRect, radius, radius, barColor);

                // Only draw label if stride allows (prevents overlap)
                if (!string.IsNullOrEmpty(item.Label) && i % labelStride == 0)
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
