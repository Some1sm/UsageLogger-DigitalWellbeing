using DigitalWellbeingWinUI3.Helpers;
using DigitalWellbeingWinUI3.Models;
using DigitalWellbeing.Core.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System.Collections.Generic;
using System.Linq;

namespace DigitalWellbeingWinUI3.Controls
{
    public sealed partial class CustomTreemap : UserControl
    {
        // Store item rects for hit testing
        private List<(TreemapItem Item, double X, double Y, double Width, double Height)> _itemRects = new();

        public static readonly DependencyProperty ItemsSourceProperty =
            DependencyProperty.Register(
                nameof(ItemsSource),
                typeof(IList<TreemapItem>),
                typeof(CustomTreemap),
                new PropertyMetadata(null, OnItemsSourceChanged));

        public IList<TreemapItem> ItemsSource
        {
            get => (IList<TreemapItem>)GetValue(ItemsSourceProperty);
            set => SetValue(ItemsSourceProperty, value);
        }

        public CustomTreemap()
        {
            this.InitializeComponent();
            this.SizeChanged += OnSizeChanged;
        }

        private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is CustomTreemap treemap)
            {
                treemap.Render();
            }
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            // Resize inner canvas to match control size
            TreemapCanvas.Width = e.NewSize.Width;
            TreemapCanvas.Height = e.NewSize.Height;
            RootCanvas.Width = e.NewSize.Width;
            RootCanvas.Height = e.NewSize.Height;
            Render();
        }

        private void Render()
        {
            TreemapCanvas.Children.Clear();
            _itemRects.Clear();

            var items = ItemsSource;
            if (items == null || items.Count == 0) return;

            // Use the UserControl's dimensions
            double width = this.ActualWidth;
            double height = this.ActualHeight;

            if (width <= 0 || height <= 0) return;

            // Calculate layout
            TreemapLayout.Calculate(items, width, height);

            // Render items
            foreach (var item in items)
            {
                if (item.Width < 2 || item.Height < 2) continue; // Skip tiny items

                var border = new Border
                {
                    Width = item.Width - 2, // 2px gap
                    Height = item.Height - 2,
                    Background = item.Fill ?? new SolidColorBrush(Microsoft.UI.Colors.Gray),
                    CornerRadius = new CornerRadius(4),
                    BorderThickness = new Thickness(0),
                    Padding = new Thickness(6),
                    Tag = item
                };

                // Calculate luminance of background color for text contrast
                var textBrush = GetContrastBrush(item.Fill);

                // Content: Name and value
                var stack = new StackPanel
                {
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    IsHitTestVisible = false
                };

                // Only show text if box is big enough
                if (item.Width > 50 && item.Height > 30)
                {
                    var nameBlock = new TextBlock
                    {
                        Text = TruncateName(item.Name, (int)(item.Width / 8)),
                        FontSize = 12,
                        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                        Foreground = textBrush,
                        TextTrimming = TextTrimming.CharacterEllipsis,
                        MaxLines = 2,
                        TextWrapping = TextWrapping.Wrap,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        TextAlignment = Microsoft.UI.Xaml.TextAlignment.Center,
                        IsHitTestVisible = false
                    };
                    stack.Children.Add(nameBlock);

                    if (item.Height > 50)
                    {
                        var valueBlock = new TextBlock
                        {
                            Text = item.FormattedValue ?? "",
                            FontSize = 10,
                            Foreground = textBrush,
                            Opacity = 0.9,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            IsHitTestVisible = false
                        };
                        stack.Children.Add(valueBlock);
                    }
                }

                border.Child = stack;

                // Store rect for custom hit testing
                _itemRects.Add((item, item.X + 1, item.Y + 1, item.Width - 2, item.Height - 2));

                // Position on canvas
                Canvas.SetLeft(border, item.X + 1);
                Canvas.SetTop(border, item.Y + 1);

                TreemapCanvas.Children.Add(border);
            }
        }

        private void TreemapCanvas_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            var pt = e.GetCurrentPoint(TreemapCanvas).Position;
            
            // Find which item is under the cursor
            TreemapItem hoveredItem = null;
            foreach (var rect in _itemRects)
            {
                if (pt.X >= rect.X && pt.X <= rect.X + rect.Width &&
                    pt.Y >= rect.Y && pt.Y <= rect.Y + rect.Height)
                {
                    hoveredItem = rect.Item;
                    break;
                }
            }

            if (hoveredItem != null)
            {
                // Update tooltip text
                TooltipText.Text = $"{hoveredItem.Name}\n{hoveredItem.FormattedValue} ({hoveredItem.Percentage:F1}%)";
                TooltipBorder.Visibility = Visibility.Visible;

                // Measure tooltip
                TooltipBorder.Measure(new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));
                double tipWidth = TooltipBorder.DesiredSize.Width;
                double tipHeight = TooltipBorder.DesiredSize.Height;

                // Position near cursor
                double tipX = pt.X + 15;
                double tipY = pt.Y + 15;

                // Clamp to bounds
                double maxWidth = this.ActualWidth;
                double maxHeight = this.ActualHeight;
                
                if (tipX + tipWidth > maxWidth) tipX = pt.X - tipWidth - 5;
                if (tipY + tipHeight > maxHeight) tipY = pt.Y - tipHeight - 5;
                if (tipX < 0) tipX = 5;
                if (tipY < 0) tipY = 5;

                Canvas.SetLeft(TooltipBorder, tipX);
                Canvas.SetTop(TooltipBorder, tipY);
            }
            else
            {
                TooltipBorder.Visibility = Visibility.Collapsed;
            }
        }

        private void TreemapCanvas_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            TooltipBorder.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// Returns a contrast brush (Black or White) based on background luminance.
        /// </summary>
        private SolidColorBrush GetContrastBrush(Brush background)
        {
            if (background is SolidColorBrush solidBrush)
            {
                var color = solidBrush.Color;
                double luminance = (0.299 * color.R + 0.587 * color.G + 0.114 * color.B) / 255.0;
                return luminance > 0.5 
                    ? new SolidColorBrush(Microsoft.UI.Colors.Black) 
                    : new SolidColorBrush(Microsoft.UI.Colors.White);
            }
            return new SolidColorBrush(Microsoft.UI.Colors.White);
        }

        private string TruncateName(string name, int maxChars)
        {
            if (string.IsNullOrEmpty(name)) return name;
            if (maxChars < 5) maxChars = 5;
            if (name.Length <= maxChars) return name;
            return name.Substring(0, maxChars - 2) + "..";
        }
    }
}
