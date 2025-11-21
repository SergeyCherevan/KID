using System;
using System.ComponentModel;
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
        private ICodeEditorViewModel codeEditorViewModel;
        private IConsoleOutputViewModel consoleOutputViewModel;
        private IGraphicsOutputViewModel graphicsOutputViewModel;

        public MenuViewModel(
            CodeExecutionService codeExecutionService,
            ICodeEditorViewModel codeEditorViewModel,
            IConsoleOutputViewModel consoleOutputViewModel,
            IGraphicsOutputViewModel graphicsOutputViewModel
        )
        {
            this.codeExecutionService = codeExecutionService;
            this.codeEditorViewModel = codeEditorViewModel;
            this.consoleOutputViewModel = consoleOutputViewModel;
            this.graphicsOutputViewModel = graphicsOutputViewModel;

            // Подписываемся на изменения свойств codeEditorViewModel
            if (codeEditorViewModel is INotifyPropertyChanged notifyPropertyChanged)
            {
                notifyPropertyChanged.PropertyChanged += CodeEditorViewModel_PropertyChanged;
            }

            NewFileCommand = new RelayCommand(ExecuteNewFile);
            OpenFileCommand = new RelayCommand(ExecuteOpenFile);
            SaveFileCommand = new RelayCommand(ExecuteSaveFile);
            RunCommand = new RelayCommand(ExecuteRun);
            StopCommand = new RelayCommand(ExecuteStop);
            UndoCommand = new RelayCommand(ExecuteUndo, () => CanUndo);
            RedoCommand = new RelayCommand(ExecuteRedo, () => CanRedo);
        }

        private void CodeEditorViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ICodeEditorViewModel.CanUndo) || 
                e.PropertyName == nameof(ICodeEditorViewModel.CanRedo))
            {
                OnPropertyChanged(nameof(CanUndo));
                OnPropertyChanged(nameof(CanRedo));
            }
        }

        public bool IsStopButtonEnabled
        {
            get => isStopButtonEnabled;
            set => SetProperty(ref isStopButtonEnabled, value);
        }
        public bool CanUndo => codeEditorViewModel.CanUndo;
        public bool CanRedo => codeEditorViewModel.CanRedo;

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

            codeEditorViewModel.Text = code;
            consoleOutputViewModel.Clear();
            consoleOutputViewModel.AppendText("Консольный вывод...");
            graphicsOutputViewModel.Clear();
        }

        private void ExecuteOpenFile()
        {
            var code = FileService.OpenCodeFile();
            if (code != null)
            {
                codeEditorViewModel.Text = code;
                consoleOutputViewModel.Clear();
                consoleOutputViewModel.AppendText("Консольный вывод...");
                graphicsOutputViewModel.Clear();
            }
        }

        private void ExecuteSaveFile()
        {
            var code = codeEditorViewModel.Text;
            FileService.SaveCodeFile(code);
        }

        private async void ExecuteRun()
        {
            IsStopButtonEnabled = true;
            cancellationSource = new CancellationTokenSource();

            consoleOutputViewModel.Clear();
            graphicsOutputViewModel.Clear();

            await codeExecutionService.ExecuteAsync(
                codeEditorViewModel.Text,
                consoleOutputViewModel.AppendText,
                graphicsOutputViewModel.GraphicsCanvasControl,
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
            codeEditorViewModel.UndoCommand.Execute(null);
        }

        private void ExecuteRedo()
        {
            codeEditorViewModel.RedoCommand.Execute(null);
        }
    }
} 