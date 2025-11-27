using KID.Services.Initialize.Interfaces;
using KID.Services.Localization.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;

namespace KID.Services.Initialize
{
    public class WindowConfigurationService : IWindowConfigurationService
    {
        private readonly ILocalizationService _localizationService;
        private readonly string _settingsPath;

        public WindowConfigurationData Settings { get; set; } = new WindowConfigurationData();

        public WindowConfigurationService(ILocalizationService localizationService)
        {
            _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
            
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
            catch (Exception)
            {
                MessageBox.Show(
                    _localizationService.GetString("Error_ConfigLoadFailed"),
                    _localizationService.GetString("Error_Title"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
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
            catch (Exception)
            {
                MessageBox.Show(
                    _localizationService.GetString("Error_TemplateLoadFailed"),
                    _localizationService.GetString("Error_Title"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
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
                MessageBox.Show(
                    $"Не удалось сохранить настройки: {ex.Message}",
                    _localizationService.GetString("Error_Title"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }
    }
}
