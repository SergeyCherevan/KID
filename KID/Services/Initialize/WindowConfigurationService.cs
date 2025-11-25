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

        public WindowConfigurationData Settings { get; set; } = new WindowConfigurationData();

        public WindowConfigurationService(ILocalizationService localizationService)
        {
            _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
        }

        public void SetConfigurationFromFile()
        {
            try
            {
                string jsonText = File.ReadAllText("DefaultWindowConfiguration.json");
                Settings = JsonSerializer.Deserialize<WindowConfigurationData>(jsonText) ?? new WindowConfigurationData();
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
                Settings.TemplateCode = File.ReadAllText(Settings.TemplateName);
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
    }
}
