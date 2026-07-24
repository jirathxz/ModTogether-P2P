using System;
using System.ComponentModel;
using System.Windows.Media;

namespace ModTogetherUniversal.Models
{
    public class ModItemData : INotifyPropertyChanged
    {
        private bool _isChecked;
        public bool IsChecked
        {
            get => _isChecked;
            set
            {
                if (_isChecked != value)
                {
                    _isChecked = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsChecked)));
                }
            }
        }
        public string Filename { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string DateModified { get; set; } = string.Empty;
        public DateTime DateNum { get; set; }
        public string Size { get; set; } = string.Empty;
        public long SizeNum { get; set; }
        public SolidColorBrush? BackgroundColor { get; set; }
        
        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
