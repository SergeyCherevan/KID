using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Resources;
using System.Runtime.CompilerServices;
using KID.Models;
using KID.Resources;
using KID.Services.Localization.Interfaces;

namespace KID.Services.Localization
{
    public class LocalizationService : ILocalizationService, INotifyPropertyChanged
    {
        private readonly ResourceManager _resourceManager;
        private readonly ResourceManager _availableLanguageResourceManager;
        private CultureInfo _currentCulture;
        private List<AvailableLanguage>? _cachedAvailableLanguages;
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

        public LocalizationService()
        {
            _resourceManager = new ResourceManager("KID.Resources.Strings", typeof(Strings).Assembly);
            _availableLanguageResourceManager = new ResourceManager("KID.Resources.AvailableLanguage", typeof(Strings).Assembly);
            _currentCulture = CultureInfo.CurrentUICulture;
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
                    
                    // Сбрасываем кэш доступных языков, чтобы обновить локализованные названия
                    _cachedAvailableLanguages = null;
                    
                    CultureChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            catch
            {
                // Игнорируем ошибки при установке культуры
            }
        }

        public IEnumerable<AvailableLanguage> GetAvailableLanguages()
        {
            // Используем кэш, если он уже создан
            if (_cachedAvailableLanguages != null)
                return _cachedAvailableLanguages;

            var languages = new List<AvailableLanguage>();

            try
            {
                // Получаем количество языков из ресурсов
                var countStr = _availableLanguageResourceManager.GetString("Language_Count", CultureInfo.InvariantCulture);
                if (string.IsNullOrEmpty(countStr) || !int.TryParse(countStr, out int count))
                    return languages;

                // Читаем данные о каждом языке
                for (int i = 0; i < count; i++)
                {
                    var cultureCode = _availableLanguageResourceManager.GetString($"Language_{i}_CultureCode", CultureInfo.InvariantCulture);
                    var englishName = _availableLanguageResourceManager.GetString($"Language_{i}_EnglishName", CultureInfo.InvariantCulture);

                    if (!string.IsNullOrEmpty(cultureCode) && !string.IsNullOrEmpty(englishName))
                    {
                        var language = new AvailableLanguage
                        {
                            CultureCode = cultureCode,
                            EnglishName = englishName
                        };

                        // Получаем локализованное название языка из основного файла ресурсов
                        var localizedKey = $"Language_{englishName}";
                        language.LocalizedDisplayName = GetString(localizedKey);

                        languages.Add(language);
                    }
                }
            }
            catch
            {
                // В случае ошибки возвращаем пустой список
                return languages;
            }

            // Кэшируем результат
            _cachedAvailableLanguages = languages;
            return languages;
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
