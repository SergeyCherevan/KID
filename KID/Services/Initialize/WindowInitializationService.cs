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

        public WindowInitializationService(IWindowConfigurationService windowConfigurationService)
        {
            this.windowConfigurationService = windowConfigurationService;
        }

        public void Initialize()
        {
            windowConfigurationService.SetConfigurationFromFile();
            windowConfigurationService.SetDefaultCode();

            // Получаем главное окно из Application
            var mainWindow = Application.Current.MainWindow as MainWindow;
            if (mainWindow != null)
            {
                InitializeMainWindow(mainWindow);
                InitializeCodeEditor(mainWindow.CodeEditorView);
                InitializeConsole(mainWindow.ConsoleOutputView);
            }
        }

        private void InitializeMainWindow(MainWindow mainWindow)
        {
            if (mainWindow.DataContext is IMainViewModel vm)
            {
                vm.RequestDragMove += mainWindow.DragMove;
            }
        }

        private void InitializeCodeEditor(CodeEditorView codeEditorView)
        {
            codeEditorView.SetSyntaxHighlighting(windowConfigurationService.Settings.Language);
            codeEditorView.FontFamily = new System.Windows.Media.FontFamily(windowConfigurationService.Settings.FontFamily);
            codeEditorView.FontSize = windowConfigurationService.Settings.FontSize;
            codeEditorView.Text = windowConfigurationService.Settings.TemplateCode;
        }

        private void InitializeConsole(ConsoleOutputView consoleOutputView)
        {
            consoleOutputView.Clear();
            consoleOutputView.AppendText(windowConfigurationService.Settings.ConsoleMessage);
        }
    }
}
