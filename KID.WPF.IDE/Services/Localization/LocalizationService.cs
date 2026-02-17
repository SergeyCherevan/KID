using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Resources;
using System.Runtime.CompilerServices;
using KID.Resources;
using KID.Services.Initialize.Interfaces;
using KID.Services.Localization.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace KID.Services.Localization
{
    public class LocalizationService : ILocalizationService, INotifyPropertyChanged
    {
        private readonly ResourceManager _resourceManager;
        private readonly ResourceManager _availableLanguageResourceManager;
        private readonly IServiceProvider _serviceProvider;
        private CultureInfo _currentCulture;
        private List<string>? _cachedAvailableLanguageKeys;
        private Dictionary<string, string>? _languageKeyToCultureCode;
        private Dictionary<string, string>? _cultureCodeToLanguageKey;
        private string _currentCultureName;

        public string CurrentCulture
        {
            get => _currentCultureName;
            private set
            {
                if (_currentCultureName != value)
                {
                    _currentCultureName = value;
                    OnPropertyChanged();
                }
            }
        }

        public event EventHandler? CultureChanged;
        public event PropertyChangedEventHandler? PropertyChanged;

        public LocalizationService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _resourceManager = new ResourceManager("KID.Resources.Strings", typeof(Strings).Assembly);
            _availableLanguageResourceManager = new ResourceManager("KID.Resources.AvailableLanguage", typeof(Strings).Assembly);
            _currentCulture = CultureInfo.CurrentUICulture;
            _currentCultureName = _currentCulture.Name;
        }

        public string GetString(string key)
        {
            if (string.IsNullOrEmpty(key))
                return string.Empty;

            try
            {
                return _resourceManager.GetString(key, _currentCulture)
                    ?? _resourceManager.GetString(key, new CultureInfo("en-US"))
                    ?? $"[{key}]";
            }
            catch
            {
                return $"[{key}]";
            }
        }

        public string GetString(string key, params object[] args)
        {
            if (string.IsNullOrEmpty(key))
                return string.Empty;

            try
            {
                var format = _resourceManager.GetString(key, _currentCulture) ?? $"[{key}]";
                return args != null && args.Length > 0 
                    ? string.Format(format, args) 
                    : format;
            }
            catch
            {
                return $"[{key}]";
            }
        }

        public void SetCulture(string cultureCode)
        {
            if (string.IsNullOrEmpty(cultureCode))
                return;

            try
            {
                var newCulture = CultureInfo.GetCultureInfo(cultureCode);
                if (_currentCulture.Name != newCulture.Name)
                {
                    _currentCulture = newCulture;
                    CultureInfo.CurrentUICulture = newCulture;
                    CultureInfo.CurrentCulture = newCulture;
                    
                    // Обновляем свойство CurrentCulture и вызываем PropertyChanged
                    CurrentCulture = newCulture.Name;

                    var windowConfigurationService = _serviceProvider.GetService<IWindowConfigurationService>();
                    windowConfigurationService?.SetUILanguage(newCulture.Name);
                    CultureChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            catch
            {
                // Игнорируем ошибки при установке культуры
            }
        }

        public IEnumerable<string> GetAvailableLanguages()
        {
            EnsureLanguageMappings();
            return _cachedAvailableLanguageKeys ?? new List<string>();
        }

        public string GetCultureCodeByLanguageKey(string languageKey)
        {
            if (string.IsNullOrWhiteSpace(languageKey))
                return string.Empty;

            EnsureLanguageMappings();
            if (_languageKeyToCultureCode != null
                && _languageKeyToCultureCode.TryGetValue(languageKey, out var cultureCode))
            {
                return cultureCode;
            }

            return string.Empty;
        }

        public string GetLanguageKeyByCultureCode(string cultureCode)
        {
            if (string.IsNullOrWhiteSpace(cultureCode))
                return string.Empty;

            EnsureLanguageMappings();
            if (_cultureCodeToLanguageKey != null
                && _cultureCodeToLanguageKey.TryGetValue(cultureCode, out var languageKey))
            {
                return languageKey;
            }

            return string.Empty;
        }

        private void EnsureLanguageMappings()
        {
            if (_cachedAvailableLanguageKeys != null
                && _languageKeyToCultureCode != null
                && _cultureCodeToLanguageKey != null)
            {
                return;
            }

            var languageKeys = new List<string>();
            var keyToCulture = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var cultureToKey = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                var countStr = _availableLanguageResourceManager.GetString("Language_Count", CultureInfo.InvariantCulture);
                if (string.IsNullOrEmpty(countStr) || !int.TryParse(countStr, out var count))
                {
                    _cachedAvailableLanguageKeys = languageKeys;
                    _languageKeyToCultureCode = keyToCulture;
                    _cultureCodeToLanguageKey = cultureToKey;
                    return;
                }

                for (var i = 0; i < count; i++)
                {
                    var cultureCode = _availableLanguageResourceManager.GetString($"Language_{i}_CultureCode", CultureInfo.InvariantCulture);
                    var englishName = _availableLanguageResourceManager.GetString($"Language_{i}_EnglishName", CultureInfo.InvariantCulture);

                    if (string.IsNullOrWhiteSpace(cultureCode) || string.IsNullOrWhiteSpace(englishName))
                        continue;

                    var key = $"Language_{englishName}";
                    languageKeys.Add(key);
                    keyToCulture[key] = cultureCode;
                    cultureToKey[cultureCode] = key;
                }
            }
            catch
            {
                // В случае ошибки оставляем пустые коллекции.
            }

            _cachedAvailableLanguageKeys = languageKeys;
            _languageKeyToCultureCode = keyToCulture;
            _cultureCodeToLanguageKey = cultureToKey;
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
