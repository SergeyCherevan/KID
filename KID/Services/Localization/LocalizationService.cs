using System.ComponentModel;
using System.Globalization;
using System.Resources;
using System.Reflection;
using KID.Services.Localization.Interfaces;

namespace KID.Services.Localization
{
    public class LocalizationService : ILocalizationService, INotifyPropertyChanged
    {
        private ResourceManager _resourceManager;
        private CultureInfo _currentCulture;

        public string CurrentCulture
        {
            get => _currentCulture.Name;
            private set
            {
                try
                {
                    var newCulture = new CultureInfo(value);
                    if (_currentCulture.Name != newCulture.Name)
                    {
                        _currentCulture = newCulture;
                        OnPropertyChanged(nameof(CurrentCulture));
                    }
                }
                catch
                {
                    // Если культура не найдена, игнорируем
                }
            }
        }
        
        public event EventHandler? CultureChanged;
        public event PropertyChangedEventHandler? PropertyChanged;

        public LocalizationService()
        {
            _resourceManager = new ResourceManager("KID.Resources.Strings", Assembly.GetExecutingAssembly());
            _currentCulture = CultureInfo.CurrentUICulture;
        }

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public string GetString(string key)
        {
            if (string.IsNullOrEmpty(key))
                return string.Empty;

            // Пытаемся получить строку для текущей культуры
            var value = _resourceManager.GetString(key, _currentCulture);
            
            // Если не найдено и текущая культура не en-US, пробуем en-US как fallback
            if (value == null && _currentCulture.Name != "en-US")
            {
                value = _resourceManager.GetString(key, new CultureInfo("en-US"));
            }
            
            return value ?? $"[{key}]";
        }

        public string GetString(string key, params object[] args)
        {
            var format = GetString(key);
            if (args == null || args.Length == 0)
                return format;
            
            try
            {
                return string.Format(format, args);
            }
            catch
            {
                return format;
            }
        }

        public void SetCulture(string cultureCode)
        {
            try
            {
                CurrentCulture = cultureCode;
                CultureChanged?.Invoke(this, EventArgs.Empty);
            }
            catch
            {
                // Если культура не найдена, используем текущую
            }
        }
    }
}

