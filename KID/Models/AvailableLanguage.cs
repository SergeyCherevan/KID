using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace KID.Models
{
    public class AvailableLanguage : INotifyPropertyChanged
    {
        private string _localizedDisplayName = string.Empty;

        public string CultureCode { get; set; } = string.Empty;
        public string EnglishName { get; set; } = string.Empty;

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

