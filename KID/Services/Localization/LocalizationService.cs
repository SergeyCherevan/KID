using System.Globalization;
using System.Resources;
using System.Reflection;
using KID.Services.Localization.Interfaces;

namespace KID.Services.Localization
{
    public class LocalizationService : ILocalizationService
    {
        private ResourceManager _resourceManager;
        private CultureInfo _currentCulture;

        public string CurrentCulture => _currentCulture.Name;
        
        public event EventHandler? CultureChanged;

        public LocalizationService()
        {
            _resourceManager = new ResourceManager("KID.Resources.Strings", Assembly.GetExecutingAssembly());
            _currentCulture = CultureInfo.CurrentUICulture;
        }

        public string GetString(string key)
        {
            if (string.IsNullOrEmpty(key))
                return string.Empty;

            var value = _resourceManager.GetString(key, _currentCulture);
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
                _currentCulture = new CultureInfo(cultureCode);
                CultureChanged?.Invoke(this, EventArgs.Empty);
            }
            catch
            {
                // Если культура не найдена, используем текущую
            }
        }
    }
}

