using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace KID.Models
{
    /// <summary>
    /// Модель доступного размера шрифта для выбора в редакторе и консоли.
    /// </summary>
    public class AvailableFontSize : INotifyPropertyChanged
    {
        private string _localizedDisplayName = string.Empty;

        /// <summary>
        /// Числовое значение размера шрифта.
        /// </summary>
        public double Size { get; set; }

        /// <summary>
        /// Локализованное отображаемое имя (например, "14" или "14 pt").
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
