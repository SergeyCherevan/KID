using KID.Services.Initialize.Interfaces;
using KID.ViewModels;
using KID.Views;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KID.Services.Initialize
{
    public class WindowInitializationService : IWindowInitializationService
    {
        private MainWindow mainViewModel;
        private CodeEditorView codeEditorView;
        private ConsoleOutputView consoleOutputView;
        private IWindowConfigurationService windowConfigurationService;

        public WindowInitializationService(
            MainWindow mw,
            CodeEditorView cev,
            ConsoleOutputView cov,
            IWindowConfigurationService wcs
        )
        { 
            mainViewModel = mw;
            codeEditorView = cev;
            consoleOutputView = cov;
            windowConfigurationService = wcs;
        }

        public void Initialize()
        {
            windowConfigurationService.SetConfigurationFromFile();
            windowConfigurationService.SetDefaultCode();

            InitializeMainWindow(mainViewModel);
            InitializeCodeEditor(codeEditorView);
            InitializeConsole(consoleOutputView);
        }

        private void InitializeMainWindow(MainWindow mainWindow)
        {
            if (mainWindow.DataContext is MainViewModel vm)
            {
                vm.RequestDragMove += mainWindow.DragMove;
            }
        }

        private void InitializeCodeEditor(CodeEditorView codeEditorView)
        {
            codeEditorView.SetSyntaxHighlighting(windowConfigurationService.Settings.Language);
            codeEditorView.FontFamily = new System.Windows.Media.FontFamily(windowConfigurationService.Settings.FontFamily);
            codeEditorView.FontSize = windowConfigurationService.Settings.FontSize;
            codeEditorView.Text = windowConfigurationService.Settings.TamplateCode;
        }

        private void InitializeConsole(ConsoleOutputView consoleOutputView)
        {
            consoleOutputView.Clear();
            consoleOutputView.AppendText(windowConfigurationService.Settings.ConsoleMessage);
        }
    }
}
