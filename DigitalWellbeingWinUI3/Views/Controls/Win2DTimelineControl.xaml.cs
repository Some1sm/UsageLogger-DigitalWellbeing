using Microsoft.Graphics.Canvas.Text;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Windows.UI;
using DigitalWellbeingWinUI3.Models;
using DigitalWellbeingWinUI3.ViewModels;
using DigitalWellbeingWinUI3.Helpers;

namespace DigitalWellbeingWinUI3.Views.Controls
{
    public sealed partial class Win2DTimelineControl : UserControl
    {
        private DayTimelineViewModel _subscribedViewModel;
        private float _scaleX = 1.0f;
        private float _scaleY = 1.0f;

        public Win2DTimelineControl()
        {
            this.InitializeComponent();
            this.DataContextChanged += OnDataContextChanged;
            this.Unloaded += OnUnloaded;
        }

        /*
        public static readonly DependencyProperty VerticalOffsetProperty =
            DependencyProperty.Register(nameof(VerticalOffset), typeof(double), typeof(Win2DTimelineControl), new PropertyMetadata(0.0, OnPropertyChanged));

        public double VerticalOffset
        {
            get => (double)GetValue(VerticalOffsetProperty);
            set => SetValue(VerticalOffsetProperty, value);
        }
        */

        private static void OnPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is Win2DTimelineControl ctrl)
            {
                ctrl.TimelineCanvas.Invalidate();
            }
        }

        public DayTimelineViewModel ViewModel => this.DataContext as DayTimelineViewModel;

        private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
        {
            if (_subscribedViewModel != null)
            {
                _subscribedViewModel.PropertyChanged -= ViewModel_PropertyChanged;
            }

            if (args.NewValue is DayTimelineViewModel vm)
            {
                _subscribedViewModel = vm;
                vm.PropertyChanged += ViewModel_PropertyChanged;
                
                // Init header
                HeaderText.Text = vm.DateString;
                
                // Force initial redraw
                TimelineCanvas.Invalidate();
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            if (_subscribedViewModel != null)
            {
                _subscribedViewModel.PropertyChanged -= ViewModel_PropertyChanged;
                _subscribedViewModel = null;
            }
            TimelineCanvas.RemoveFromVisualTree();
            TimelineCanvas = null;
        }

        private void ViewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            // Safety check - control might be unloaded
            if (TimelineCanvas == null || _subscribedViewModel == null) return;
            
            if (e.PropertyName == nameof(DayTimelineViewModel.SessionBlocks) ||
                e.PropertyName == nameof(DayTimelineViewModel.GridLines) ||
                e.PropertyName == nameof(DayTimelineViewModel.CanvasHeight) ||
                e.PropertyName == nameof(DayTimelineViewModel.CurrentTimeTop) ||
                e.PropertyName == nameof(DayTimelineViewModel.CurrentTimeVisibility) ||
                e.PropertyName == nameof(DayTimelineViewModel.TimelineWidth))
            {
                if (e.PropertyName == nameof(DayTimelineViewModel.CanvasHeight))
                {
                    double h = Math.Max(100, _subscribedViewModel.CanvasHeight);
                    // CanvasVirtualControl supports huge heights, so we remove the arbitrary 50000 limit.
                    // However, we MUST set the Height of the control itself.
                    TimelineCanvas.Height = h;
                    ContentGrid.Height = h;
                    ContentGrid.MinHeight = h;
                }
                if (e.PropertyName == nameof(DayTimelineViewModel.TimelineWidth))
                {
                    double width = _subscribedViewModel.TimelineWidth > 0 ? _subscribedViewModel.TimelineWidth - 30 : 400;
                    TimelineCanvas.Width = width;
                    ContentGrid.Width = width;
                }
                TimelineCanvas.Invalidate();
            }
            
            // Header
            if (e.PropertyName == nameof(DayTimelineViewModel.DateString))
            {
                HeaderText.Text = _subscribedViewModel.DateString;
            }
        }

        private void TimelineCanvas_RegionsInvalidated(Microsoft.Graphics.Canvas.UI.Xaml.CanvasVirtualControl sender, Microsoft.Graphics.Canvas.UI.Xaml.CanvasRegionsInvalidatedEventArgs args)
        {
            var vm = ViewModel;
            if (vm == null) return;
            
            float width = (float)sender.ActualWidth;
            float height = (float)sender.ActualHeight;
            
            if (width <= 0 || height <= 0) return;

            // Fallback width logic handled in resizing, but ensure scale uses it
            float renderWidth = vm.TimelineWidth > 0 ? (float)(vm.TimelineWidth - 30) : 400f;
            if (width <= 0) width = renderWidth;
             
            _scaleX = 1.0f;
            _scaleY = 1.0f;
            
            // Loop through invalidated regions
            foreach (var region in args.InvalidatedRegions)
            {
                using (var ds = sender.CreateDrawingSession(region))
                {
                    ds.Clear(Colors.Transparent);
                    
                    // Optimization: Only draw things inside the region? 
                    // For now, simpler to just draw everything and let Win2D clip, 
                    // or implement basic clipping if performance issues arise.
                    // Given the simple vector graphics, drawing all shouldn't be too expensive 
                    // unless there are thousands of blocks.
                    
                    // However, for strict correctness with transforms (if we had them), passing region helps.
                    // Here we just draw to the session.
                    
                    DrawGridLines(ds, width, vm);
                    DrawSessionBlocks(ds, width, vm);
                    
                    if (vm.CurrentTimeVisibility == Visibility.Visible)
                    {
                        float y = (float)vm.CurrentTimeTop;
                        ds.DrawLine(0, y, width, y, Colors.Red, 2);
                    }
                }
            }
        }

        private void DrawGridLines(Microsoft.Graphics.Canvas.CanvasDrawingSession ds, float width, DayTimelineViewModel vm)
        {
            var gridLines = vm.GridLines;
            if (gridLines == null) return;

            // Create text format
            using var textFormat = new CanvasTextFormat
            {
                FontSize = 10,
                FontFamily = "Segoe UI",
                HorizontalAlignment = CanvasHorizontalAlignment.Right,
                VerticalAlignment = CanvasVerticalAlignment.Center
            };

            foreach (var line in gridLines)
            {
                float y = (float)line.Top;
                Color lineColor = Color.FromArgb((byte)(Math.Max(0, Math.Min(255, line.Opacity * 255))), 128, 128, 128);
                
                ds.DrawLine(0, y, width - 50, y, lineColor, 1);
                
                if (!string.IsNullOrEmpty(line.TimeText))
                {
                    textFormat.FontSize = (float)Math.Max(8, line.FontSize);
                    ds.DrawText(line.TimeText, width - 45 - 2, y, Colors.Gray, textFormat);
                }
            }
        }

        private void DrawSessionBlocks(Microsoft.Graphics.Canvas.CanvasDrawingSession ds, float width, DayTimelineViewModel vm)
        {
            var sessionBlocks = vm.SessionBlocks;
            if (sessionBlocks == null) return;

            float mainBlockWidth = (width - 50) * 0.80f;
            
            // Text formats
            using var titleFormat = new CanvasTextFormat { FontSize = 12, FontFamily = "Segoe UI" };
            using var durFormat = new CanvasTextFormat { FontSize = 10, FontFamily = "Segoe UI", FontStyle = Windows.UI.Text.FontStyle.Italic };
            
            var drawnLabels = new List<Windows.Foundation.Rect>();

            foreach (var block in sessionBlocks)
            {
                float top = (float)block.Top;
                float height = (float)block.Height;
                if (height < 1) continue;
                
                Color blockColor = Colors.Gray;
                if (block.BackgroundColor is SolidColorBrush brush)
                {
                   blockColor = brush.Color;
                }

                // Indicator
                ds.FillRoundedRectangle((float)0, top, 4f, height, 2f, 2f, blockColor);
                
                // Main Block Background
                Color bg = Color.FromArgb(50, blockColor.R, blockColor.G, blockColor.B);
                ds.FillRoundedRectangle(4f, top, mainBlockWidth, height, 4f, 4f, bg);

                if (block.IsAfk)
                {
                    Color afkColor = Color.FromArgb(80, 255, 185, 0);
                    ds.FillRoundedRectangle(4f, top, mainBlockWidth, height, 4f, 4f, afkColor);
                }
                
                // Text
                if (height > 16)
                {
                     float mainFontSize = Math.Max(9, Math.Min(12, height * 0.6f));
                     titleFormat.FontSize = mainFontSize;
                     
                     var displayName = UserPreferences.GetDisplayName(block.ProcessName);
                     string title = string.IsNullOrEmpty(displayName) ? block.Title : displayName;
                     
                     using var layout = new CanvasTextLayout(ds, title, titleFormat, mainBlockWidth, height);
                     float textX = 8.0f;
                     
                     // Sticky Header Logic
                     float labelHeight = (float)layout.LayoutBounds.Height; // Approx
                     float textY = top + 2; // Default top
                     
                     /*
                     // If top is scrolled off screen (Top < Offset) but Bottom is still visible (Bottom > Offset)
                     if (top < VerticalOffset && (top + height) > VerticalOffset + labelHeight)
                     {
                         textY = (float)VerticalOffset + 2;
                         // Clamp to bottom of block
                         if (textY + labelHeight > top + height)
                         {
                             textY = top + height - labelHeight - 2;
                         }
                     }
                     else
                     {
                         // Centering logic if small block? Or just top align?
                         // Original: top + height*0.1f. 
                         textY = top + 2; // Keep it simple top aligned
                     }
                     */
                     textY = top + 2;
                     
                     ds.DrawTextLayout(layout, textX, textY, blockColor);
                }
                
                // Duration
                if (height > 28)
                {
                     // Simple duration text drawing below title
                     // I'll skip complex collision for speed, or implement if needed.
                     // Skia implementation had complex collision.
                }
            }
        }

        private void TimelineCanvas_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
             var pt = e.GetCurrentPoint(TimelineCanvas).Position;
             var vm = ViewModel;
             if (vm?.SessionBlocks == null) return;
             
             // Find block under mouse
             // Simplified hit test
             // Blocks are at X: 4 to 4+mainBlockWidth
             // Y: block.Top to block.Top + Height
             
             // In Win2DBarChart we used a tooltip.
             var block = vm.SessionBlocks.FirstOrDefault(b => 
                 pt.Y >= b.Top && pt.Y <= b.Top + b.Height);
                 
             if (block != null)
             {
                 TooltipText.Text = block.TooltipText;
                 TooltipBorder.Visibility = Visibility.Visible;
                 
                 // Position tooltip
                 Canvas.SetLeft(TooltipBorder, pt.X + 10);
                 Canvas.SetTop(TooltipBorder, pt.Y + 10);
             }
             else
             {
                 TooltipBorder.Visibility = Visibility.Collapsed;
             }
        }

        private void TimelineCanvas_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            TooltipBorder.Visibility = Visibility.Collapsed;
        }
    }
}
