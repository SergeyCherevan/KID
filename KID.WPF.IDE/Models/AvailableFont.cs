using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace KID.Models
{
    /// <summary>
    /// Модель доступного шрифта для выбора в редакторе и консоли.
    /// </summary>
    public class AvailableFont : INotifyPropertyChanged
    {
        private string _localizedDisplayName = string.Empty;

        /// <summary>
        /// Системное имя шрифта (например, "Consolas", "Cascadia Code").
        /// </summary>
        public string FontFamilyName { get; set; } = string.Empty;

        /// <summary>
        /// Локализованное отображаемое имя шрифта.
        /// </summary>
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
