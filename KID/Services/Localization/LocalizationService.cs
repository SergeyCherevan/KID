using System.ComponentModel;
using System.Globalization;
using System.Resources;
using System.Reflection;
using System.Collections.ObjectModel;
using System.Linq;
using KID.Services.Localization.Interfaces;
using KID.Models;

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

        public IReadOnlyList<AvailableLanguage> GetAvailableLanguages()
        {
            var languages = new List<AvailableLanguage>();
            
            // Получаем все ресурсные файлы из сборки
            var assembly = Assembly.GetExecutingAssembly();
            var resourceNames = assembly.GetManifestResourceNames()
                .Where(name => name.Contains("Strings", StringComparison.OrdinalIgnoreCase) &&
                              name.EndsWith(".resources", StringComparison.OrdinalIgnoreCase))
                .ToList();

            // Извлекаем культуры из имен ресурсов
            var foundCultures = new HashSet<string>();
            
            foreach (var resourceName in resourceNames)
            {
                // Формат: KID.Resources.Strings.{culture}.resources
                var parts = resourceName.Split('.');
                
                // Ищем часть с культурой после "Strings"
                for (int i = 0; i < parts.Length - 1; i++)
                {
                    if (parts[i].Equals("Strings", StringComparison.OrdinalIgnoreCase))
                    {
                        var culturePart = parts[i + 1];
                        // Если следующая часть не "resources", то это код культуры
                        if (!culturePart.Equals("resources", StringComparison.OrdinalIgnoreCase))
                        {
                            // Проверяем, что это похоже на код культуры
                            if (culturePart.Contains('-') || culturePart.Length == 2)
                            {
                                foundCultures.Add(culturePart);
                            }
                        }
                        break;
                    }
                }
            }

            // Всегда добавляем известные культуры из проекта (на случай, если они не найдены в манифесте)
            var knownCultures = new[] { "en-US", "ru-RU", "uk-UA" };
            foreach (var cultureCode in knownCultures)
            {
                foundCultures.Add(cultureCode);
            }

            // Создаем объекты AvailableLanguage для каждой найденной культуры
            // Локализованные имена должны быть на текущем языке приложения
            foreach (var cultureCode in foundCultures)
            {
                try
                {
                    var culture = new CultureInfo(cultureCode);
                    var languageKey = GetLanguageKey(cultureCode);
                    // Используем текущую культуру приложения для получения локализованного имени
                    var localizedName = GetString("Language_" + languageKey);
                    
                    // Если локализованное имя не найдено, используем DisplayName
                    if (localizedName.StartsWith("[Language_", StringComparison.Ordinal))
                    {
                        localizedName = culture.DisplayName;
                    }
                    
                    languages.Add(new AvailableLanguage
                    {
                        CultureCode = cultureCode,
                        DisplayName = culture.DisplayName,
                        LocalizedDisplayName = localizedName
                    });
                }
                catch
                {
                    // Игнорируем невалидные культуры
                }
            }

            return languages.OrderBy(l => l.CultureCode).ToList();
        }

        private string GetLanguageKey(string cultureCode)
        {
            return cultureCode switch
            {
                "ru-RU" => "Russian",
                "uk-UA" => "Ukrainian",
                "en-US" => "English",
                _ => cultureCode
            };
        }

        private string GetString(string key, CultureInfo culture)
        {
            if (string.IsNullOrEmpty(key))
                return string.Empty;

            var value = _resourceManager.GetString(key, culture);
            
            // Если не найдено, пробуем en-US как fallback
            if (value == null && culture.Name != "en-US")
            {
                value = _resourceManager.GetString(key, new CultureInfo("en-US"));
            }
            
            return value ?? $"[{key}]";
        }
    }
}

