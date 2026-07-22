using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace ModTogetherMHW.Models
{
    public class ModTreeNode : INotifyPropertyChanged
    {
        public string Name { get; set; } = string.Empty;
        public string EntryKey { get; set; } = string.Empty;
        public bool IsDirectory { get; set; }
        
        public ModTreeNode? Parent { get; set; }
        
        private bool? _isChecked = true;
        public bool? IsChecked
        {
            get => _isChecked;
            set
            {
                if (_isChecked != value)
                {
                    _isChecked = value;
                    OnPropertyChanged();
                    UpdateChildrenCheckState();
                    Parent?.UpdateParentCheckState();
                }
            }
        }
        
        private bool _isExpanded = true;
        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (_isExpanded != value)
                {
                    _isExpanded = value;
                    OnPropertyChanged();
                }
            }
        }

        public ObservableCollection<ModTreeNode> Children { get; set; } = new ObservableCollection<ModTreeNode>();

        private void UpdateChildrenCheckState()
        {
            if (_isChecked == null) return;

            foreach (var child in Children)
            {
                var oldParent = child.Parent;
                child.Parent = null;
                
                child.IsChecked = _isChecked;
                
                child.Parent = oldParent;
                child.UpdateChildrenCheckState();
            }
        }

        public void UpdateParentCheckState()
        {
            if (Children.Count == 0) return;

            bool allChecked = Children.All(c => c.IsChecked == true);
            bool allUnchecked = Children.All(c => c.IsChecked == false);

            bool? newState = null;
            if (allChecked) newState = true;
            else if (allUnchecked) newState = false;

            if (_isChecked != newState)
            {
                _isChecked = newState;
                OnPropertyChanged(nameof(IsChecked));
                Parent?.UpdateParentCheckState();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
