using System.IO;
using KID.Models;
using KID.Services.CodeExecution.Contexts;
using KID.Services.CodeExecution.Interfaces;
using KID.Services.Files;
using KID.Services.Files.Interfaces;
using KID.Services.Fonts.Interfaces;
using KID.Services.Initialize.Interfaces;
using KID.Services.Localization.Interfaces;
using KID.Services.Themes.Interfaces;
using System.Windows.Media;
using KID.ViewModels.Infrastructure;
using KID.ViewModels.Interfaces;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
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

        private readonly ICodeEditorsViewModel codeEditorsViewModel;
        private readonly IConsoleOutputViewModel consoleOutputViewModel;
        private readonly IGraphicsOutputViewModel graphicsOutputViewModel;
        private readonly ICodeFileService codeFileService;
        private readonly ILocalizationService localizationService;
        private readonly IThemeService themeService;
        private readonly IFontProviderService fontProviderService;

        public ObservableCollection<AvailableLanguage> AvailableLanguages { get; }
        public ObservableCollection<AvailableTheme> AvailableThemes { get; }
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
        /// Выбранный шрифт (для отображения галочки в меню).
        /// </summary>
        public string SelectedFontFamily => windowConfigurationService?.Settings?.FontFamily ?? string.Empty;

        /// <summary>
        /// Выбранный размер шрифта (для отображения галочки в меню).
        /// </summary>
        public double SelectedFontSize => windowConfigurationService?.Settings?.FontSize ?? 0.0;

        public MenuViewModel(
            IWindowConfigurationService windowConfigurationService,
            ICodeExecutionService codeExecutionService,
            CanvasTextBoxContextFabric canvasTextBoxContextFabric,
            ICodeEditorsViewModel codeEditorsViewModel,
            IConsoleOutputViewModel consoleOutputViewModel,
            IGraphicsOutputViewModel graphicsOutputViewModel,
            ICodeFileService codeFileService,
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
            AvailableLanguages = new ObservableCollection<AvailableLanguage>(languages ?? Array.Empty<AvailableLanguage>());
            
            // Обновляем локализованные имена для всех языков
            UpdateLanguageDisplayNames();

            // Инициализируем список доступных тем
            var themes = themeService.GetAvailableThemes();
            AvailableThemes = new ObservableCollection<AvailableTheme>(themes ?? Array.Empty<AvailableTheme>());
            
            // Обновляем локализованные имена для всех тем
            UpdateThemeDisplayNames();

            // Подписываемся на изменения свойств codeEditorsViewModel
            if (codeEditorsViewModel is INotifyPropertyChanged notifyPropertyChanged)
            {
                notifyPropertyChanged.PropertyChanged += CodeEditorViewModel_PropertyChanged;
            }

            NewFileCommand = new RelayCommand(ExecuteNewFile);
            OpenFileCommand = new RelayCommand(() => ExecuteAsync(ExecuteOpenFileAsync, "Error_FileOpenFailed"));
            SaveFileCommand = new RelayCommand(() => ExecuteAsync(ExecuteSaveFileAsync, "Error_FileSaveFailed"), () => CanSaveFile);
            SaveAsFileCommand = new RelayCommand(() => ExecuteAsync(ExecuteSaveAsFileAsync, "Error_FileSaveFailed"));
            SaveAndSetAsTemplateCommand = new RelayCommand(
                ExecuteSaveAndSetAsTemplate,
                () => codeEditorsViewModel.SaveAndSetAsTemplateCommand.CanExecute(codeEditorsViewModel.ActiveFile));
            RunCommand = new RelayCommand(() => ExecuteAsync(ExecuteRunAsync, "Error_RunFailed"), () => !IsStopButtonEnabled);
            StopCommand = new RelayCommand(ExecuteStop);
            UndoCommand = new RelayCommand(ExecuteUndo, () => CanUndo);
            RedoCommand = new RelayCommand(ExecuteRedo, () => CanRedo);
            ChangeLanguageCommand = new RelayCommand<AvailableLanguage>(lang => ChangeLanguage(lang));
            ChangeThemeCommand = new RelayCommand<AvailableTheme>(theme => ChangeTheme(theme));
            ChangeFontCommand = new RelayCommand<string>(font => ChangeFont(font));
            ChangeFontSizeCommand = new RelayCommand<double>(fontSize => ChangeFontSize(fontSize));

            // Подписываемся на изменение культуры для обновления UI
            localizationService.CultureChanged += (s, e) =>
            {
                OnPropertyChanged(string.Empty);
                UpdateLanguageDisplayNames();
                UpdateThemeDisplayNames();
            };

            windowConfigurationService.FontSettingsChanged += (s, e) =>
            {
                OnPropertyChanged(nameof(SelectedFontFamily));
                OnPropertyChanged(nameof(SelectedFontSize));
            };

            RefreshSelectedSettings();
        }

        /// <summary>
        /// Обновляет отображение галочек у выбранных параметров в меню.
        /// </summary>
        private void RefreshSelectedSettings()
        {
            OnPropertyChanged(nameof(SelectedThemeKey));
            OnPropertyChanged(nameof(SelectedCultureCode));
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
            if (e.PropertyName == nameof(ICodeEditorsViewModel.ActiveFile))
            {
                SaveFileCommand.RaiseCanExecuteChanged();
                SaveAndSetAsTemplateCommand.RaiseCanExecuteChanged();
            }
        }

        public bool IsStopButtonEnabled
        {
            get => isStopButtonEnabled;
            set
            {
                if (SetProperty(ref isStopButtonEnabled, value))
                {
                    RunCommand.RaiseCanExecuteChanged();
                    StopCommand.RaiseCanExecuteChanged();
                }
            }
        }
        public bool CanUndo => codeEditorsViewModel.CanUndo;
        public bool CanRedo => codeEditorsViewModel.CanRedo;

        private bool CanSaveFile
        {
            get
            {
                var activeFile = codeEditorsViewModel.ActiveFile;
                return activeFile != null
                    && !string.IsNullOrEmpty(activeFile.FilePath)
                    && !codeFileService.IsNewFilePath(activeFile.FilePath)
                    && activeFile.IsModified;
            }
        }

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

        private void ExecuteNewFile()
        {
            if (windowConfigurationService?.Settings == null || 
                codeEditorsViewModel == null || 
                consoleOutputViewModel == null || 
                graphicsOutputViewModel == null ||
                localizationService == null)
                return;
            
            var code = windowConfigurationService.Settings.TemplateCode;
            codeEditorsViewModel.AddFile(codeFileService.NewFilePath, code ?? string.Empty);
            if (!IsStopButtonEnabled)
            {
                consoleOutputViewModel.Text = localizationService.GetString("Console_Output");
                graphicsOutputViewModel.Clear();
            }
        }

        private string GetFileFilter()
        {
            return localizationService?.GetString("FileFilter_CSharp") ?? "C# Files (*.cs)|*.cs";
        }

        /// <summary>
        /// Выполняет асинхронное действие с обработкой ошибок и показом MessageBox.
        /// </summary>
        private async void ExecuteAsync(Func<Task> asyncAction, string errorMessageKey)
        {
            try
            {
                await asyncAction().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Application.Current.Dispatcher.Invoke(() => MessageBox.Show(
                    string.Format(localizationService.GetString(errorMessageKey) ?? errorMessageKey, ex.Message),
                    localizationService.GetString("Error_Title") ?? "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error));
            }
        }

        private async Task ExecuteOpenFileAsync()
        {
            if (codeFileService == null || codeEditorsViewModel == null ||
                consoleOutputViewModel == null || graphicsOutputViewModel == null ||
                localizationService == null)
                return;

            var result = await codeFileService.OpenCodeFileWithPathAsync(GetFileFilter());
            if (result != null)
            {
                var openedFiles = codeEditorsViewModel.OpenedFiles;
                var onlyTab = openedFiles.Count == 1 ? openedFiles[0] : null;
                var shouldReplaceNewFile = onlyTab != null
                    && codeFileService.IsNewFilePath(onlyTab.FilePath)
                    && !onlyTab.IsModified;

                codeEditorsViewModel.AddFile(result.FilePath, result.Code);
                if (!IsStopButtonEnabled)
                {
                    consoleOutputViewModel.Text = localizationService.GetString("Console_Output");
                    graphicsOutputViewModel.Clear();
                }

                if (shouldReplaceNewFile && onlyTab != null)
                    codeEditorsViewModel.CloseFile(onlyTab);
            }
        }

        private async Task ExecuteSaveFileAsync()
        {
            if (codeEditorsViewModel == null || codeFileService == null)
                return;

            if (!CanSaveFile)
            {
                await ExecuteSaveAsFileAsync();
                return;
            }

            var activeFile = codeEditorsViewModel.ActiveFile;
            if (activeFile == null)
                return;

            var code = activeFile.CurrentContent;
            if (!string.IsNullOrEmpty(code))
            {
                await codeFileService.SaveToPathAsync(activeFile.FilePath, code);
                codeEditorsViewModel.NotifyActiveFileSaved(code);
            }
        }

        private void ExecuteSaveAndSetAsTemplate()
        {
            var activeFile = codeEditorsViewModel.ActiveFile;
            if (activeFile != null && codeEditorsViewModel.SaveAndSetAsTemplateCommand.CanExecute(activeFile))
            {
                codeEditorsViewModel.SaveAndSetAsTemplateCommand.Execute(activeFile);
            }
        }

        private async Task ExecuteSaveAsFileAsync()
        {
            if (codeEditorsViewModel == null || codeFileService == null)
                return;

            var activeFile = codeEditorsViewModel.ActiveFile;
            if (activeFile == null)
                return;

            var code = activeFile.CurrentContent;
            if (string.IsNullOrEmpty(code))
                return;

            var defaultFileName = codeFileService.IsNewFilePath(activeFile.FilePath)
                ? "NewFile.cs"
                : Path.GetFileName(activeFile.FilePath);

            var savedPath = await codeFileService.SaveCodeFileAsync(code, GetFileFilter(), defaultFileName);
            if (savedPath != null)
            {
                activeFile.FilePath = savedPath;
                codeEditorsViewModel.NotifyActiveFileSaved(code);
            }
        }

        private async Task ExecuteRunAsync()
        {
            if (codeEditorsViewModel == null || consoleOutputViewModel == null ||
                graphicsOutputViewModel == null || canvasTextBoxContextFabric == null ||
                codeExecutionService == null)
                return;

            IsStopButtonEnabled = true;
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

                var activeFile = codeEditorsViewModel.ActiveFile;
                var code = activeFile?.CurrentContent ?? string.Empty;
                if (!string.IsNullOrEmpty(code))
                    await codeExecutionService.ExecuteAsync(code, context);
            }
            finally
            {
                IsStopButtonEnabled = false;
            }
        }

        private void ExecuteStop()
        {
            cancellationSource?.Cancel();
            IsStopButtonEnabled = false;
        }

        private void ExecuteUndo()
        {
            if (codeEditorsViewModel?.UndoCommand != null)
            {
                codeEditorsViewModel.UndoCommand.Execute(null);
            }
        }

        private void ExecuteRedo()
        {
            if (codeEditorsViewModel?.RedoCommand != null)
            {
                codeEditorsViewModel.RedoCommand.Execute(null);
            }
        }

        private void ChangeLanguage(AvailableLanguage language)
        {
            if (language == null || 
                localizationService == null || 
                windowConfigurationService?.Settings == null)
                return;
            
            localizationService.SetCulture(language.CultureCode);
            
            // Сохраняем выбранный язык в настройках
            windowConfigurationService.Settings.UILanguage = language.CultureCode;
            windowConfigurationService.SaveSettings();
            OnPropertyChanged(nameof(SelectedCultureCode));
        }

        private void UpdateLanguageDisplayNames()
        {
            if (AvailableLanguages == null || localizationService == null)
                return;
            
            foreach (var language in AvailableLanguages)
            {
                if (language == null)
                    continue;
                
                // Получаем локализованное название языка
                var key = $"Language_{language.EnglishName}";
                language.LocalizedDisplayName = localizationService.GetString(key);
            }
        }

        private void ChangeTheme(AvailableTheme theme)
        {
            if (theme == null || 
                themeService == null || 
                windowConfigurationService?.Settings == null)
                return;
            
            themeService.ApplyTheme(theme.ThemeKey);
            
            // Сохраняем выбранную тему в настройках
            windowConfigurationService.Settings.ColorTheme = theme.ThemeKey;
            windowConfigurationService.SaveSettings();
            OnPropertyChanged(nameof(SelectedThemeKey));
        }

        private void UpdateThemeDisplayNames()
        {
            if (AvailableThemes == null || localizationService == null)
                return;
            
            foreach (var theme in AvailableThemes)
            {
                if (theme == null)
                    continue;
                
                // Получаем локализованное название темы
                var key = $"Theme_{theme.EnglishName}";
                theme.LocalizedDisplayName = localizationService.GetString(key);
            }
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
    }
}