using DigitalWellbeingWinUI3.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DigitalWellbeingWinUI3.Helpers
{
    /// <summary>
    /// Implements a Slice-and-Dice Treemap layout algorithm.
    /// This version guarantees full coverage of the available space.
    /// </summary>
    public static class TreemapLayout
    {
        /// <summary>
        /// Calculates the layout for all items to fit within the given bounds.
        /// Uses a recursive slice-and-dice approach that fills the entire space.
        /// </summary>
        public static void Calculate(IList<TreemapItem> items, double width, double height)
        {
            if (items == null || items.Count == 0 || width <= 0 || height <= 0)
                return;

            double totalValue = items.Sum(i => i.Value);
            if (totalValue <= 0) return;

            // Sort items by value descending
            var sortedItems = items.OrderByDescending(i => i.Value).ToList();

            // Layout all items recursively
            LayoutItems(sortedItems, 0, 0, width, height, totalValue, true);
        }

        private static void LayoutItems(List<TreemapItem> items, double x, double y, double width, double height, double totalValue, bool horizontal)
        {
            if (items.Count == 0 || width <= 0 || height <= 0 || totalValue <= 0)
                return;

            if (items.Count == 1)
            {
                // Single item takes full space
                var item = items[0];
                item.X = x;
                item.Y = y;
                item.Width = width;
                item.Height = height;
                return;
            }

            // Find the split point where we get roughly half the total value
            double halfValue = totalValue / 2;
            double runningSum = 0;
            int splitIndex = 0;

            for (int i = 0; i < items.Count; i++)
            {
                runningSum += items[i].Value;
                splitIndex = i;
                if (runningSum >= halfValue) break;
            }

            // Ensure at least one item in each group
            if (splitIndex == items.Count - 1 && items.Count > 1)
                splitIndex = items.Count - 2;

            var firstGroup = items.Take(splitIndex + 1).ToList();
            var secondGroup = items.Skip(splitIndex + 1).ToList();

            double firstValue = firstGroup.Sum(i => i.Value);
            double secondValue = secondGroup.Sum(i => i.Value);
            double ratio = firstValue / totalValue;

            if (horizontal)
            {
                // Split horizontally (left-right)
                double firstWidth = width * ratio;
                double secondWidth = width - firstWidth;

                LayoutItems(firstGroup, x, y, firstWidth, height, firstValue, !horizontal);
                if (secondGroup.Count > 0)
                    LayoutItems(secondGroup, x + firstWidth, y, secondWidth, height, secondValue, !horizontal);
            }
            else
            {
                // Split vertically (top-bottom)
                double firstHeight = height * ratio;
                double secondHeight = height - firstHeight;

                LayoutItems(firstGroup, x, y, width, firstHeight, firstValue, !horizontal);
                if (secondGroup.Count > 0)
                    LayoutItems(secondGroup, x, y + firstHeight, width, secondHeight, secondValue, !horizontal);
            }
        }
    }
}
