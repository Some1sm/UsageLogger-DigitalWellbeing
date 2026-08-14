using System;
using Microsoft.UI.Xaml.Data;

namespace UsageLogger.Converters
{
    public class EnumToIntConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is Enum e)
            {
                return System.Convert.ToInt32(e);
            }
            return 0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            if (value is int intVal && targetType.IsEnum)
            {
                return Enum.ToObject(targetType, intVal);
            }
            return value;
        }
    }
}
