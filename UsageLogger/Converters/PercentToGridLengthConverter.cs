using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using System;

namespace UsageLogger.Converters
{
    public class PercentToGridLengthConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            double d = 0;
            if (value is double db) d = db;
            else if (value is int i) d = (double)i;
            else if (value is float f) d = (double)f;
            else if (value is decimal dec) d = (double)dec;

            // If d <= 0, return a tiny positive Star fraction so GridColumn still exists but collapses gracefully
            return new GridLength(Math.Max(0.0001, d), GridUnitType.Star);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
