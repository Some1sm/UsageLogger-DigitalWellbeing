using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using SkiaSharp;
using SkiaSharp.Views.Windows;
using System;
using System.Linq;
using System.ComponentModel;
using DigitalWellbeingWinUI3.Models;
using DigitalWellbeingWinUI3.Helpers;
using DigitalWellbeingWinUI3.ViewModels;

namespace DigitalWellbeingWinUI3.Views.Controls
{
    public sealed partial class SkiaTimelineControl : UserControl
    {
        private SKXamlCanvas _canvas;
        private DayTimelineViewModel _subscribedViewModel;
        private Border _tooltipBorder;
        private TextBlock _tooltipText;

        public SkiaTimelineControl()
        {
            this.InitializeComponent();
            
            _canvas = new SKXamlCanvas();
            _canvas.PaintSurface += OnPaintSurface;
            _canvas.PointerMoved += OnPointerMoved;
            _canvas.PointerExited += OnPointerExited;
            _canvas.SizeChanged += OnCanvasSizeChanged;
            
            // Create tooltip
            _tooltipText = new TextBlock { FontSize = 12, Foreground = new SolidColorBrush(Microsoft.UI.Colors.White) };
            _tooltipBorder = new Border
            {
                Child = _tooltipText,
                Background = new SolidColorBrush(Microsoft.UI.Colors.Black),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8, 4, 8, 4),
                Visibility = Visibility.Collapsed,
                IsHitTestVisible = false
            };
            
            ContentGrid.Children.Add(_canvas);
            ContentGrid.Children.Add(_tooltipBorder);
            
            this.DataContextChanged += OnDataContextChanged;
            this.Loaded += OnLoaded;
        }
        
        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // Force a redraw when control is loaded
            InvalidateCanvas();
        }
        
        private void OnCanvasSizeChanged(object sender, SizeChangedEventArgs e)
        {
            // Force immediate redraw when size changes
            try { _canvas?.Invalidate(); } catch { }
        }

        private DayTimelineViewModel ViewModel => DataContext as DayTimelineViewModel;

        private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
        {
            if (_subscribedViewModel != null)
            {
                _subscribedViewModel.PropertyChanged -= OnViewModelPropertyChanged;
                _subscribedViewModel = null;
            }
            
            var vm = ViewModel;
            if (vm != null)
            {
                HeaderText.Text = vm.DateString;
                _canvas.Height = vm.CanvasHeight;
                ContentGrid.Height = vm.CanvasHeight;
                
                // Use TimelineWidth from ViewModel for consistent sizing
                double width = vm.TimelineWidth > 0 ? vm.TimelineWidth - 30 : 400;
                _canvas.Width = width;
                ContentGrid.Width = width;
                
                vm.PropertyChanged += OnViewModelPropertyChanged;
                _subscribedViewModel = vm;
                InvalidateCanvas();
            }
        }

        private void OnViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            try
            {
                var vm = ViewModel;
                if (vm == null || _canvas == null) return;
                
                if (e.PropertyName == nameof(DayTimelineViewModel.SessionBlocks) ||
                    e.PropertyName == nameof(DayTimelineViewModel.GridLines) ||
                    e.PropertyName == nameof(DayTimelineViewModel.CanvasHeight) ||
                    e.PropertyName == nameof(DayTimelineViewModel.CurrentTimeTop) ||
                    e.PropertyName == nameof(DayTimelineViewModel.CurrentTimeVisibility) ||
                    e.PropertyName == nameof(DayTimelineViewModel.TimelineWidth))
                {
                    if (e.PropertyName == nameof(DayTimelineViewModel.CanvasHeight))
                    {
                        var h = Math.Max(100, Math.Min(vm.CanvasHeight, 50000));
                        _canvas.Height = h;
                        ContentGrid.Height = h;
                    }
                    if (e.PropertyName == nameof(DayTimelineViewModel.TimelineWidth))
                    {
                        double width = vm.TimelineWidth > 0 ? vm.TimelineWidth - 30 : 400;
                        _canvas.Width = width;
                        ContentGrid.Width = width;
                    }
                    InvalidateCanvas();
                }
            }
            catch { }
        }

        private void InvalidateCanvas()
        {
            try
            {
                // Use DispatcherQueue to ensure we invalidate after layout is complete
                DispatcherQueue?.TryEnqueue(() =>
                {
                    try { _canvas?.Invalidate(); } catch { }
                });
            }
            catch { }
        }

        private void OnPaintSurface(object sender, SKPaintSurfaceEventArgs e)
        {
            try
            {
                var canvas = e.Surface.Canvas;
                var info = e.Info;
                canvas.Clear(SKColors.Transparent);
                
                var vm = ViewModel;
                if (vm == null) return;
                
                float width = info.Width;
                float actualWidth = (float)_canvas.ActualWidth;
                
                // Fallback width calculation if ActualWidth is 0
                if (actualWidth <= 0)
                {
                    actualWidth = vm.TimelineWidth > 0 ? (float)(vm.TimelineWidth - 30) : 400f;
                }
                if (width <= 0) width = actualWidth;
                
                float scale = width / Math.Max(actualWidth, 1);
                
                bool drewGridLines = DrawGridLines(canvas, width, scale, vm);
                bool drewBlocks = DrawSessionBlocks(canvas, width, scale, vm);
                
                if (vm.CurrentTimeVisibility == Visibility.Visible)
                    DrawCurrentTimeIndicator(canvas, width, scale, vm);
                
                // If we didn't draw anything but data should exist, retry after a short delay
                if (!drewGridLines && !drewBlocks && vm.SessionBlocks != null)
                {
                    DispatcherQueue?.TryEnqueue(async () =>
                    {
                        await System.Threading.Tasks.Task.Delay(50);
                        _canvas?.Invalidate();
                    });
                }
            }
            catch { }
        }

        private bool DrawGridLines(SKCanvas canvas, float width, float scale, DayTimelineViewModel vm)
        {
            // Take a snapshot to avoid race conditions
            var gridLines = vm.GridLines;
            if (gridLines == null || gridLines.Count == 0) return false;
            
            using var linePaint = new SKPaint { Style = SKPaintStyle.Stroke, StrokeWidth = 1, IsAntialias = true };
            using var textPaint = new SKPaint { Style = SKPaintStyle.Fill, IsAntialias = true, Typeface = SKTypeface.FromFamilyName("Segoe UI") };

            foreach (var line in gridLines.ToList())
            {
                float y = (float)(line.Top * scale);
                linePaint.Color = new SKColor(128, 128, 128, (byte)Math.Max(0, Math.Min(255, line.Opacity * 255)));
                canvas.DrawLine(0, y, width - 50 * scale, y, linePaint);
                
                if (!string.IsNullOrEmpty(line.TimeText))
                {
                    textPaint.Color = new SKColor(128, 128, 128, 128);
                    textPaint.TextSize = Math.Max(8, (float)(line.FontSize * scale));
                    float textWidth = textPaint.MeasureText(line.TimeText);
                    canvas.DrawText(line.TimeText, width - 45 * scale - textWidth, y - 2 * scale, textPaint);
                }
            }
            return true;
        }

        private bool DrawSessionBlocks(SKCanvas canvas, float width, float scale, DayTimelineViewModel vm)
        {
            // Take a snapshot to avoid race conditions
            var sessionBlocks = vm.SessionBlocks;
            if (sessionBlocks == null || sessionBlocks.Count == 0) return false;
            
            // Main blocks 80%, audio 18%
            float mainBlockWidth = (width - 50 * scale) * 0.80f;
            float audioLeft = mainBlockWidth + 6 * scale;
            float audioWidth = (width - 50 * scale) * 0.18f;
            
            using var fillPaint = new SKPaint { Style = SKPaintStyle.Fill, IsAntialias = true };
            using var textPaint = new SKPaint { Style = SKPaintStyle.Fill, IsAntialias = true, Typeface = SKTypeface.FromFamilyName("Segoe UI") };
            using var strokePaint = new SKPaint { Style = SKPaintStyle.Stroke, StrokeWidth = 1, IsAntialias = true };

            // Label collision detection
            var drawnLabels = new System.Collections.Generic.List<SKRect>();

            foreach (var block in sessionBlocks.ToList())
            {
                float top = (float)(block.Top * scale);
                float height = (float)(block.Height * scale);
                if (height < 1) continue;
                
                SKColor blockColor = SKColors.Gray;
                if (block.BackgroundColor is Microsoft.UI.Xaml.Media.SolidColorBrush brush)
                    blockColor = new SKColor(brush.Color.R, brush.Color.G, brush.Color.B, 255);

                // Indicator bar - always draw
                fillPaint.Color = blockColor;
                canvas.DrawRoundRect(new SKRect(0, top, 4 * scale, top + height), 2 * scale, 2 * scale, fillPaint);

                // Main block - always draw
                fillPaint.Color = new SKColor(blockColor.Red, blockColor.Green, blockColor.Blue, 50);
                canvas.DrawRoundRect(new SKRect(4 * scale, top, mainBlockWidth, top + height), 4 * scale, 4 * scale, fillPaint);

                if (block.IsAfk)
                {
                    fillPaint.Color = new SKColor(255, 185, 0, 80);
                    canvas.DrawRoundRect(new SKRect(4 * scale, top, mainBlockWidth, top + height), 4 * scale, 4 * scale, fillPaint);
                }

                // Main text - show if block is at least 16 pixels tall (decluttering threshold)
                if (height > 16)
                {
                    textPaint.TextSize = Math.Max(9, Math.Min(12, height * 0.6f));
                    var displayName = UserPreferences.GetDisplayName(block.ProcessName);
                    string title = string.IsNullOrEmpty(displayName) ? block.Title : displayName;
                    textPaint.Color = blockColor;
                    
                    float textX = 12 * scale;
                    float textY = top + height * 0.65f;
                    string clippedTitle = ClipText(title, textPaint, mainBlockWidth - 20 * scale);
                    float textWidth = textPaint.MeasureText(clippedTitle);
                    var labelRect = new SKRect(textX, textY - textPaint.TextSize, textX + textWidth, textY);
                    
                    // Check for collision
                    bool collides = drawnLabels.Any(r => r.IntersectsWith(labelRect));
                    if (!collides)
                    {
                        canvas.DrawText(clippedTitle, textX, textY, textPaint);
                        drawnLabels.Add(labelRect);
                    }

                    // Duration text - show if at least 28 pixels tall
                    if (height > 28)
                    {
                        textPaint.TextSize = Math.Max(8, Math.Min(10, height * 0.35f));
                        textPaint.Color = new SKColor(128, 128, 128, 180);
                        float durY = top + height * 0.9f;
                        string durText = block.DurationText ?? "";
                        float durWidth = textPaint.MeasureText(durText);
                        var durRect = new SKRect(textX, durY - textPaint.TextSize, textX + durWidth, durY);
                        
                        bool durCollides = drawnLabels.Any(r => r.IntersectsWith(durRect));
                        if (!durCollides)
                        {
                            canvas.DrawText(durText, textX, durY, textPaint);
                            drawnLabels.Add(durRect);
                        }
                    }
                }

                // Audio block - show if block is at least 12 pixels tall (decluttering threshold)
                if (block.HasAudio && height > 12)
                {
                    var audioRect = new SKRect(audioLeft, top, audioLeft + audioWidth, top + height);
                    fillPaint.Color = new SKColor(50, 50, 50, 220);
                    canvas.DrawRoundRect(audioRect, 4 * scale, 4 * scale, fillPaint);
                    strokePaint.Color = new SKColor(100, 100, 100, 150);
                    canvas.DrawRoundRect(audioRect, 4 * scale, 4 * scale, strokePaint);
                    
                    // Audio icon - show if at least 16 pixels
                    if (height > 16)
                    {
                        textPaint.TextSize = Math.Max(8, Math.Min(11, height * 0.5f));
                        textPaint.Color = new SKColor(200, 200, 200, 255);
                        canvas.DrawText("🔊", audioLeft + 3 * scale, top + height * 0.6f, textPaint);
                    }
                    
                    // Audio source text - show if at least 24 pixels
                    if (height > 24)
                    {
                        textPaint.TextSize = Math.Max(7, Math.Min(10, height * 0.35f));
                        textPaint.Color = new SKColor(230, 230, 230, 255);
                        canvas.DrawText(ClipText(block.AudioSourcesText, textPaint, audioWidth - 6 * scale), audioLeft + 3 * scale, top + height * 0.85f, textPaint);
                    }
                }
            }
            return true;
        }


        private string ClipText(string text, SKPaint paint, float maxWidth)
        {
            if (string.IsNullOrEmpty(text) || maxWidth <= 0) return "";
            if (paint.MeasureText(text) <= maxWidth) return text;
            int len = text.Length;
            while (len > 0 && paint.MeasureText(text.Substring(0, len) + "...") > maxWidth) len--;
            return len > 0 ? text.Substring(0, len) + "..." : "";
        }

        private void DrawCurrentTimeIndicator(SKCanvas canvas, float width, float scale, DayTimelineViewModel vm)
        {
            float y = (float)(vm.CurrentTimeTop * scale);
            using var paint = new SKPaint { Style = SKPaintStyle.Fill, Color = new SKColor(255, 82, 82), IsAntialias = true };
            canvas.DrawRect(0, y - 1 * scale, width - 50 * scale, 2 * scale, paint);
            canvas.DrawCircle(0, y, 4 * scale, paint);
        }

        private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
        {
            try
            {
                var vm = ViewModel;
                if (vm?.SessionBlocks == null) return;
                
                var pos = e.GetCurrentPoint(_canvas).Position;
                float mainBlockWidth = (float)((_canvas.ActualWidth - 50) * 0.80);
                float audioLeft = mainBlockWidth + 6;
                float audioWidth = (float)((_canvas.ActualWidth - 50) * 0.18);

                // Collect ALL blocks that intersect the mouse position
                var mainCandidates = vm.SessionBlocks
                    .Where(b => pos.Y >= b.Top && pos.Y <= b.Top + b.Height && pos.X >= 0 && pos.X <= mainBlockWidth)
                    .ToList();
                
                var audioCandidates = vm.SessionBlocks
                    .Where(b => b.HasAudio && pos.Y >= b.Top && pos.Y <= b.Top + b.Height && pos.X >= audioLeft && pos.X <= audioLeft + audioWidth)
                    .ToList();

                // Prioritize main blocks over audio
                if (mainCandidates.Any())
                {
                    // Smart selection: prefer non-AFK, then smallest (most specific) block
                    var best = mainCandidates
                        .OrderBy(b => b.IsAfk ? 1 : 0) // Non-AFK first
                        .ThenBy(b => b.Height)         // Smallest first (more specific)
                        .First();

                    string durationStr = FormatDuration(best.OriginalSession?.Duration ?? TimeSpan.Zero);
                    _tooltipText.Text = $"{best.Title}\n{durationStr}{(best.IsAfk ? " [AFK]" : "")}";
                    _tooltipBorder.Visibility = Visibility.Visible;
                    
                    double left = Math.Min(pos.X + 15, _canvas.ActualWidth - 180);
                    double top = pos.Y - 10;
                    Canvas.SetLeft(_tooltipBorder, left);
                    Canvas.SetTop(_tooltipBorder, top);
                    return;
                }
                
                if (audioCandidates.Any())
                {
                    var best = audioCandidates.OrderBy(b => b.Height).First();
                    string durationStr = FormatDuration(best.OriginalSession?.Duration ?? TimeSpan.Zero);
                    _tooltipText.Text = $"🔊 Audio: {best.AudioSourcesText}\n{durationStr}";
                    _tooltipBorder.Visibility = Visibility.Visible;
                    
                    double left = Math.Min(pos.X + 15, _canvas.ActualWidth - 180);
                    double top = pos.Y - 10;
                    Canvas.SetLeft(_tooltipBorder, left);
                    Canvas.SetTop(_tooltipBorder, top);
                    return;
                }

                _tooltipBorder.Visibility = Visibility.Collapsed;
            }
            catch { }
        }

        
        private string FormatDuration(TimeSpan duration)
        {
            if (duration.TotalHours >= 1)
                return $"{(int)duration.TotalHours}h {duration.Minutes}m";
            else if (duration.TotalMinutes >= 1)
                return $"{(int)duration.TotalMinutes}m {duration.Seconds}s";
            else
                return $"{duration.Seconds}s";
        }

        private void OnPointerExited(object sender, PointerRoutedEventArgs e)
        {
            _tooltipBorder.Visibility = Visibility.Collapsed;
        }
    }
}
