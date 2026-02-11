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

        private readonly IMainViewModel mainViewModel;
        private readonly ICodeEditorsViewModel codeEditorsViewModel;
        private readonly IConsoleOutputViewModel consoleOutputViewModel;

        private readonly MainWindow mainWindow;

        public WindowInitializationService(
            IWindowConfigurationService windowConfigurationService,
            ILocalizationService localizationService,
            IThemeService themeService,
            IMainViewModel mainViewModel,
            ICodeEditorsViewModel codeEditorsViewModel,
            IConsoleOutputViewModel consoleOutputViewModel,
            MainWindow mainWindow
        )
        {
            this.windowConfigurationService = windowConfigurationService ?? throw new ArgumentNullException(nameof(windowConfigurationService));
            this.localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
            this.themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));

            this.mainViewModel = mainViewModel ?? throw new ArgumentNullException(nameof(mainViewModel));
            this.codeEditorsViewModel = codeEditorsViewModel ?? throw new ArgumentNullException(nameof(codeEditorsViewModel));
            this.consoleOutputViewModel = consoleOutputViewModel ?? throw new ArgumentNullException(nameof(consoleOutputViewModel));

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
            
            InitializeMainWindow();
            InitializeCodeEditor();
            InitializeConsole();

            mainWindow.UpdateLayout();

            // Применяем шрифт и подсветку синтаксиса после загрузки представления (TextEditor создаётся асинхронно)
            mainWindow.Dispatcher.BeginInvoke(new Action(ApplyCodeEditorSettings), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        private void ApplyCodeEditorSettings()
        {
            if (codeEditorsViewModel == null || windowConfigurationService?.Settings == null)
                return;

            if (!string.IsNullOrEmpty(windowConfigurationService.Settings.ProgrammingLanguage))
            {
                codeEditorsViewModel.SetSyntaxHighlighting(windowConfigurationService.Settings.ProgrammingLanguage);
            }

            if (!string.IsNullOrEmpty(windowConfigurationService.Settings.FontFamily))
            {
                codeEditorsViewModel.FontFamily = new System.Windows.Media.FontFamily(windowConfigurationService.Settings.FontFamily);
            }

            if (windowConfigurationService.Settings.FontSize > 0)
            {
                codeEditorsViewModel.FontSize = windowConfigurationService.Settings.FontSize;
            }
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

        private void InitializeMainWindow()
        {
            if (mainViewModel != null && mainWindow != null)
            {
                mainViewModel.RequestDragMove += mainWindow.DragMove;
            }
        }

        private void InitializeCodeEditor()
        {
            if (codeEditorsViewModel == null || windowConfigurationService?.Settings == null)
                return;



            var templateCode = windowConfigurationService.Settings.TemplateCode ?? string.Empty;
            codeEditorsViewModel.AddFile(CodeEditorsViewModel.NewFilePath, templateCode);
        }

        private void InitializeConsole()
        {
            if (consoleOutputViewModel == null)
                return;

            consoleOutputViewModel.Text = localizationService.GetString("Console_Output");

            if (windowConfigurationService?.Settings != null)
            {
                if (!string.IsNullOrEmpty(windowConfigurationService.Settings.FontFamily))
                    consoleOutputViewModel.FontFamily = new System.Windows.Media.FontFamily(windowConfigurationService.Settings.FontFamily);
                if (windowConfigurationService.Settings.FontSize > 0)
                    consoleOutputViewModel.FontSize = windowConfigurationService.Settings.FontSize;
            }
        }
    }
}
