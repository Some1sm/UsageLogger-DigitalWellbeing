using DigitalWellbeingWinUI3.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DigitalWellbeingWinUI3.Views
{
    public sealed partial class SessionsPage : Page
    {
        public SessionsViewModel ViewModel { get; }

        public SessionsPage()
        {
            this.InitializeComponent();
            ViewModel = new SessionsViewModel();
        }

    }

    public class DateFormatConverter : Microsoft.UI.Xaml.Data.IValueConverter
    {
        public object Convert(object value, System.Type targetType, object parameter, string language)
        {
            if (value is System.DateTime dt)
            {
                if (dt.Date == System.DateTime.Now.Date) return "Today";
                if (dt.Date == System.DateTime.Now.AddDays(-1).Date) return "Yesterday";
                if (dt.Date == System.DateTime.Now.AddDays(1).Date) return "Tomorrow";
                return dt.ToString("D"); // Long date pattern
            }
            return value;
        }

        public object ConvertBack(object value, System.Type targetType, object parameter, string language)
        {
            throw new System.NotImplementedException();
        }
    }
}
