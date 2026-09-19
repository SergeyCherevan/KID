using KID.Services.Files.Interfaces;
using KID.Services.Diagnostics;
using KID.Services.Errors.Interfaces;
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
using Microsoft.Extensions.Logging;

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
        private readonly IAsyncOperationErrorHandler asyncOperationErrorHandler;
        private readonly ILogger<WindowInitializationService>? logger;

        private readonly MainWindow mainWindow;

        public WindowInitializationService(
            IWindowConfigurationService windowConfigurationService,
            ILocalizationService localizationService,
            IThemeService themeService,
            ICodeEditorsViewModel codeEditorsViewModel,
            IConsoleOutputViewModel consoleOutputViewModel,
            ICodeFileService codeFileService,
            IAsyncOperationErrorHandler asyncOperationErrorHandler,
            MainWindow mainWindow,
            ILogger<WindowInitializationService>? logger = null
        )
        {
            this.windowConfigurationService = windowConfigurationService ?? throw new ArgumentNullException(nameof(windowConfigurationService));
            this.localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
            this.themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));

            this.codeEditorsViewModel = codeEditorsViewModel ?? throw new ArgumentNullException(nameof(codeEditorsViewModel));
            this.consoleOutputViewModel = consoleOutputViewModel ?? throw new ArgumentNullException(nameof(consoleOutputViewModel));
            this.codeFileService = codeFileService ?? throw new ArgumentNullException(nameof(codeFileService));
            this.asyncOperationErrorHandler = asyncOperationErrorHandler ?? throw new ArgumentNullException(nameof(asyncOperationErrorHandler));
            this.logger = logger;

            this.mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
        }

        public async Task InitializeAsync()
        {
            await windowConfigurationService.SetConfigurationFromFileAsync();
            await windowConfigurationService.SetDefaultCodeAsync();

            // Применяем тему из настроек
            InitializeTheme();
            
            // Применяем язык интерфейса из настроек
            await InitializeLanguageAsync();
            
            await InitializeCodeEditorAsync();
            InitializeConsole();

            mainWindow.UpdateLayout();
            logger?.LogInformation(
                DiagnosticEventIds.StartupCompleted,
                "Window initialization completed.");
        }

        private void InitializeTheme()
        {
            themeService.ApplyTheme(windowConfigurationService.Settings.ColorTheme);
        }

        private async Task InitializeLanguageAsync()
        {
            // Устанавливаем язык интерфейса из настроек
            if (!string.IsNullOrEmpty(windowConfigurationService.Settings.UILanguage))
            {
                await localizationService.SetCultureAsync(windowConfigurationService.Settings.UILanguage);
            }
        }

        private async Task InitializeCodeEditorAsync()
        {
            if (codeEditorsViewModel == null || windowConfigurationService?.Settings == null)
                return;

            var restored = false;
            await asyncOperationErrorHandler.ExecuteAsync(
                async () => restored = await codeEditorsViewModel.RestoreSessionAsync(),
                "Error_SessionRestoreFailed");

            if (!restored && codeEditorsViewModel.OpenedFileTabs.Count == 0)
            {
                var templateCode = windowConfigurationService.Settings.TemplateCode ?? string.Empty;
                await codeEditorsViewModel.CreateAndAddFileTabAsync(codeFileService.NewFilePath, templateCode);
            }
        }

        private void InitializeConsole()
        {
            if (consoleOutputViewModel == null)
                return;

            consoleOutputViewModel.Text = localizationService.GetString("Console_Output");
        }
    }
}
