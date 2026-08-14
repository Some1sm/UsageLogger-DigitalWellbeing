using Microsoft.UI.Xaml.Data;
using System;

namespace UsageLogger.Converters
{
    public class PercentConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is double d)
            {
                return $"{d:0.0}%";
            }
            if (value is int i)
            {
                return $"{i}%";
            }
            return "0%";
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
