using KID.Services;
using KID.Services.CodeExecution;
using KID.Services.CodeExecution.Interfaces;
using KID.Services.Initialize.Interfaces;
using KID.ViewModels.Infrastructure;
using KID.ViewModels.Interfaces;
using System.ComponentModel;
using System.Windows.Input;

namespace KID.ViewModels
{
    public class MenuViewModel : ViewModelBase, IMenuViewModel
    {
        private readonly ICodeExecutionService codeExecutionService;
        private readonly CanvasTextBoxContextFabric canvasTextBoxContextFabric;
        private CancellationTokenSource? cancellationSource;
        private bool isStopButtonEnabled;

        private readonly IWindowConfigurationService windowConfigurationService;

        private readonly ICodeEditorViewModel codeEditorViewModel;
        private readonly IConsoleOutputViewModel consoleOutputViewModel;
        private readonly IGraphicsOutputViewModel graphicsOutputViewModel;

        public MenuViewModel(
            IWindowConfigurationService windowConfigurationService,
            ICodeExecutionService codeExecutionService,
            CanvasTextBoxContextFabric canvasTextBoxContextFabric,
            ICodeEditorViewModel codeEditorViewModel,
            IConsoleOutputViewModel consoleOutputViewModel,
            IGraphicsOutputViewModel graphicsOutputViewModel
        )
        {
            this.windowConfigurationService = windowConfigurationService;
            this.codeExecutionService = codeExecutionService;
            this.canvasTextBoxContextFabric = canvasTextBoxContextFabric;

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
            RunCommand = new RelayCommand(ExecuteRun, () => !IsStopButtonEnabled);
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
            var code = windowConfigurationService.Settings.TemplateCode;

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

            CodeExecutionContext context = canvasTextBoxContextFabric.Create(
                graphicsOutputViewModel.GraphicsCanvasControl,
                consoleOutputViewModel.ConsoleOutputControl,
                cancellationSource.Token
            );

            await codeExecutionService.ExecuteAsync(codeEditorViewModel.Text, context);

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