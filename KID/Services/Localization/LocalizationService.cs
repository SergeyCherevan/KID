using System.ComponentModel;
using System.Globalization;
using System.Resources;
using System.Reflection;
using System.Text.RegularExpressions;
using KID.Models;
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

        public IEnumerable<AvailableLanguage> GetAvailableLanguages()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceNames = assembly.GetManifestResourceNames();
            var languages = new List<AvailableLanguage>();

            // Ищем все ресурсы вида "KID.Resources.Strings.<culture>.resources"
            // Также проверяем вариант без расширения .resources
            var pattern = new Regex(@"KID\.Resources\.Strings\.([a-z]{2}-[A-Z]{2})(?:\.resources)?$", RegexOptions.IgnoreCase);
            
            foreach (var resourceName in resourceNames)
            {
                var match = pattern.Match(resourceName);
                if (match.Success)
                {
                    var cultureCode = match.Groups[1].Value;
                    try
                    {
                        var culture = new CultureInfo(cultureCode);
                        var tempResourceManager = new ResourceManager("KID.Resources.Strings", assembly);
                        
                        // Получаем английское название языка из ресурса
                        // Сначала пробуем получить из ресурса для этой культуры
                        var englishName = tempResourceManager.GetString("Language_SelfName", culture);
                        
                        // Если не найдено, пробуем из английского ресурса
                        if (string.IsNullOrEmpty(englishName))
                        {
                            englishName = tempResourceManager.GetString("Language_SelfName", new CultureInfo("en-US"));
                        }
                        
                        // Если всё ещё не найдено, используем стандартное название культуры
                        if (string.IsNullOrEmpty(englishName))
                        {
                            englishName = culture.EnglishName;
                        }

                        var language = new AvailableLanguage
                        {
                            CultureCode = cultureCode,
                            EnglishName = englishName ?? cultureCode,
                            LocalizedDisplayName = englishName ?? cultureCode // Будет обновлено позже
                        };

                        languages.Add(language);
                    }
                    catch
                    {
                        // Пропускаем некорректные культуры
                    }
                }
            }

            // Если языки не найдены через ресурсы, пробуем найти известные культуры
            if (languages.Count == 0)
            {
                var knownCultures = new[] { "en-US", "ru-RU", "uk-UA" };
                foreach (var cultureCode in knownCultures)
                {
                    try
                    {
                        var culture = new CultureInfo(cultureCode);
                        var tempResourceManager = new ResourceManager("KID.Resources.Strings", assembly);
                        var englishName = tempResourceManager.GetString("Language_SelfName", culture)
                                        ?? tempResourceManager.GetString("Language_SelfName", new CultureInfo("en-US"))
                                        ?? culture.EnglishName;

                        languages.Add(new AvailableLanguage
                        {
                            CultureCode = cultureCode,
                            EnglishName = englishName ?? cultureCode,
                            LocalizedDisplayName = englishName ?? cultureCode
                        });
                    }
                    catch
                    {
                        // Пропускаем
                    }
                }
            }

            // Сортируем по английскому названию
            return languages.OrderBy(l => l.EnglishName);
        }
    }
}

