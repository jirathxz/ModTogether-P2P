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

        private bool _hasConflict;
        public bool HasConflict
        {
            get => _hasConflict;
            set
            {
                if (_hasConflict != value)
                {
                    _hasConflict = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasConflict)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ConflictVisibility)));
                }
            }
        }

        private string _conflictWarningText = string.Empty;
        public string ConflictWarningText
        {
            get => _conflictWarningText;
            set
            {
                if (_conflictWarningText != value)
                {
                    _conflictWarningText = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ConflictWarningText)));
                }
            }
        }

        private int _priority;
        public int Priority
        {
            get => _priority;
            set
            {
                if (_priority != value)
                {
                    _priority = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Priority)));
                }
            }
        }

        public System.Windows.Visibility ConflictVisibility => HasConflict ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;

        private string _ownersBadgeText = string.Empty;
        public string OwnersBadgeText
        {
            get => _ownersBadgeText;
            set
            {
                if (_ownersBadgeText != value)
                {
                    _ownersBadgeText = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(OwnersBadgeText)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(OwnersBadgeVisibility)));
                }
            }
        }

        public System.Windows.Visibility OwnersBadgeVisibility => !string.IsNullOrEmpty(OwnersBadgeText) ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
