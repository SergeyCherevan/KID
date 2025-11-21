using KID.Services.Initialize.Interfaces;
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
        private readonly IMainViewModel mainViewModel;
        private readonly ICodeEditorViewModel codeEditorViewModel;
        private readonly IConsoleOutputViewModel consoleOutputViewModel;

        public WindowInitializationService(
            IWindowConfigurationService windowConfigurationService,
            IMainViewModel mainViewModel,
            ICodeEditorViewModel codeEditorViewModel,
            IConsoleOutputViewModel consoleOutputViewModel
        )
        {
            this.windowConfigurationService = windowConfigurationService;
            this.mainViewModel = mainViewModel;
            this.codeEditorViewModel = codeEditorViewModel;
            this.consoleOutputViewModel = consoleOutputViewModel;
        }

        public void Initialize()
        {
            windowConfigurationService.SetConfigurationFromFile();
            windowConfigurationService.SetDefaultCode();

            InitializeMainWindow();
            InitializeCodeEditor();
            InitializeConsole();
        }

        private void InitializeMainWindow()
        {
            var mainWindow = Application.Current.MainWindow as MainWindow;
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
            consoleOutputViewModel.Clear();
            consoleOutputViewModel.AppendText(windowConfigurationService.Settings.ConsoleMessage);
        }
    }
}
