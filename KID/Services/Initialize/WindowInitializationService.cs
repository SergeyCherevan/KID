using KID.Services.Initialize.Interfaces;
using KID.Services.Localization.Interfaces;
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

        private readonly IMainViewModel mainViewModel;
        private readonly ICodeEditorViewModel codeEditorViewModel;
        private readonly IConsoleOutputViewModel consoleOutputViewModel;

        private readonly MainWindow mainWindow;

        public WindowInitializationService(
            IWindowConfigurationService windowConfigurationService,
            ILocalizationService localizationService,
            IMainViewModel mainViewModel,
            ICodeEditorViewModel codeEditorViewModel,
            IConsoleOutputViewModel consoleOutputViewModel,
            MainWindow mainWindow
        )
        {
            this.windowConfigurationService = windowConfigurationService;
            this.localizationService = localizationService;

            this.mainViewModel = mainViewModel;
            this.codeEditorViewModel = codeEditorViewModel;
            this.consoleOutputViewModel = consoleOutputViewModel;

            this.mainWindow = mainWindow;
        }

        public void Initialize()
        {
            windowConfigurationService.SetConfigurationFromFile();
            windowConfigurationService.SetDefaultCode();

            // Применяем язык интерфейса из настроек
            InitializeLanguage();
            
            InitializeMainWindow();
            InitializeCodeEditor();
            InitializeConsole();

            mainWindow.UpdateLayout();
        }

        private void InitializeLanguage()
        {
            // Устанавливаем язык интерфейса из настроек
            if (!string.IsNullOrEmpty(windowConfigurationService.Settings.UILanguage))
            {
                localizationService.SetCulture(windowConfigurationService.Settings.UILanguage);
            }
        }

        private void InitializeMainWindow()
        {
            mainViewModel.RequestDragMove += mainWindow.DragMove;
        }

        private void InitializeCodeEditor()
        {
            codeEditorViewModel.SetSyntaxHighlighting(windowConfigurationService.Settings.Language);
            codeEditorViewModel.FontFamily = new System.Windows.Media.FontFamily(windowConfigurationService.Settings.FontFamily);
            codeEditorViewModel.FontSize = windowConfigurationService.Settings.FontSize;
            codeEditorViewModel.Text = windowConfigurationService.Settings.TemplateCode;
        }

        private void InitializeConsole()
        {
            consoleOutputViewModel.Text = windowConfigurationService.Settings.ConsoleMessage;
        }
    }
}
