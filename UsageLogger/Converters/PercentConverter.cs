using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using System;

namespace UsageLogger.Converters
{
    public class PercentConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            double d = 0;
            if (value is double db) d = db;
            else if (value is int i) d = (double)i;

            if (targetType == typeof(GridLength))
            {
                return new GridLength(Math.Max(0.001, d), GridUnitType.Star);
            }

            return $"{d:0.0}%";
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
