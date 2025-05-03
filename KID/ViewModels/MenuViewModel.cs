using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using KID.Services;
using KID.Views;

namespace KID.ViewModels
{
    public class MenuViewModel : ViewModelBase
    {
        private readonly CodeExecutionService codeExecutionService;
        private CancellationTokenSource cancellationSource;
        private bool isStopButtonEnabled;

        public MenuViewModel()
        {
            codeExecutionService = new CodeExecutionService(
                new CSharpCompiler(),
                new DefaultCodeRunner()
            );

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

            MainWindow.Instance.CodeEditorView.Text = code;
            MainWindow.Instance.ConsoleOutputView.Clear();
            MainWindow.Instance.ConsoleOutputView.AppendText("Консольный вывод...");
            MainWindow.Instance.GraphicsOutputView.Clear();
        }

        private void ExecuteOpenFile()
        {
            var code = FileService.OpenCodeFile();
            if (code != null)
            {
                MainWindow.Instance.CodeEditorView.Text = code;
                MainWindow.Instance.ConsoleOutputView.Clear();
                MainWindow.Instance.ConsoleOutputView.AppendText("Консольный вывод...");
                MainWindow.Instance.GraphicsOutputView.Clear();
            }
        }

        private void ExecuteSaveFile()
        {
            var code = MainWindow.Instance.CodeEditorView.Text;
            FileService.SaveCodeFile(code);
        }

        private async void ExecuteRun()
        {
            IsStopButtonEnabled = true;
            cancellationSource = new CancellationTokenSource();

            MainWindow.Instance.ConsoleOutputView.Clear();
            MainWindow.Instance.GraphicsOutputView.Clear();

            await codeExecutionService.ExecuteAsync(
                MainWindow.Instance.CodeEditorView.Text,
                MainWindow.Instance.ConsoleOutputView.AppendText,
                MainWindow.Instance.GraphicsOutputView.GraphicsCanvasControl,
                cancellationSource.Token
            );

            IsStopButtonEnabled = false;
        }

        private void ExecuteStop()
        {
            cancellationSource?.Cancel();
            IsStopButtonEnabled = false;
        }

        private void ExecuteUndo()
        {
            if (MainWindow.Instance.CodeEditorView.CanUndo())
            {
                MainWindow.Instance.CodeEditorView.Undo();
            }
        }

        private void ExecuteRedo()
        {
            if (MainWindow.Instance.CodeEditorView.CanRedo())
            {
                MainWindow.Instance.CodeEditorView.Redo();
            }
        }
    }
} 