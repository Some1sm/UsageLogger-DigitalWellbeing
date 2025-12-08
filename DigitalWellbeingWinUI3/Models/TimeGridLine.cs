using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DigitalWellbeingWinUI3.Models
{
    public class TimeGridLine : INotifyPropertyChanged
    {
        public string TimeText { get; set; }
        public double Height { get; set; }
        public double Top { get; set; }
        public double Opacity { get; set; }
        public double FontSize { get; set; }

        private double _width;
        public double Width
        {
            get => _width;
            set { if (_width != value) { _width = value; OnPropertyChanged(); } }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
