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

        public Win2DTimelineControl()
        {
            this.InitializeComponent();
            this.DataContextChanged += OnDataContextChanged;
            this.Unloaded += OnUnloaded;
            this.Loaded += OnLoaded;
        }

        /// <summary>
        /// Public method to force the canvas to invalidate and redraw.
        /// Called by parent page when outer scroll position changes.
        /// </summary>
        public void ForceInvalidate()
        {
            if (TimelineCanvas != null)
            {
                TimelineCanvas.Invalidate();
            }
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
            if (d is Win2DTimelineControl ctrl && ctrl.TimelineCanvas != null)
            {
                double w = ctrl.TimelineCanvas.Width;
                double h = ctrl.TimelineCanvas.Height;
                if (w > 0 && h > 0)
                {
                    ctrl.TimelineCanvas.Invalidate();
                }
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
                
                // CRITICAL FIX: If the control is already loaded but DataContext arrived late,
                // we must trigger dimension setup NOW (otherwise OnLoaded returned early with null VM)
                if (this.IsLoaded)
                {
                    SetupCanvasDimensions(vm);
                }
            }
        }
        
        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // Called AFTER layout is complete - dimensions are now valid
            var vm = ViewModel;
            if (vm == null) return;
            
            SetupCanvasDimensions(vm);
        }

        /// <summary>
        /// Sets up canvas dimensions and triggers invalidation.
        /// Can be called from OnLoaded or OnDataContextChanged (whichever happens last).
        /// </summary>
        private void SetupCanvasDimensions(DayTimelineViewModel vm)
        {
            if (TimelineCanvas == null) return;
            
            // ZOOM LOGIC:
            // 1. The ScrollViewer content (ContentGrid) gets the FULL Height (e.g. 50,000px).
            //    This forces the ScrollViewer to show the correct scrollbar thumb.
            // 2. The CanvasControl (TimelineCanvas) gets the VIEWPORT Height (e.g. 1080px).
            //    This ensures we never exceed GPU texture limits (16384px) even if zoomed in massive amounts.
            
            double h = vm.CanvasHeight > 0 ? vm.CanvasHeight : 1440;
            ContentGrid.Height = h;
            ContentGrid.MinHeight = h;
            
            // Set WIDTH explicitly with correct overhead calculation
            // Overhead: 15px margin (7.5*2) + 2px border (1*2) + 30px padding (15*2) = 47px
            double w = vm.TimelineWidth > 0 ? vm.TimelineWidth - 47 : 400;
            TimelineCanvas.Width = w;
            ContentGrid.Width = w;
            
            // Disable inner horizontal scrollbar to ensure simple vertical-only layout
            if (MainScrollViewer != null) 
            {
                MainScrollViewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
                MainScrollViewer.ViewChanged -= MainScrollViewer_ViewChanged;
                MainScrollViewer.ViewChanged += MainScrollViewer_ViewChanged;
            }

            // Standard invalidation is sufficient now that Width matches container exactly (-47px overhead)
            TimelineCanvas.Invalidate();
        }

        private void MainScrollViewer_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
             // When scrolling vertically, we must invalidate the CanvasControl
             // so it can redraw the new slice of content.
             TimelineCanvas.Invalidate();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            if (_subscribedViewModel != null)
            {
                _subscribedViewModel.PropertyChanged -= ViewModel_PropertyChanged;
                _subscribedViewModel = null;
            }
            if (TimelineCanvas != null)
            {
                TimelineCanvas.RemoveFromVisualTree();
                TimelineCanvas = null;
            }
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
                // Sync HEIGHT on any relevant property change (for zoom)
                double h = Math.Max(100, _subscribedViewModel.CanvasHeight);
                ContentGrid.Height = h;
                ContentGrid.MinHeight = h;
                
                // Set WIDTH explicitly with correct overhead calculation
                // Overhead: 15px margin + 2px border + 30px padding = 47px
                double w = _subscribedViewModel.TimelineWidth > 0 ? _subscribedViewModel.TimelineWidth - 47 : 400;
                TimelineCanvas.Width = w;
                ContentGrid.Width = w;

                // Sync Invalidation
                TimelineCanvas.Invalidate();
            }
            
            // Header
            if (e.PropertyName == nameof(DayTimelineViewModel.DateString))
            {
                HeaderText.Text = _subscribedViewModel.DateString;
            }
        }

        private void TimelineCanvas_Draw(Microsoft.Graphics.Canvas.UI.Xaml.CanvasControl sender, Microsoft.Graphics.Canvas.UI.Xaml.CanvasDrawEventArgs args)
        {
            var vm = ViewModel;
            if (vm == null) return;
            
            float width = (float)sender.ActualWidth;
            float height = (float)sender.ActualHeight;
            
            if (width <= 0 || height <= 0) return;

            // Fallback width logic
            float renderWidth = vm.TimelineWidth > 0 ? (float)(vm.TimelineWidth - 47) : 400f;
            if (width <= 0) width = renderWidth;
            
            // MOVING WINDOW TRANSLATION:
            // Calculate the vertical scroll offset from the wrapper ScrollViewer
            float scrollOffset = (float)(MainScrollViewer?.VerticalOffset ?? 0);
            
            // Apply translation to "shift" drawing up
            using (var ds = args.DrawingSession)
            {
                ds.Transform = System.Numerics.Matrix3x2.CreateTranslation(0, -scrollOffset);
                
                // Draw EVERYTHING (The drawing session will clip what's outside bounds automatically)
                // Note: Win2D clipping is fast.
                
                DrawCanvasContent(ds, width, vm);
            }
        }

        private void DrawCanvasContent(Microsoft.Graphics.Canvas.CanvasDrawingSession ds, float width, DayTimelineViewModel vm)
        {
            ds.Clear(Colors.Transparent);
            DrawSessionBlocks(ds, width, vm);
            DrawGridLines(ds, width, vm);
            
            if (vm.CurrentTimeVisibility == Visibility.Visible)
            {
                float y = (float)vm.CurrentTimeTop;
                ds.DrawLine(0, y, width, y, Colors.Red, 2);
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

            // Pass 1: Draw Main Blocks (Backgrounds + Text)
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
                     
                     // Title selection based on ViewMode
                     string title;
                     string viewMode = vm.ViewMode ?? "SubApp";
                     
                     if (viewMode == "App")
                     {
                         // App mode: show process name or display name
                         var displayName = UserPreferences.GetDisplayName(block.ProcessName);
                         title = !string.IsNullOrEmpty(displayName) ? displayName : (block.ProcessName ?? "Unknown");
                     }
                     else if (viewMode == "Category")
                     {
                         // Category mode: block.Title is already set to the tag name by the ViewModel
                         title = block.Title ?? "Untagged";
                     }
                     else // SubApp (default)
                     {
                         // SubApp mode: prefer specific title over process name
                         var displayName = UserPreferences.GetDisplayName(block.ProcessName);
                         
                         if (!string.IsNullOrEmpty(block.Title) && 
                             !block.Title.Equals(block.ProcessName, StringComparison.OrdinalIgnoreCase))
                         {
                             title = block.Title;
                         }
                         else if (!string.IsNullOrEmpty(displayName))
                         {
                             title = displayName;
                         }
                         else
                         {
                             title = block.Title ?? block.ProcessName ?? "";
                         }
                     }
                     
                     if (!string.IsNullOrEmpty(block.DurationText)) title += $" ({block.DurationText})";
                     
                     using var layout = new CanvasTextLayout(ds, title, titleFormat, mainBlockWidth, height);
                     float textX = 8.0f;
                     
                     // Sticky Header Logic
                     float labelHeight = (float)layout.LayoutBounds.Height; // Approx
                     float textY = top + 2; // Default top
                     
                     textY = top + 2;
                     
                     ds.DrawTextLayout(layout, textX, textY, Colors.White);
                }
            }

            // Pass 2: Draw Background Audio Indicators (Always on top)
            foreach (var block in sessionBlocks)
            {
                float top = (float)block.Top;
                float height = (float)block.Height;
                if (height < 1) continue;

                if (block.HasAudio)
                {
                     // Space on the right: 20% of width
                     float rightSpaceX = 4f + mainBlockWidth + 4f;
                     float rightSpaceW = (width - 50) - rightSpaceX;
                     
                     if (rightSpaceW > 2)
                     {
                         int count = block.AudioSources.Count;
                         // Divide width among sources
                         float sourceWidth = (rightSpaceW - (count - 1) * 2) / count;
                         if (sourceWidth < 2) sourceWidth = 2; // Min width

                         for (int i = 0; i < count; i++)
                         {
                             string audioApp = block.AudioSources[i];
                             
                             // Attempt to get color for audio app
                             // Logic mimics DayTimelineViewModel selection
                             var audioTag = AppTagHelper.GetAppTag(audioApp);
                             var audioBrush = AppTagHelper.GetTagColor(audioTag) as SolidColorBrush;
                             Color baseColor = audioBrush?.Color ?? Colors.Gray;
                             Color audioColor = Color.FromArgb(64, baseColor.R, baseColor.G, baseColor.B);
                             
                             float ax = rightSpaceX + i * (sourceWidth + 2);
                             
                             // Draw
                             ds.FillRoundedRectangle(ax, top, sourceWidth, height, 2, 2, audioColor);
                         }
                     }
                }
            }
        }

        private void TimelineGrid_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
             // Event comes from the SCROLLING ContentGrid.
             // The Y coordinate given is explicitly relative to the ContentGrid's blocked out height.
             // This means it INCLUDES the scroll offset automatically.
             // Example: If scrolled 1000px down and mouse is at top of screen, Y = 1010.
             
             // Therefore, we do NOT need to add VerticalOffset manually anymore.
             
             if (sender is not FrameworkElement fe) return;
             
             var pt = e.GetCurrentPoint(fe).Position;
             double correctedY = pt.Y; // Already "corrected" by being relative to the scrolled container
             
             var vm = ViewModel;
             if (vm?.SessionBlocks == null) return;
             
             // Find block under mouse
             // Use LastOrDefault to find the "top-most" (drawn last) element in case of overlaps
             var block = vm.SessionBlocks.LastOrDefault(b => 
                 correctedY >= b.Top && correctedY <= b.Top + b.Height);
                 
             if (block != null)
             {
                 TooltipText.Text = block.TooltipText;
                 TooltipBorder.Visibility = Visibility.Visible;
                 
                 // Position tooltip
                 // Tooltip is inside a Canvas (Overlay) which is FIXED to the viewport.
                 // But our mouse Point 'pt' is relative to the HUGE scrolled grid (e.g. Y=5000).
                 // We need to convert back to Viewport coordinates for the tooltip to appear on screen.
                 
                 double viewportY = pt.Y - (MainScrollViewer?.VerticalOffset ?? 0);
                 
                 Canvas.SetLeft(TooltipBorder, pt.X + 10);
                 Canvas.SetTop(TooltipBorder, viewportY + 10);
             }
             else
             {
                 TooltipBorder.Visibility = Visibility.Collapsed;
             }
        }

        private void TimelineGrid_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            TooltipBorder.Visibility = Visibility.Collapsed;
        }
    }
}
