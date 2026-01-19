using UsageLogger.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace UsageLogger.Helpers
{
    /// <summary>
    /// Implements a Squarified Treemap layout algorithm.
    /// Based on Bruls, Huizing, and van Wijk (2000) - produces rectangles with better aspect ratios.
    /// </summary>
    public static class TreemapLayout
    {
        /// <summary>
        /// Calculates the layout for all items to fit within the given bounds.
        /// </summary>
        public static void Calculate(IList<TreemapItem> items, double width, double height)
        {
            if (items == null || items.Count == 0 || width <= 0 || height <= 0)
                return;

            double totalValue = items.Sum(i => i.Value);
            if (totalValue <= 0) return;

            // Sort items by value descending (required for squarify)
            var sortedItems = items.OrderByDescending(i => i.Value).ToList();

            // Squarify layout
            Squarify(sortedItems, new List<TreemapItem>(), new Rect(0, 0, width, height), totalValue);
        }

        private struct Rect
        {
            public double X, Y, Width, Height;
            public Rect(double x, double y, double w, double h) { X = x; Y = y; Width = w; Height = h; }
            public double ShortSide => Math.Min(Width, Height);
            public double Area => Width * Height;
        }

        private static void Squarify(List<TreemapItem> items, List<TreemapItem> row, Rect bounds, double totalValue)
        {
            if (items.Count == 0)
            {
                LayoutRow(row, bounds, totalValue);
                return;
            }

            if (bounds.Width <= 0 || bounds.Height <= 0) return;

            var item = items[0];
            var newRow = new List<TreemapItem>(row) { item };

            // Check if adding this item improves or maintains aspect ratio
            if (row.Count == 0 || WorstAspectRatio(newRow, bounds, totalValue) <= WorstAspectRatio(row, bounds, totalValue))
            {
                // Adding item improves (or doesn't worsen) aspect ratio - continue building row
                Squarify(items.Skip(1).ToList(), newRow, bounds, totalValue);
            }
            else
            {
                // Adding item worsens aspect ratio - layout current row and start fresh
                Rect remaining = LayoutRow(row, bounds, totalValue);
                double remainingValue = items.Sum(i => i.Value);
                Squarify(items, new List<TreemapItem>(), remaining, remainingValue);
            }
        }

        /// <summary>
        /// Calculates the worst (highest) aspect ratio among rectangles in a row.
        /// </summary>
        private static double WorstAspectRatio(List<TreemapItem> row, Rect bounds, double totalValue)
        {
            if (row.Count == 0) return double.MaxValue;

            double rowValue = row.Sum(i => i.Value);
            double rowArea = bounds.Area * (rowValue / totalValue);
            double side = bounds.ShortSide;
            
            if (side <= 0 || rowArea <= 0) return double.MaxValue;

            // Width of the row strip (along the short side)
            double rowWidth = rowArea / side;
            if (rowWidth <= 0) return double.MaxValue;

            double worst = 0;
            foreach (var item in row)
            {
                double itemArea = bounds.Area * (item.Value / totalValue);
                double itemHeight = itemArea / rowWidth;
                if (itemHeight <= 0) continue;

                double aspect = Math.Max(rowWidth / itemHeight, itemHeight / rowWidth);
                worst = Math.Max(worst, aspect);
            }

            return worst == 0 ? double.MaxValue : worst;
        }

        /// <summary>
        /// Lays out a row of items along the short side of the bounds.
        /// Returns the remaining rectangle.
        /// </summary>
        private static Rect LayoutRow(List<TreemapItem> row, Rect bounds, double totalValue)
        {
            if (row.Count == 0) return bounds;

            double rowValue = row.Sum(i => i.Value);
            double rowRatio = rowValue / totalValue;
            double rowArea = bounds.Area * rowRatio;

            bool horizontal = bounds.Width >= bounds.Height;
            double side = bounds.ShortSide;
            double rowWidth = (side > 0) ? rowArea / side : 0;

            double offset = 0;
            foreach (var item in row)
            {
                double itemRatio = item.Value / rowValue;
                double itemLength = side * itemRatio;

                if (horizontal)
                {
                    // Row is laid out on the left, items stacked vertically
                    item.X = bounds.X;
                    item.Y = bounds.Y + offset;
                    item.Width = rowWidth;
                    item.Height = itemLength;
                }
                else
                {
                    // Row is laid out on top, items stacked horizontally
                    item.X = bounds.X + offset;
                    item.Y = bounds.Y;
                    item.Width = itemLength;
                    item.Height = rowWidth;
                }

                offset += itemLength;
            }

            // Return remaining area
            if (horizontal)
            {
                return new Rect(bounds.X + rowWidth, bounds.Y, bounds.Width - rowWidth, bounds.Height);
            }
            else
            {
                return new Rect(bounds.X, bounds.Y + rowWidth, bounds.Width, bounds.Height - rowWidth);
            }
        }
    }
}
