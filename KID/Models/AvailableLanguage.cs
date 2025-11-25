using System.ComponentModel;

namespace KID.Models
{
    public class AvailableLanguage : INotifyPropertyChanged
    {
        private string _cultureCode = string.Empty;
        private string _displayName = string.Empty;
        private string _localizedDisplayName = string.Empty;

        public string CultureCode
        {
            get => _cultureCode;
            set
            {
                if (_cultureCode != value)
                {
                    _cultureCode = value;
                    OnPropertyChanged(nameof(CultureCode));
                }
            }
        }

        public string DisplayName
        {
            get => _displayName;
            set
            {
                if (_displayName != value)
                {
                    _displayName = value;
                    OnPropertyChanged(nameof(DisplayName));
                }
            }
        }

        public string LocalizedDisplayName
        {
            get => _localizedDisplayName;
            set
            {
                if (_localizedDisplayName != value)
                {
                    _localizedDisplayName = value;
                    OnPropertyChanged(nameof(LocalizedDisplayName));
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

