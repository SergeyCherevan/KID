using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using KID.Services;
using KID.ViewModels.Infrastructure;
using KID.ViewModels.Interfaces;
using KID.Views;
using Microsoft.Extensions.DependencyInjection;

namespace KID.ViewModels
{
    public class MenuViewModel : ViewModelBase, IMenuViewModel
    {
        private readonly CodeExecutionService codeExecutionService;
        private CancellationTokenSource? cancellationSource;
        private bool isStopButtonEnabled;

        public MenuViewModel(CodeExecutionService codeExecutionService)
        {
            this.codeExecutionService = codeExecutionService;

            NewFileCommand = new RelayCommand(ExecuteNewFile);
            OpenFileCommand = new RelayCommand(ExecuteOpenFile);
            SaveFileCommand = new RelayCommand(ExecuteSaveFile);
            RunCommand = new RelayCommand(ExecuteRun);
            StopCommand = new RelayCommand(ExecuteStop);
            UndoCommand = new RelayCommand(ExecuteUndo);
            RedoCommand = new RelayCommand(ExecuteRedo);
        }

        public bool IsStopButtonEnabled
        {
            get => isStopButtonEnabled;
            set => SetProperty(ref isStopButtonEnabled, value);
        }

        public ICommand NewFileCommand { get; }
        public ICommand OpenFileCommand { get; }
        public ICommand SaveFileCommand { get; }
        public ICommand RunCommand { get; }
        public ICommand StopCommand { get; }
        public ICommand UndoCommand { get; }
        public ICommand RedoCommand { get; }

        private void ExecuteNewFile()
        {
            var code = @"System.Console.WriteLine(""Hello World!"");

KID.Graphics.SetColor(255, 0, 0);
KID.Graphics.Circle(150, 150, 125);

KID.Graphics.SetColor(0x0000FF);
KID.Graphics.Rectangle(150, 150, 100, 100);

KID.Graphics.SetColor(""White"");
KID.Graphics.SetFont(""Arial"", 25);
KID.Graphics.Text(150, 150, ""Hello\nWorld!"");";

            var mainWindow = Application.Current.MainWindow as MainWindow;
            if (mainWindow != null)
            {
                mainWindow.CodeEditorView.Text = code;
                mainWindow.ConsoleOutputView.Clear();
                mainWindow.ConsoleOutputView.AppendText("Консольный вывод...");
                mainWindow.GraphicsOutputView.Clear();
            }
        }

        private void ExecuteOpenFile()
        {
            var code = FileService.OpenCodeFile();
            if (code != null)
            {
                var mainWindow = Application.Current.MainWindow as MainWindow;
                if (mainWindow != null)
                {
                    mainWindow.CodeEditorView.Text = code;
                    mainWindow.ConsoleOutputView.Clear();
                    mainWindow.ConsoleOutputView.AppendText("Консольный вывод...");
                    mainWindow.GraphicsOutputView.Clear();
                }
            }
        }

        private void ExecuteSaveFile()
        {
            var mainWindow = Application.Current.MainWindow as MainWindow;
            if (mainWindow != null)
            {
                var code = mainWindow.CodeEditorView.Text;
                FileService.SaveCodeFile(code);
            }
        }

        private async void ExecuteRun()
        {
            IsStopButtonEnabled = true;
            cancellationSource = new CancellationTokenSource();

            var mainWindow = Application.Current.MainWindow as MainWindow;
            if (mainWindow != null)
            {
                mainWindow.ConsoleOutputView.Clear();
                mainWindow.GraphicsOutputView.Clear();

                await codeExecutionService.ExecuteAsync(
                    mainWindow.CodeEditorView.Text,
                    mainWindow.ConsoleOutputView.AppendText,
                    mainWindow.GraphicsOutputView.GraphicsCanvasControl,
                    cancellationSource.Token
                );
            }

            IsStopButtonEnabled = false;
        }

        private void ExecuteStop()
        {
            cancellationSource?.Cancel();
            IsStopButtonEnabled = false;
        }

        private void ExecuteUndo()
        {
            var mainWindow = Application.Current.MainWindow as MainWindow;
            if (mainWindow != null && mainWindow.CodeEditorView.CanUndo())
            {
                mainWindow.CodeEditorView.Undo();
            }
        }

        private void ExecuteRedo()
        {
            var mainWindow = Application.Current.MainWindow as MainWindow;
            if (mainWindow != null && mainWindow.CodeEditorView.CanRedo())
            {
                mainWindow.CodeEditorView.Redo();
            }
        }
    }
} 