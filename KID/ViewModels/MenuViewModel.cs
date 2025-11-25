using KID.Services.CodeExecution.Contexts;
using KID.Services.CodeExecution.Interfaces;
using KID.Services.Files.Interfaces;
using KID.Services.Initialize.Interfaces;
using KID.Services.Localization.Interfaces;
using KID.ViewModels.Infrastructure;
using KID.ViewModels.Interfaces;
using KID.Models;
using System.Collections.ObjectModel;
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
        private readonly ICodeFileService codeFileService;
        private readonly ILocalizationService localizationService;

        public ObservableCollection<AvailableLanguage> AvailableLanguages { get; }

        public MenuViewModel(
            IWindowConfigurationService windowConfigurationService,
            ICodeExecutionService codeExecutionService,
            CanvasTextBoxContextFabric canvasTextBoxContextFabric,
            ICodeEditorViewModel codeEditorViewModel,
            IConsoleOutputViewModel consoleOutputViewModel,
            IGraphicsOutputViewModel graphicsOutputViewModel,
            ICodeFileService codeFileService,
            ILocalizationService localizationService
        )
        {
            this.windowConfigurationService = windowConfigurationService;
            this.codeExecutionService = codeExecutionService;
            this.canvasTextBoxContextFabric = canvasTextBoxContextFabric;

            this.codeEditorViewModel = codeEditorViewModel;
            this.consoleOutputViewModel = consoleOutputViewModel;
            this.graphicsOutputViewModel = graphicsOutputViewModel;
            this.codeFileService = codeFileService;
            this.localizationService = localizationService;

            // Инициализируем список доступных языков
            var languages = localizationService.GetAvailableLanguages();
            AvailableLanguages = new ObservableCollection<AvailableLanguage>(languages);
            
            // Обновляем локализованные имена для всех языков
            foreach (var lang in AvailableLanguages)
            {
                lang.LocalizedDisplayName = localizationService.GetString($"Language_{GetLanguageKey(lang.CultureCode)}");
            }

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
            ChangeLanguageCommand = new RelayCommand<AvailableLanguage>(lang => ChangeLanguage(lang.CultureCode));

            // Подписываемся на изменение культуры для обновления UI
            localizationService.CultureChanged += (s, e) => 
            {
                OnPropertyChanged(string.Empty);
                // Обновляем локализованные имена языков
                foreach (var lang in AvailableLanguages)
                {
                    lang.LocalizedDisplayName = localizationService.GetString($"Language_{GetLanguageKey(lang.CultureCode)}");
                }
            };
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
        public ICommand ChangeLanguageCommand { get; }

        private void ExecuteNewFile()
        {
            var code = windowConfigurationService.Settings.TemplateCode;

            codeEditorViewModel.Text = code;
            consoleOutputViewModel.Text = localizationService.GetString("Console_Output");
            graphicsOutputViewModel.Clear();
        }

        private string GetFileFilter()
        {
            return localizationService.GetString("FileFilter_CSharp");
        }

        private async void ExecuteOpenFile()
        {
            var code = await codeFileService.OpenCodeFileAsync(GetFileFilter());
            if (code != null)
            {
                codeEditorViewModel.Text = code;
                consoleOutputViewModel.Text = localizationService.GetString("Console_Output");
                graphicsOutputViewModel.Clear();
            }
        }

        private async void ExecuteSaveFile()
        {
            var code = codeEditorViewModel.Text;
            await codeFileService.SaveCodeFileAsync(code, GetFileFilter());
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

        private void ChangeLanguage(string cultureCode)
        {
            localizationService.SetCulture(cultureCode);
        }

        private string GetLanguageKey(string cultureCode)
        {
            return cultureCode switch
            {
                "ru-RU" => "Russian",
                "uk-UA" => "Ukrainian",
                "en-US" => "English",
                _ => cultureCode
            };
        }
    }
}