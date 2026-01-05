using Microsoft.UI.Xaml.Data;
using System;

namespace DigitalWellbeingWinUI3.Converters
{
    public sealed partial class PercentConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is double d)
            {
                return $"{d:0.0}%";
            }
            return "0%";
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
