using KID.Services.Initialize.Interfaces;
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
        public WindowConfigurationData Settings { get; set; } = new WindowConfigurationData();
        public void SetConfigurationFromFile()
        {
            try
            {
                string jsonText = File.ReadAllText("DefaultWindowConfiguration.json");
                Settings = JsonSerializer.Deserialize<WindowConfigurationData>(jsonText) ?? new WindowConfigurationData();
            }
            catch (Exception)
            {
                MessageBox.Show("Не удалось загрузить конфигурационный файл. Будут использованы настройки по умолчанию.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                Settings = new WindowConfigurationData();
            }
        }

        public void SetDefaultCode()
        {
            try
            {
                Settings.TemplateCode = File.ReadAllText(Settings.TemplateName);
            }
            catch (Exception)
            {
                MessageBox.Show("Не удалось загрузить файл с шаблонным кодом. Будет использован встроенный шаблон.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                Settings.TemplateCode = new WindowConfigurationData().TemplateCode;
            }
        }
    }
}
