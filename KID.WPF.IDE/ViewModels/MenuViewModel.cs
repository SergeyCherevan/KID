using KID.Models;
using KID.Services.CodeExecution.Contexts;
using KID.Services.CodeExecution.Interfaces;
using KID.Services.Files.Interfaces;
using KID.Services.Initialize.Interfaces;
using KID.Services.Localization.Interfaces;
using KID.Services.Themes.Interfaces;
using KID.ViewModels.Infrastructure;
using KID.ViewModels.Interfaces;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
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
        private readonly IThemeService themeService;

        public ObservableCollection<AvailableLanguage> AvailableLanguages { get; }
        public ObservableCollection<AvailableTheme> AvailableThemes { get; }

        public MenuViewModel(
            IWindowConfigurationService windowConfigurationService,
            ICodeExecutionService codeExecutionService,
            CanvasTextBoxContextFabric canvasTextBoxContextFabric,
            ICodeEditorViewModel codeEditorViewModel,
            IConsoleOutputViewModel consoleOutputViewModel,
            IGraphicsOutputViewModel graphicsOutputViewModel,
            ICodeFileService codeFileService,
            ILocalizationService localizationService,
            IThemeService themeService
        )
        {
            this.windowConfigurationService = windowConfigurationService ?? throw new ArgumentNullException(nameof(windowConfigurationService));
            this.codeExecutionService = codeExecutionService ?? throw new ArgumentNullException(nameof(codeExecutionService));
            this.canvasTextBoxContextFabric = canvasTextBoxContextFabric ?? throw new ArgumentNullException(nameof(canvasTextBoxContextFabric));

            this.codeEditorViewModel = codeEditorViewModel ?? throw new ArgumentNullException(nameof(codeEditorViewModel));
            this.consoleOutputViewModel = consoleOutputViewModel ?? throw new ArgumentNullException(nameof(consoleOutputViewModel));
            this.graphicsOutputViewModel = graphicsOutputViewModel ?? throw new ArgumentNullException(nameof(graphicsOutputViewModel));
            this.codeFileService = codeFileService ?? throw new ArgumentNullException(nameof(codeFileService));
            this.localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
            this.themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));

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
            ChangeLanguageCommand = new RelayCommand<AvailableLanguage>(lang => ChangeLanguage(lang));
            ChangeThemeCommand = new RelayCommand<AvailableTheme>(theme => ChangeTheme(theme));

            // Подписываемся на изменение культуры для обновления UI
            localizationService.CultureChanged += (s, e) =>
            {
                OnPropertyChanged(string.Empty);
                UpdateLanguageDisplayNames();
                UpdateThemeDisplayNames();
            };
        }

        private void CodeEditorViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
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
            set
            {
                if (SetProperty(ref isStopButtonEnabled, value))
                {
                    // Принудительно обновляем состояние команд после изменения IsStopButtonEnabled
                    CommandManager.InvalidateRequerySuggested();
                }
            }
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
        public ICommand ChangeThemeCommand { get; }

        private void ExecuteNewFile()
        {
            if (windowConfigurationService?.Settings == null || 
                codeEditorViewModel == null || 
                consoleOutputViewModel == null || 
                graphicsOutputViewModel == null ||
                localizationService == null)
                return;
            
            var code = windowConfigurationService.Settings.TemplateCode;

            codeEditorViewModel.Text = code ?? string.Empty;
            consoleOutputViewModel.Text = localizationService.GetString("Console_Output");
            graphicsOutputViewModel.Clear();
        }

        private string GetFileFilter()
        {
            return localizationService?.GetString("FileFilter_CSharp") ?? "C# Files (*.cs)|*.cs";
        }

        private async void ExecuteOpenFile()
        {
            if (codeFileService == null || codeEditorViewModel == null || 
                consoleOutputViewModel == null || graphicsOutputViewModel == null ||
                localizationService == null)
                return;
            
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
            if (codeEditorViewModel == null || codeFileService == null)
                return;
            
            var code = codeEditorViewModel.Text;
            if (!string.IsNullOrEmpty(code))
            {
                await codeFileService.SaveCodeFileAsync(code, GetFileFilter());
            }
        }

        private async void ExecuteRun()
        {
            if (codeEditorViewModel == null || consoleOutputViewModel == null || 
                graphicsOutputViewModel == null || canvasTextBoxContextFabric == null ||
                codeExecutionService == null)
                return;
            
            IsStopButtonEnabled = true;
            cancellationSource = new CancellationTokenSource();

            graphicsOutputViewModel.ResetOutputViewMinSizeToDefault();

            consoleOutputViewModel.Clear();
            graphicsOutputViewModel.Clear();

            if (graphicsOutputViewModel.GraphicsCanvasControl == null || 
                consoleOutputViewModel.ConsoleOutputControl == null)
            {
                IsStopButtonEnabled = false;
                return;
            }

            CodeExecutionContext context = canvasTextBoxContextFabric.Create(
                graphicsOutputViewModel.GraphicsCanvasControl,
                consoleOutputViewModel.ConsoleOutputControl,
                cancellationSource.Token
            );

            var code = codeEditorViewModel.Text;
            if (!string.IsNullOrEmpty(code))
            {
                await codeExecutionService.ExecuteAsync(code, context);
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
            if (codeEditorViewModel?.UndoCommand != null)
            {
                codeEditorViewModel.UndoCommand.Execute(null);
            }
        }

        private void ExecuteRedo()
        {
            if (codeEditorViewModel?.RedoCommand != null)
            {
                codeEditorViewModel.RedoCommand.Execute(null);
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
    }
}