using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using UsageLogger.ViewModels;
using UsageLogger.Models;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.Graphics.Canvas.Text;
using Microsoft.Graphics.Canvas.Geometry;
using System;
using System.Linq;
using System.Numerics;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using Microsoft.UI;
using static Microsoft.UI.Colors;

namespace UsageLogger.Views.Controls
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

        // Win2D Text Formats (cached for performance)
        private CanvasTextFormat _gridTextFormat;
        private CanvasTextFormat _titleTextFormat;
        private CanvasTextFormat _durationTextFormat;
        private CanvasTextFormat _audioTextFormat;
        
        // Win2D CanvasControl - created programmatically to bypass XAML compiler issues
        private CanvasControl TimelineCanvas;

        public DayTimelineControl()
        {
            this.InitializeComponent();
            
            // Create Win2D CanvasControl programmatically (bypasses XAML compiler issues with Win2D types)
            TimelineCanvas = new CanvasControl();
            TimelineCanvas.Draw += TimelineCanvas_Draw;
            TimelineCanvas.PointerMoved += TimelineCanvas_PointerMoved;
            TimelineCanvas.PointerExited += TimelineCanvas_PointerExited;
            CanvasContainer.Children.Add(TimelineCanvas);
            
            // Initialize Win2D text formats
            _gridTextFormat = new CanvasTextFormat { FontSize = 11, FontFamily = "Segoe UI" };
            _titleTextFormat = new CanvasTextFormat { FontSize = 12, FontFamily = "Segoe UI", FontWeight = Microsoft.UI.Text.FontWeights.Bold };
            _durationTextFormat = new CanvasTextFormat { FontSize = 10, FontFamily = "Segoe UI" };
            _audioTextFormat = new CanvasTextFormat { FontSize = 9, FontFamily = "Segoe UI" };
            
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
                TimelineCanvas?.Invalidate();
            }
        }

        private void TimelineCanvas_Draw(CanvasControl sender, CanvasDrawEventArgs args)
        {
            try
            {
                var ds = args.DrawingSession;
                ds.Clear(Colors.Transparent);

                if (ViewModel == null) return;
                
                // Safety check for dimensions
                if (ViewModel.ContentWidth <= 0 || ViewModel.CanvasHeight <= 0) return;

                // Get scroll offset for virtualization
                float scrollOffset = (float)TimelineScrollViewer.VerticalOffset;
                float width = (float)ViewModel.ContentWidth;

                // Draw Grid Lines
                if (ViewModel.GridLines != null)
                {
                    foreach (var line in ViewModel.GridLines)
                    {
                        float y = (float)line.Top - scrollOffset;
                        
                        // Skip lines outside viewport
                        if (y < -50 || y > sender.ActualHeight + 50) continue;
                        
                        byte alpha = (byte)(line.Opacity * 255);
                        var lineColor = Color.FromArgb(alpha, 128, 128, 128);
                        ds.DrawLine(0, y, width, y, lineColor, 1);

                        if (!string.IsNullOrEmpty(line.TimeText))
                        {
                            _gridTextFormat.FontSize = (float)line.FontSize;
                            var textLayout = new CanvasTextLayout(sender, line.TimeText, _gridTextFormat, 100, 20);
                            float textWidth = (float)textLayout.LayoutBounds.Width;
                            ds.DrawText(line.TimeText, width - textWidth - 35, y - 6, Colors.Gray, _gridTextFormat);
                        }
                    }
                }

                // Draw Session Blocks
                if (ViewModel.SessionBlocks != null)
                {
                    foreach (var block in ViewModel.SessionBlocks)
                    {
                        float top = (float)block.Top - scrollOffset;
                        float height = (float)block.Height;
                        float left = (float)block.Left;
                        float blockWidth = (float)block.Width - 140; // Right margin

                        // Skip blocks outside viewport
                        if (top + height < -50 || top > sender.ActualHeight + 50) continue;

                        if (blockWidth < 1) blockWidth = 1;
                        if (height < 1) height = 1;

                        var color = BrushToColor(block.BackgroundColor);

                        // Left Accent Bar
                        var accentColor = Color.FromArgb(255, color.R, color.G, color.B);
                        var accentRect = new Windows.Foundation.Rect(left, top, 4, height);
                        ds.FillRoundedRectangle(accentRect, 2, 2, accentColor);

                        // Main Block
                        var blockColor = Color.FromArgb(50, color.R, color.G, color.B);
                        var mainRect = new Windows.Foundation.Rect(left + 4, top, blockWidth - 4, height);
                        ds.FillRoundedRectangle(mainRect, 0, 4, blockColor);

                        // AFK Overlay
                        if (block.IsAfk)
                        {
                            var afkColor = Color.FromArgb(100, 255, 193, 7);
                            ds.FillRoundedRectangle(mainRect, 0, 4, afkColor);
                        }

                        // Text
                        if (block.ShowDetails && height > 16)
                        {
                            string title = block.Title ?? "";
                            if (title.Length > 25) title = title.Substring(0, 25) + "...";
                            
                            float textY = top + 2;
                            ds.DrawText(title, left + 12, textY, Colors.White, _titleTextFormat);

                            if (height > 32)
                            {
                                var durationColor = Color.FromArgb(180, 255, 255, 255);
                                ds.DrawText(block.DurationText ?? "", left + 12, textY + 14, durationColor, _durationTextFormat);
                            }
                        }

                        // Audio indicator
                        if (block.HasAudio && blockWidth > 120)
                        {
                            var audioColor = Color.FromArgb(200, 173, 216, 230);
                            var audioRect = new Windows.Foundation.Rect(left + blockWidth - 40, top + 2, 36, height - 4);
                            if (audioRect.Height > 8)
                            {
                                ds.FillRoundedRectangle(audioRect, 4, 4, audioColor);
                                ds.DrawText("♪", (float)audioRect.X + 12, (float)audioRect.Y + 2, Colors.White, _audioTextFormat);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Fallback: Draw usage error text on canvas so we know it crashed
                args.DrawingSession.DrawText("Render Error", 10, 20, Colors.Red);
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

            // Find the top-most (last drawn, visually on top) block under cursor
            // Using Reverse() to get the last block in draw order (which is on top)
            var candidates = ViewModel.SessionBlocks
                .Where(b => y >= b.Top && y <= b.Top + b.Height && x >= b.Left && x <= b.Left + b.Width - 140)
                .Reverse() // Prefer last-drawn (visually on top) blocks
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

        private static Color BrushToColor(Brush brush)
        {
            if (brush is SolidColorBrush scb)
            {
                var c = scb.Color;
                return Color.FromArgb(c.A, c.R, c.G, c.B);
            }
            return Colors.Gray;
        }
    }
}
