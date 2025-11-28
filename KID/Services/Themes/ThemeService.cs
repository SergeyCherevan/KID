using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using KID.Models;
using KID.Services.Localization.Interfaces;
using KID.Services.Themes.Interfaces;

namespace KID.Services.Themes
{
    public class ThemeService : IThemeService
    {
        private readonly ILocalizationService _localizationService;
        private string _currentTheme = "Light";

        public string CurrentTheme => _currentTheme;

        public ThemeService(ILocalizationService localizationService)
        {
            _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
        }

        public void ApplyTheme(string themeKey)
        {
            if (string.IsNullOrEmpty(themeKey))
                return;

            try
            {
                var app = Application.Current;
                if (app?.Resources == null)
                    return;

                // Очищаем текущие темы
                app.Resources.MergedDictionaries.Clear();

                // Загружаем новую тему
                var themePath = themeKey switch
                {
                    "Light" => "Themes/LightTheme.xaml",
                    "Dark" => "Themes/DarkTheme.xaml",
                    _ => "Themes/LightTheme.xaml"
                };

                var themeUri = new Uri($"/{themePath}", UriKind.Relative);
                var themeDictionary = new ResourceDictionary { Source = themeUri };
                app.Resources.MergedDictionaries.Add(themeDictionary);

                _currentTheme = themeKey;
            }
            catch (Exception)
            {
                // Игнорируем ошибки при применении темы
            }
        }

        public IEnumerable<AvailableTheme> GetAvailableThemes()
        {
            var themes = new List<AvailableTheme>
            {
                new AvailableTheme
                {
                    ThemeKey = "Light",
                    EnglishName = "Light"
                },
                new AvailableTheme
                {
                    ThemeKey = "Dark",
                    EnglishName = "Dark"
                }
            };

            // Обновляем локализованные названия
            foreach (var theme in themes)
            {
                var key = $"Theme_{theme.EnglishName}";
                theme.LocalizedDisplayName = _localizationService.GetString(key);
            }

            return themes;
        }
    }
}

