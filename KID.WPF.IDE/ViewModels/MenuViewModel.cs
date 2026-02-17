using KID.Services.CodeExecution.Contexts;
using KID.Services.CodeExecution.Interfaces;
using KID.Services.Errors.Interfaces;
using KID.Services.Files.Interfaces;
using KID.Services.Fonts.Interfaces;
using KID.Services.Initialize.Interfaces;
using KID.Services.Localization.Interfaces;
using KID.Services.Themes.Interfaces;
using KID.ViewModels.Infrastructure;
using KID.ViewModels.Interfaces;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows.Input;

namespace KID.ViewModels
{
    public class MenuViewModel : ViewModelBase, IMenuViewModel
    {
        private readonly ICodeExecutionService codeExecutionService;
        private readonly CanvasTextBoxContextFabric canvasTextBoxContextFabric;
        private readonly IWindowConfigurationService windowConfigurationService;
        private readonly ICodeEditorsViewModel codeEditorsViewModel;
        private readonly IConsoleOutputViewModel consoleOutputViewModel;
        private readonly IGraphicsOutputViewModel graphicsOutputViewModel;
        private readonly ICodeFileService codeFileService;
        private readonly IAsyncOperationErrorHandler asyncOperationErrorHandler;
        private readonly ILocalizationService localizationService;
        private readonly IThemeService themeService;
        private readonly IFontProviderService fontProviderService;
        private CancellationTokenSource? cancellationSource;



        public ObservableCollection<string> AvailableLanguages { get; }
        public ObservableCollection<string> AvailableThemes { get; }
        public ObservableCollection<string> AvailableFonts { get; }
        public ObservableCollection<double> AvailableFontSizes { get; }

        /// <summary>
        /// Выбранная тема (для отображения галочки в меню).
        /// </summary>
        public string SelectedThemeKey => windowConfigurationService?.Settings?.ColorTheme ?? string.Empty;

        /// <summary>
        /// Выбранный язык интерфейса (для отображения галочки в меню).
        /// </summary>
        public string SelectedCultureCode => windowConfigurationService?.Settings?.UILanguage ?? string.Empty;

        /// <summary>
        /// Выбранный ключ языка интерфейса (для отображения галочки в меню).
        /// </summary>
        public string SelectedLanguageKey => localizationService.GetLanguageKeyByCultureCode(SelectedCultureCode);

        /// <summary>
        /// Выбранный шрифт (для отображения галочки в меню).
        /// </summary>
        public string SelectedFontFamily => windowConfigurationService?.Settings?.FontFamily ?? string.Empty;

        /// <summary>
        /// Выбранный размер шрифта (для отображения галочки в меню).
        /// </summary>
        public double SelectedFontSize => windowConfigurationService?.Settings?.FontSize ?? 0.0;



        public ICommand NewFileCommand { get; }
        public ICommand OpenFileCommand { get; }
        public RelayCommand SaveFileCommand { get; }
        public ICommand SaveAsFileCommand { get; }
        public RelayCommand SaveAndSetAsTemplateCommand { get; }
        public RelayCommand RunCommand { get; }
        public RelayCommand StopCommand { get; }
        public RelayCommand UndoCommand { get; }
        public RelayCommand RedoCommand { get; }
        public ICommand ChangeLanguageCommand { get; }
        public ICommand ChangeThemeCommand { get; }
        public ICommand ChangeFontCommand { get; }
        public ICommand ChangeFontSizeCommand { get; }



        public MenuViewModel(
            IWindowConfigurationService windowConfigurationService,
            ICodeExecutionService codeExecutionService,
            CanvasTextBoxContextFabric canvasTextBoxContextFabric,
            ICodeEditorsViewModel codeEditorsViewModel,
            IConsoleOutputViewModel consoleOutputViewModel,
            IGraphicsOutputViewModel graphicsOutputViewModel,
            ICodeFileService codeFileService,
            IAsyncOperationErrorHandler asyncOperationErrorHandler,
            ILocalizationService localizationService,
            IThemeService themeService,
            IFontProviderService fontProviderService
        )
        {
            this.windowConfigurationService = windowConfigurationService ?? throw new ArgumentNullException(nameof(windowConfigurationService));
            this.codeExecutionService = codeExecutionService ?? throw new ArgumentNullException(nameof(codeExecutionService));
            this.canvasTextBoxContextFabric = canvasTextBoxContextFabric ?? throw new ArgumentNullException(nameof(canvasTextBoxContextFabric));

            this.codeEditorsViewModel = codeEditorsViewModel ?? throw new ArgumentNullException(nameof(codeEditorsViewModel));
            this.consoleOutputViewModel = consoleOutputViewModel ?? throw new ArgumentNullException(nameof(consoleOutputViewModel));
            this.graphicsOutputViewModel = graphicsOutputViewModel ?? throw new ArgumentNullException(nameof(graphicsOutputViewModel));
            this.codeFileService = codeFileService ?? throw new ArgumentNullException(nameof(codeFileService));
            this.asyncOperationErrorHandler = asyncOperationErrorHandler ?? throw new ArgumentNullException(nameof(asyncOperationErrorHandler));
            this.localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
            this.themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));
            this.fontProviderService = fontProviderService ?? throw new ArgumentNullException(nameof(fontProviderService));

            // Инициализируем список доступных шрифтов и размеров
            var fonts = fontProviderService.GetAvailableFonts();
            AvailableFonts = new ObservableCollection<string>(fonts ?? Array.Empty<string>());
            var fontSizes = fontProviderService.GetAvailableFontSizes();
            AvailableFontSizes = new ObservableCollection<double>(fontSizes ?? Array.Empty<double>());

            // Инициализируем список доступных языков
            var languages = localizationService.GetAvailableLanguages();
            AvailableLanguages = new ObservableCollection<string>(languages ?? Array.Empty<string>());

            // Инициализируем список доступных тем
            var themes = themeService.GetAvailableThemes();
            AvailableThemes = new ObservableCollection<string>(themes ?? Array.Empty<string>());

            // Подписываемся на изменения свойств codeEditorsViewModel
            if (codeEditorsViewModel is INotifyPropertyChanged notifyPropertyChanged)
            {
                notifyPropertyChanged.PropertyChanged += CodeEditorViewModel_PropertyChanged;
            }

            NewFileCommand = new RelayCommand(ExecuteNewFile);
            OpenFileCommand = new RelayCommand(ExecuteOpenFile);
            SaveFileCommand = new RelayCommand(ExecuteSaveFile, CanExecuteSaveFile);
            SaveAsFileCommand = new RelayCommand(ExecuteSaveAsFile, CanExecuteSaveAsFile);
            SaveAndSetAsTemplateCommand = new RelayCommand(
                ExecuteSaveAndSetAsTemplate,
                () =>
                {
                    var currentFileTab = codeEditorsViewModel.CurrentFileTab;
                    return currentFileTab != null
                        && codeEditorsViewModel.SaveAndSetAsTemplateCommand.CanExecute(currentFileTab);
                });
            RunCommand = new RelayCommand(ExecuteRun, () => !CanStop);
            StopCommand = new RelayCommand(ExecuteStop);
            UndoCommand = new RelayCommand(ExecuteUndo, () => CanUndo);
            RedoCommand = new RelayCommand(ExecuteRedo, () => CanRedo);
            ChangeLanguageCommand = new RelayCommand<string>(key => ChangeLanguage(key));
            ChangeThemeCommand = new RelayCommand<string>(key => ChangeTheme(key));
            ChangeFontCommand = new RelayCommand<string>(font => ChangeFont(font));
            ChangeFontSizeCommand = new RelayCommand<double>(fontSize => ChangeFontSize(fontSize));

            localizationService.CultureChanged += (s, e) => OnPropertyChanged(nameof(SelectedLanguageKey));

            windowConfigurationService.UILanguageSettingsChanged += (s, e) =>
            {
                OnPropertyChanged(nameof(SelectedCultureCode));
                OnPropertyChanged(nameof(SelectedLanguageKey));
            };

            windowConfigurationService.ColorThemeSettingsChanged += (s, e) =>
                OnPropertyChanged(nameof(SelectedThemeKey));

            windowConfigurationService.FontSettingsChanged += (s, e) =>
            {
                OnPropertyChanged(nameof(SelectedFontFamily));
                OnPropertyChanged(nameof(SelectedFontSize));
            };

            RefreshSelectedSettings();
        }



        private bool canStop;
        public bool CanStop
        {
            get => canStop;
            set
            {
                if (SetProperty(ref canStop, value))
                {
                    RunCommand.RaiseCanExecuteChanged();
                    StopCommand.RaiseCanExecuteChanged();
                }
            }
        }
        public bool CanUndo => codeEditorsViewModel.CanUndo;
        public bool CanRedo => codeEditorsViewModel.CanRedo;



        private void ExecuteNewFile()
        {
            if (windowConfigurationService?.Settings == null ||
                codeEditorsViewModel == null ||
                consoleOutputViewModel == null ||
                graphicsOutputViewModel == null ||
                localizationService == null)
                return;

            var code = windowConfigurationService.Settings.TemplateCode;
            codeEditorsViewModel.CreateAndAddFileTab(codeFileService.NewFilePath, code ?? string.Empty);
            if (!CanStop)
            {
                consoleOutputViewModel.Text = localizationService.GetString("Console_Output");
                graphicsOutputViewModel.Clear();
            }
        }

        private async void ExecuteOpenFile() {
            await asyncOperationErrorHandler.ExecuteAsync(ExecuteOpenFileAsync, "Error_FileOpenFailed");
        }

        private async Task ExecuteOpenFileAsync()
        {
            if (codeFileService == null || codeEditorsViewModel == null ||
                consoleOutputViewModel == null || graphicsOutputViewModel == null ||
                localizationService == null)
                return;

            var result = await codeFileService.OpenCodeFileWithPathAsync(codeFileService.CodeFileFilter);
            if (result != null)
            {
                var openedFiles = codeEditorsViewModel.OpenedFiles;
                var onlyTab = openedFiles.Count == 1 ? openedFiles[0] : null;
                var shouldReplaceNewFile = onlyTab != null
                    && codeFileService.IsNewFilePath(onlyTab.FilePath)
                    && !onlyTab.IsModified;

                codeEditorsViewModel.CreateAndAddFileTab(result.FilePath, result.Code);
                if (!CanStop)
                {
                    consoleOutputViewModel.Text = localizationService.GetString("Console_Output");
                    graphicsOutputViewModel.Clear();
                }

                if (shouldReplaceNewFile)
                    codeEditorsViewModel.CloseFileTab(onlyTab!);
            }
        }

        private void ExecuteSaveAndSetAsTemplate()
        {
            var currentFileTab = codeEditorsViewModel.CurrentFileTab;
            if (currentFileTab != null && codeEditorsViewModel.SaveAndSetAsTemplateCommand.CanExecute(currentFileTab))
            {
                codeEditorsViewModel.SaveAndSetAsTemplateCommand.Execute(currentFileTab);
            }
        }

        private bool CanExecuteSaveFile()
        {
            var currentFileTab = codeEditorsViewModel.CurrentFileTab;
            return currentFileTab != null && codeEditorsViewModel.SaveFileCommand.CanExecute(currentFileTab);
        }

        private void ExecuteSaveFile()
        {
            var currentFileTab = codeEditorsViewModel.CurrentFileTab;
            if (currentFileTab != null && codeEditorsViewModel.SaveFileCommand.CanExecute(currentFileTab))
            {
                codeEditorsViewModel.SaveFileCommand.Execute(currentFileTab);
            }
        }

        private bool CanExecuteSaveAsFile() => codeEditorsViewModel.CurrentFileTab != null;

        private void ExecuteSaveAsFile()
        {
            var currentFileTab = codeEditorsViewModel.CurrentFileTab;
            if (currentFileTab != null)
            {
                codeEditorsViewModel.SaveAsFileCommand.Execute(currentFileTab);
            }
        }

        private async void ExecuteRun()
        {
            if (codeEditorsViewModel == null || consoleOutputViewModel == null ||
                graphicsOutputViewModel == null || canvasTextBoxContextFabric == null ||
                codeExecutionService == null)
                return;

            CanStop = true;
            cancellationSource = new CancellationTokenSource();
            try
            {
                graphicsOutputViewModel.ResetOutputViewMinSizeToDefault();

                consoleOutputViewModel.Clear();
                graphicsOutputViewModel.Clear();

                if (graphicsOutputViewModel.GraphicsCanvasControl == null ||
                    consoleOutputViewModel.ConsoleOutputControl == null)
                    return;

                var context = canvasTextBoxContextFabric.Create(
                    graphicsOutputViewModel.GraphicsCanvasControl,
                    consoleOutputViewModel.ConsoleOutputControl,
                    cancellationSource.Token);

                var currentFileTab = codeEditorsViewModel.CurrentFileTab;
                var code = currentFileTab?.CurrentContent ?? string.Empty;
                if (!string.IsNullOrEmpty(code))
                    await codeExecutionService.ExecuteAsync(code, context);
            }
            finally
            {
                CanStop = false;
            }
        }

        private void ExecuteStop()
        {
            cancellationSource?.Cancel();
            CanStop = false;
        }

        private void ExecuteUndo()
        {
            if (codeEditorsViewModel?.UndoCommand != null)
            {
                codeEditorsViewModel.UndoCommand.Execute(null!);
            }
        }

        private void ExecuteRedo()
        {
            if (codeEditorsViewModel?.RedoCommand != null)
            {
                codeEditorsViewModel.RedoCommand.Execute(null!);
            }
        }

        private void ChangeLanguage(string? languageKey)
        {
            if (string.IsNullOrWhiteSpace(languageKey) || localizationService == null)
                return;

            var cultureCode = localizationService.GetCultureCodeByLanguageKey(languageKey);
            if (!string.IsNullOrWhiteSpace(cultureCode))
                localizationService.SetCulture(cultureCode);
        }

        private void ChangeTheme(string? themeKey)
        {
            if (string.IsNullOrWhiteSpace(themeKey) || themeService == null)
                return;

            themeService.ApplyTheme(themeKey);
        }

        private void ChangeFont(string? fontFamilyName)
        {
            if (string.IsNullOrEmpty(fontFamilyName) || windowConfigurationService?.Settings == null)
                return;

            var fontSize = windowConfigurationService.Settings.FontSize > 0
                ? windowConfigurationService.Settings.FontSize
                : 14.0;
            windowConfigurationService.SetFont(fontFamilyName, fontSize);
        }

        private void ChangeFontSize(double fontSize)
        {
            if (fontSize <= 0 || windowConfigurationService?.Settings == null)
                return;

            var fontFamily = windowConfigurationService.Settings.FontFamily ?? "Consolas";
            windowConfigurationService.SetFont(fontFamily, fontSize);
        }



        /// <summary>
        /// Обновляет отображение галочек у выбранных параметров в меню.
        /// </summary>
        private void RefreshSelectedSettings()
        {
            OnPropertyChanged(nameof(SelectedThemeKey));
            OnPropertyChanged(nameof(SelectedCultureCode));
            OnPropertyChanged(nameof(SelectedLanguageKey));
            OnPropertyChanged(nameof(SelectedFontFamily));
            OnPropertyChanged(nameof(SelectedFontSize));
        }

        private void CodeEditorViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ICodeEditorsViewModel.CanUndo) ||
                e.PropertyName == nameof(ICodeEditorsViewModel.CanRedo))
            {
                OnPropertyChanged(nameof(CanUndo));
                OnPropertyChanged(nameof(CanRedo));
                UndoCommand.RaiseCanExecuteChanged();
                RedoCommand.RaiseCanExecuteChanged();
            }
            if (e.PropertyName == nameof(ICodeEditorsViewModel.CurrentFileTab))
            {
                SaveFileCommand.RaiseCanExecuteChanged();
                if (SaveAsFileCommand is RelayCommand saveAsCommand)
                    saveAsCommand.RaiseCanExecuteChanged();
                SaveAndSetAsTemplateCommand.RaiseCanExecuteChanged();
            }
        }

    }
}
