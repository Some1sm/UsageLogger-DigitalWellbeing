using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using DigitalWellbeingWinUI3.ViewModels;
using DigitalWellbeingWinUI3.Models;
using SkiaSharp;
using SkiaSharp.Views.Windows;
using System;
using System.Linq;
using Microsoft.UI.Xaml.Media;

namespace DigitalWellbeingWinUI3.Views.Controls
{
    public sealed partial class DayTimelineControl : UserControl
    {
        public DayTimelineViewModel ViewModel
        {
            get => (DayTimelineViewModel)DataContext;
            set => DataContext = value;
        }

        private SessionBlock _hoveredBlock = null;
        private double _lastCanvasHeight = 1440;
        private DayTimelineViewModel _subscribedViewModel = null;

        public DayTimelineControl()
        {
            this.InitializeComponent();
            this.Loaded += (s, e) => 
            {
                TimelineCanvas?.Invalidate();
                if (ViewModel != null) _lastCanvasHeight = ViewModel.CanvasHeight;
            };
            this.DataContextChanged += OnDataContextChanged;
            
            // Safety net: Invalidate when canvas size changes (after layout completes)
            TimelineCanvas.SizeChanged += (s, e) => 
            {
                TimelineCanvas?.Invalidate();
            };
        }
        
        // Virtualization: Invalidate canvas when scroll position changes
        private void TimelineScrollViewer_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            TimelineCanvas?.Invalidate();
        }

        private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
        {
            // Unsubscribe from old ViewModel
            if (_subscribedViewModel != null)
            {
                _subscribedViewModel.PropertyChanged -= OnViewModelPropertyChanged;
                _subscribedViewModel = null;
            }

            // Subscribe to new ViewModel
            if (ViewModel != null)
            {
                _subscribedViewModel = ViewModel;
                _lastCanvasHeight = ViewModel.CanvasHeight;
                _subscribedViewModel.PropertyChanged += OnViewModelPropertyChanged;
                TimelineCanvas?.Invalidate();
                
                // Delayed Invalidate to catch race with async data loading
                DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
                {
                    TimelineCanvas?.Invalidate();
                });
            }
        }

        private void OnViewModelPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs args)
        {
            if (args.PropertyName == nameof(DayTimelineViewModel.CanvasHeight))
            {
                // Anchor scroll: preserve center time on zoom
                double oldHeight = _lastCanvasHeight;
                double newHeight = ViewModel?.CanvasHeight ?? oldHeight;
                
                if (oldHeight > 0 && newHeight > 0 && TimelineScrollViewer != null)
                {
                    double viewportHeight = TimelineScrollViewer.ViewportHeight;
                    double centerOffset = TimelineScrollViewer.VerticalOffset + (viewportHeight / 2.0);
                    double ratio = centerOffset / oldHeight;
                    
                    double newCenterOffset = ratio * newHeight;
                    double newScrollTop = Math.Max(0, newCenterOffset - (viewportHeight / 2.0));
                    
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        TimelineScrollViewer?.ChangeView(null, newScrollTop, null, true);
                    });
                }
                _lastCanvasHeight = newHeight;
            }
            
            if (args.PropertyName == nameof(DayTimelineViewModel.SessionBlocks) ||
                args.PropertyName == nameof(DayTimelineViewModel.GridLines) ||
                args.PropertyName == nameof(DayTimelineViewModel.CanvasHeight) ||
                args.PropertyName == nameof(DayTimelineViewModel.ContentWidth))
            {
                //TimelineCanvas?.Invalidate();
            }
        }

        private void TimelineCanvas_PaintSurface(object sender, SKPaintSurfaceEventArgs e)
        {
            try
            {
                var canvas = e.Surface.Canvas;
                canvas.Clear(SKColors.Transparent);

                if (ViewModel == null) return;
                
                // Safety check for dimensions
                if (ViewModel.ContentWidth <= 0 || ViewModel.CanvasHeight <= 0) return;

                // Handle DPI Scaling
                // e.Info.Width is in Pixels. ViewModel properties are in DIPs.
                // We need to scale the canvas to match DIPs.
                float scale = (float)e.Info.Width / (float)this.ActualWidth;
                if (float.IsNaN(scale) || scale <= 0) scale = 1;
                
                canvas.Scale(scale);
                
                // Virtualization: Apply scroll offset translation
                float scrollOffset = (float)TimelineScrollViewer.VerticalOffset;
                canvas.Translate(0, -scrollOffset);

                float width = (float)ViewModel.ContentWidth;

                // Draw Grid Lines
                using var linePaint = new SKPaint { Color = SKColors.Gray.WithAlpha(50), StrokeWidth = 1, IsAntialias = true };
                using var textPaint = new SKPaint { Color = SKColors.Gray, TextSize = 11, IsAntialias = true };
                
                if (ViewModel.GridLines != null)
                {
                    foreach (var line in ViewModel.GridLines)
                    {
                        float y = (float)line.Top;
                        byte alpha = (byte)(line.Opacity * 255);
                        linePaint.Color = SKColors.Gray.WithAlpha(alpha);
                        canvas.DrawLine(0, y, width, y, linePaint);

                        if (!string.IsNullOrEmpty(line.TimeText))
                        {
                            textPaint.TextSize = (float)line.FontSize;
                            float textWidth = textPaint.MeasureText(line.TimeText);
                            canvas.DrawText(line.TimeText, width - textWidth - 35, y + 4, textPaint);
                        }
                    }
                }

                // Draw Session Blocks
                if (ViewModel.SessionBlocks != null)
                {
                    foreach (var block in ViewModel.SessionBlocks)
                    {
                        float top = (float)block.Top;
                        float height = (float)block.Height;
                        float left = (float)block.Left;
                        float blockWidth = (float)block.Width - 140; // Right margin

                        if (blockWidth < 1) blockWidth = 1;
                        if (height < 1) height = 1;

                        var skColor = BrushToSKColor(block.BackgroundColor);

                        // Left Accent Bar
                        using var accentPaint = new SKPaint { Color = skColor.WithAlpha(255), IsAntialias = true };
                        var accentRect = new SKRect(left, top, left + 4, top + height);
                        canvas.DrawRoundRect(accentRect, 2, 2, accentPaint);

                        // Main Block
                        using var blockPaint = new SKPaint { Color = skColor.WithAlpha(50), IsAntialias = true };
                        var mainRect = new SKRect(left + 4, top, left + blockWidth, top + height);
                        canvas.DrawRoundRect(mainRect, 0, 4, blockPaint);

                        // AFK Overlay
                        if (block.IsAfk)
                        {
                            using var afkPaint = new SKPaint { Color = new SKColor(255, 193, 7, 100), IsAntialias = true };
                            canvas.DrawRoundRect(mainRect, 0, 4, afkPaint);
                        }

                        // Text
                        if (block.ShowDetails && height > 16)
                        {
                            using var titlePaint = new SKPaint 
                            { 
                                Color = SKColors.White, 
                                TextSize = 12, 
                                IsAntialias = true,
                                Typeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold)
                            };
                            
                            string title = block.Title ?? "";
                            if (title.Length > 25) title = title.Substring(0, 25) + "...";
                            
                            float textY = top + 14;
                            canvas.DrawText(title, left + 12, textY, titlePaint);

                            if (height > 32)
                            {
                                using var durationPaint = new SKPaint
                                {
                                    Color = SKColors.White.WithAlpha(180),
                                    TextSize = 10,
                                    IsAntialias = true
                                };
                                canvas.DrawText(block.DurationText ?? "", left + 12, textY + 14, durationPaint);
                            }
                        }

                        // Audio indicator
                        if (block.HasAudio && blockWidth > 120)
                        {
                            using var audioPaint = new SKPaint { Color = SKColors.LightBlue.WithAlpha(200), IsAntialias = true };
                            var audioRect = new SKRect(left + blockWidth - 40, top + 2, left + blockWidth - 4, top + height - 2);
                            if (audioRect.Height > 8)
                            {
                                canvas.DrawRoundRect(audioRect, 4, 4, audioPaint);
                                using var audioTextPaint = new SKPaint { Color = SKColors.White, TextSize = 9, IsAntialias = true };
                                canvas.DrawText("♪", audioRect.Left + 12, audioRect.Top + 12, audioTextPaint);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Fallback: Draw usage error text on canvas so we know it crashed
                var canvas = e.Surface.Canvas;
                using var errorPaint = new SKPaint { Color = SKColors.Red, TextSize = 12 };
                canvas.DrawText("Render Error", 10, 20, errorPaint);
                System.Diagnostics.Debug.WriteLine($"Timeline Render Error: {ex}");
            }
        }

        private void TimelineCanvas_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (ViewModel == null) return;

            var pos = e.GetCurrentPoint(TimelineCanvas).Position;
            // Virtualization: Add scroll offset to map screen Y to content Y
            double y = pos.Y + TimelineScrollViewer.VerticalOffset;
            double x = pos.X;

            // Find deepest (smallest, most specific) block under cursor
            var candidates = ViewModel.SessionBlocks
                .Where(b => y >= b.Top && y <= b.Top + b.Height && x >= b.Left && x <= b.Left + b.Width - 140)
                .OrderBy(b => b.Height) // Prefer smaller (more specific) blocks
                .ToList();

            var hit = candidates.FirstOrDefault();

            if (hit != null && hit != _hoveredBlock)
            {
                _hoveredBlock = hit;

                TooltipTitle.Text = hit.Title;
                TooltipDuration.Text = hit.DurationText + (hit.IsAfk ? " [AFK]" : "") + (hit.HasAudio ? $" ♪ {hit.AudioSourcesText}" : "");

                // Position tooltip
                double tooltipX = Math.Min(x + 10, ActualWidth - 200);
                double tooltipY = Math.Min(y + 10, ActualHeight - 60);

                TooltipOverlay.RenderTransform = new TranslateTransform { X = tooltipX, Y = tooltipY };
                TooltipOverlay.Visibility = Visibility.Visible;
            }
            else if (hit == null)
            {
                _hoveredBlock = null;
                TooltipOverlay.Visibility = Visibility.Collapsed;
            }
        }

        private void TimelineCanvas_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            _hoveredBlock = null;
            TooltipOverlay.Visibility = Visibility.Collapsed;
        }

        private static SKColor BrushToSKColor(Brush brush)
        {
            if (brush is SolidColorBrush scb)
            {
                var c = scb.Color;
                return new SKColor(c.R, c.G, c.B, c.A);
            }
            return SKColors.Gray;
        }

    }
}
