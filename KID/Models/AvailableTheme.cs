using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace KID.Models
{
    public class AvailableTheme : INotifyPropertyChanged
    {
        private string _localizedDisplayName = string.Empty;

        public string ThemeKey { get; set; } = string.Empty; // "Light" или "Dark"
        public string EnglishName { get; set; } = string.Empty; // "Light" или "Dark"

        public string LocalizedDisplayName
        {
            get => _localizedDisplayName;
            set
            {
                if (_localizedDisplayName != value)
                {
                    _localizedDisplayName = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

