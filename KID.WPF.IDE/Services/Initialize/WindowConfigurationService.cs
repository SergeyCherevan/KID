using KID.Models;
using KID.Services.Errors.Interfaces;
using KID.Services.Initialize.Interfaces;
using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace KID.Services.Initialize
{
    public class WindowConfigurationService : IWindowConfigurationService
    {
        private readonly IAsyncOperationErrorHandler _asyncOperationErrorHandler;
        private readonly string _settingsPath;

        public WindowConfigurationData Settings { get; set; } = new WindowConfigurationData();

        /// <inheritdoc />
        public event EventHandler? FontSettingsChanged;
        /// <inheritdoc />
        public event EventHandler? UILanguageSettingsChanged;
        /// <inheritdoc />
        public event EventHandler? ColorThemeSettingsChanged;

        public WindowConfigurationService(IAsyncOperationErrorHandler asyncOperationErrorHandler)
        {
            _asyncOperationErrorHandler = asyncOperationErrorHandler ?? throw new ArgumentNullException(nameof(asyncOperationErrorHandler));
            
            // Путь к файлу настроек в AppData
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var appFolder = Path.Combine(appDataPath, "KID");
            Directory.CreateDirectory(appFolder); // Создаем папку, если её нет
            _settingsPath = Path.Combine(appFolder, "settings.json");
        }

        public void SetConfigurationFromFile()
        {
            try
            {
                if (File.Exists(_settingsPath))
                {
                    // Загружаем пользовательские настройки
                    string jsonText = File.ReadAllText(_settingsPath);
                    Settings = JsonSerializer.Deserialize<WindowConfigurationData>(jsonText) 
                        ?? new WindowConfigurationData();
                }
                else
                {
                    // Если файла нет, пробуем загрузить из DefaultWindowConfiguration.json
                    // как fallback, затем сохраняем в AppData
                    try
                    {
                        string defaultJson = File.ReadAllText("DefaultWindowConfiguration.json");
                        Settings = JsonSerializer.Deserialize<WindowConfigurationData>(defaultJson) 
                            ?? new WindowConfigurationData();
                    }
                    catch
                    {
                        // Если и дефолтного файла нет - используем значения по умолчанию
                        Settings = new WindowConfigurationData();
                    }
                    
                    // Сохраняем настройки в AppData для следующего запуска
                    SaveSettings();
                }
            }
            catch (Exception ex)
            {
                ExecuteWithErrorHandling(
                    () => throw new InvalidOperationException(ex.Message, ex),
                    "Error_ConfigLoadFailed");
                Settings = new WindowConfigurationData();
            }
        }

        public void SetDefaultCode()
        {
            try
            {
                // Если TemplateName содержит путь к файлу, загружаем его
                if (!string.IsNullOrEmpty(Settings.TemplateName) && File.Exists(Settings.TemplateName))
                {
                    Settings.TemplateCode = File.ReadAllText(Settings.TemplateName);
                }
                else
                {
                    // Иначе используем значение по умолчанию
                    Settings.TemplateCode = new WindowConfigurationData().TemplateCode;
                }
            }
            catch (Exception ex)
            {
                ExecuteWithErrorHandling(
                    () => throw new InvalidOperationException(ex.Message, ex),
                    "Error_TemplateLoadFailed");
                Settings.TemplateCode = new WindowConfigurationData().TemplateCode;
            }
        }

        public void SaveSettings()
        {
            try
            {
                var options = new JsonSerializerOptions 
                { 
                    WriteIndented = true 
                };
                string json = JsonSerializer.Serialize(Settings, options);
                File.WriteAllText(_settingsPath, json);
            }
            catch (Exception ex)
            {
                ExecuteWithErrorHandling(() => throw new InvalidOperationException(ex.Message, ex), "Error_SettingsSaveFailed");
            }
        }

        /// <inheritdoc />
        public void SetFont(string fontFamilyName, double fontSize)
        {
            if (!string.IsNullOrEmpty(fontFamilyName))
                Settings.FontFamily = fontFamilyName;
            if (fontSize > 0)
                Settings.FontSize = fontSize;
            SaveSettings();
            FontSettingsChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <inheritdoc />
        public void SetUILanguage(string cultureCode)
        {
            if (string.IsNullOrWhiteSpace(cultureCode))
                return;

            if (string.Equals(Settings.UILanguage, cultureCode, StringComparison.OrdinalIgnoreCase))
                return;

            Settings.UILanguage = cultureCode;
            SaveSettings();
            UILanguageSettingsChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <inheritdoc />
        public void SetColorTheme(string themeKey)
        {
            if (string.IsNullOrWhiteSpace(themeKey))
                return;

            if (string.Equals(Settings.ColorTheme, themeKey, StringComparison.Ordinal))
                return;

            Settings.ColorTheme = themeKey;
            SaveSettings();
            ColorThemeSettingsChanged?.Invoke(this, EventArgs.Empty);
        }

        private void ExecuteWithErrorHandling(Action action, string errorMessageKey)
        {
            _asyncOperationErrorHandler
                .ExecuteAsync(() =>
                {
                    action();
                    return Task.CompletedTask;
                }, errorMessageKey)
                .GetAwaiter()
                .GetResult();
        }
    }
}
