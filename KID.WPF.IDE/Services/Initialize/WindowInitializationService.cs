using KID.Services.Files.Interfaces;
using KID.Services.Initialize.Interfaces;
using KID.Services.Localization.Interfaces;
using KID.Services.Themes.Interfaces;
using KID.ViewModels;
using KID.ViewModels.Interfaces;
using KID.Views;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace KID.Services.Initialize
{
    public class WindowInitializationService : IWindowInitializationService
    {
        private readonly IWindowConfigurationService windowConfigurationService;
        private readonly ILocalizationService localizationService;
        private readonly IThemeService themeService;

        private readonly ICodeEditorsViewModel codeEditorsViewModel;
        private readonly IConsoleOutputViewModel consoleOutputViewModel;
        private readonly ICodeFileService codeFileService;

        private readonly MainWindow mainWindow;

        public WindowInitializationService(
            IWindowConfigurationService windowConfigurationService,
            ILocalizationService localizationService,
            IThemeService themeService,
            ICodeEditorsViewModel codeEditorsViewModel,
            IConsoleOutputViewModel consoleOutputViewModel,
            ICodeFileService codeFileService,
            MainWindow mainWindow
        )
        {
            this.windowConfigurationService = windowConfigurationService ?? throw new ArgumentNullException(nameof(windowConfigurationService));
            this.localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
            this.themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));

            this.codeEditorsViewModel = codeEditorsViewModel ?? throw new ArgumentNullException(nameof(codeEditorsViewModel));
            this.consoleOutputViewModel = consoleOutputViewModel ?? throw new ArgumentNullException(nameof(consoleOutputViewModel));
            this.codeFileService = codeFileService ?? throw new ArgumentNullException(nameof(codeFileService));

            this.mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
        }

        public void Initialize()
        {
            windowConfigurationService.SetConfigurationFromFile();
            windowConfigurationService.SetDefaultCode();

            // Применяем тему из настроек
            InitializeTheme();
            
            // Применяем язык интерфейса из настроек
            InitializeLanguage();
            
            InitializeCodeEditor();
            InitializeConsole();

            mainWindow.UpdateLayout();
        }

        private void InitializeTheme()
        {
            // Применяем тему из настроек
            if (!string.IsNullOrEmpty(windowConfigurationService.Settings.ColorTheme))
            {
                themeService.ApplyTheme(windowConfigurationService.Settings.ColorTheme);
            }
            else
            {
                // Если тема не указана, используем светлую по умолчанию
                themeService.ApplyTheme("Light");
            }
        }

        private void InitializeLanguage()
        {
            // Устанавливаем язык интерфейса из настроек
            if (!string.IsNullOrEmpty(windowConfigurationService.Settings.UILanguage))
            {
                localizationService.SetCulture(windowConfigurationService.Settings.UILanguage);
            }
        }

        private void InitializeCodeEditor()
        {
            if (codeEditorsViewModel == null || windowConfigurationService?.Settings == null)
                return;

            var templateCode = windowConfigurationService.Settings.TemplateCode ?? string.Empty;
            codeEditorsViewModel.AddFile(codeFileService.NewFilePath, templateCode);
        }

        private void InitializeConsole()
        {
            if (consoleOutputViewModel == null)
                return;

            consoleOutputViewModel.Text = localizationService.GetString("Console_Output");
        }
    }
}
