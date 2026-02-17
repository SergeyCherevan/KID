using System;
using System.Collections.Generic;
using System.Windows;
using KID.Services.Initialize.Interfaces;
using KID.Services.Themes.Interfaces;

namespace KID.Services.Themes
{
    public class ThemeService : IThemeService
    {
        private readonly IWindowConfigurationService _windowConfigurationService;
        private string _currentTheme = "Theme_Light";
        private readonly App _app;

        public string CurrentTheme => _currentTheme;

        public ThemeService(IWindowConfigurationService windowConfigurationService, App app)
        {
            _windowConfigurationService = windowConfigurationService ?? throw new ArgumentNullException(nameof(windowConfigurationService));
            _app = app ?? throw new ArgumentNullException(nameof(app));
        }

        public void ApplyTheme(string themeKey)
        {
            if (string.IsNullOrEmpty(themeKey))
                return;

            var normalizedThemeKey = NormalizeThemeKey(themeKey);
            if (string.IsNullOrEmpty(normalizedThemeKey))
                return;

            try
            {
                if (_app?.Resources == null)
                    return;

                // Очищаем текущие темы
                _app.Resources.MergedDictionaries.Clear();

                // Загружаем новую тему
                var themePath = normalizedThemeKey switch
                {
                    "Theme_Light" => "Themes/LightTheme.xaml",
                    "Theme_Dark" => "Themes/DarkTheme.xaml",
                    _ => "Themes/LightTheme.xaml"
                };

                var themeUri = new Uri($"/{themePath}", UriKind.Relative);
                var themeDictionary = new ResourceDictionary { Source = themeUri };
                _app.Resources.MergedDictionaries.Add(themeDictionary);

                _currentTheme = normalizedThemeKey;
                _windowConfigurationService.SetColorTheme(normalizedThemeKey);
            }
            catch (Exception)
            {
                // Игнорируем ошибки при применении темы
            }
        }

        public IEnumerable<string> GetAvailableThemes()
        {
            return new List<string>
            {
                "Theme_Light",
                "Theme_Dark"
            };
        }

        private static string NormalizeThemeKey(string themeKey)
        {
            return themeKey switch
            {
                "Light" => "Theme_Light",
                "Dark" => "Theme_Dark",
                "Theme_Light" => "Theme_Light",
                "Theme_Dark" => "Theme_Dark",
                _ => string.Empty
            };
        }
    }
}
